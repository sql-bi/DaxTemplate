namespace Dax.Template.Tests
{
    using Dax.Template.Constants;
    using Dax.Template.Exceptions;
    using Dax.Template.Functions;
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using Xunit;

    /// <summary>
    /// Offline regression tests for the DAX user-defined-function library (Phase 3) feature: build a
    /// synthetic compatibility-1702 in-memory model via <see cref="FunctionOfflineModelFixture"/>, run the
    /// real <see cref="Engine.ApplyTemplates"/> dispatch for the <c>FunctionLibraryTemplate</c> class, and
    /// assert both the resulting TOM <see cref="Function"/> shape and a golden-file snapshot. Compatibility
    /// level 1702 (rather than the shared <see cref="OfflineModelFixture"/>'s 1600, the Calendar fixture's
    /// 1701, or the CalculationGroup fixture's 1605) is required because <see cref="FunctionLibraryTemplate"/>
    /// enforces it up front — see <see cref="FunctionOfflineModelFixture"/>'s remarks. These guard
    /// <see cref="Functions.FunctionLibraryTemplate"/> and its <c>Engine.ApplyFunctionLibraryTemplate</c>
    /// dispatch. A parallel opt-in live-server test exercises the same path against a real engine.
    /// </summary>
    public class FunctionLibraryGoldenTests
    {
        private const string FunctionsTemplatePath = @".\_data\Templates\Config-04 - Functions.template.json";
        private const string FunctionsDisabledTemplatePath = @".\_data\Templates\Config-04b - Functions-Disabled.template.json";

        private const string PercentOfTotalExpectedExpression =
            "( Amount: SCALAR DECIMAL VAL, Filter: TABLEREF, DecimalPlaces: INT64 = 2 ) => " +
            "\r\nROUND (\r\n    DIVIDE ( Amount, CALCULATE ( Amount, REMOVEFILTERS ( Filter ) ) ) * 100,\r\n    DecimalPlaces\r\n)";

        private const string SafeDivideExpectedExpression =
            "( Numerator: SCALAR DECIMAL, Denominator: SCALAR DECIMAL = BLANK(), AlternateResult: ANYVAL = BLANK() ) => " +
            "IF ( ISBLANK ( Denominator ) || Denominator = 0, AlternateResult, Numerator / Denominator )";

        [Fact]
        public void ApplyTemplates_FunctionLibraryStandardConfig_CreatesPercentOfTotalAndSafeDivideFunctions()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(FunctionsTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            Assert.Equal(2, database.Model.Functions.Count);

            var percentOfTotal = database.Model.Functions.Find("PercentOfTotal");
            Assert.NotNull(percentOfTotal);
            Assert.Equal(PercentOfTotalExpectedExpression, percentOfTotal!.Expression);
            Assert.Equal("Returns Amount as a percentage of its total when Filter is removed, rounded to DecimalPlaces.", percentOfTotal.Description);
            Assert.False(percentOfTotal.IsHidden);
            Assert.Contains(percentOfTotal.Annotations, a => a.Name == Attributes.SqlbiTemplate && a.Value == Attributes.SqlbiTemplateFunctions);

            var safeDivide = database.Model.Functions.Find("SafeDivide");
            Assert.NotNull(safeDivide);
            Assert.Equal(SafeDivideExpectedExpression, safeDivide!.Expression);
            Assert.Equal("Divides Numerator by Denominator, returning AlternateResult when Denominator is blank or zero.", safeDivide.Description);
            Assert.False(safeDivide.IsHidden);
            Assert.Contains(safeDivide.Annotations, a => a.Name == Attributes.SqlbiTemplate && a.Value == Attributes.SqlbiTemplateFunctions);
        }

        [Fact]
        public void ApplyTemplates_FunctionLibraryStandardConfig_MatchesSnapshot()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(FunctionsTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var actual = GoldenFile.SerializeNormalized(database);
            GoldenFile.AssertMatchesSnapshot(actual, "Config-04 - Functions");
        }

        [Fact]
        public void ApplyTemplates_FunctionLibraryStandardConfigAppliedTwice_ProducesIdenticalNormalizedOutput()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(FunctionsTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);
            var firstRun = GoldenFile.SerializeNormalized(database);

            engine.ApplyTemplates(database.Model);
            var secondRun = GoldenFile.SerializeNormalized(database);

            // Assert: re-running the same template against its own prior output is stable.
            Assert.Equal(firstRun, secondRun);
            Assert.Equal(2, database.Model.Functions.Count);
        }

        [Fact]
        public void ApplyTemplate_ReappliedWithFunctionRemovedFromDefinition_RemovesOrphanedFunction()
        {
            // Arrange: first apply (via the engine/Config-04 JSON) produces {PercentOfTotal, SafeDivide}.
            var database = FunctionOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(FunctionsTemplatePath));
            engine.ApplyTemplates(database.Model);

            Assert.Equal(2, database.Model.Functions.Count); // sanity check on the initial state

            var definitionWithOnlySafeDivide = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "SafeDivide",
                        Parameters =
                        [
                            new ParameterDefinition { Name = "Numerator", Type = "SCALAR", Subtype = "DECIMAL" },
                            new ParameterDefinition { Name = "Denominator", Type = "SCALAR", Subtype = "DECIMAL", DefaultExpression = "BLANK()" },
                            new ParameterDefinition { Name = "AlternateResult", Type = "ANYVAL", DefaultExpression = "BLANK()" },
                        ],
                        Body = "IF ( ISBLANK ( Denominator ) || Denominator = 0, AlternateResult, Numerator / Denominator )",
                    },
                ],
            };

            // Act: re-apply directly against the model with a definition that dropped "PercentOfTotal".
            new FunctionLibraryTemplate(definitionWithOnlySafeDivide).ApplyTemplate(database.Model, isEnabled: true);

            // Assert: "PercentOfTotal" was removed as an orphan; "SafeDivide" remains.
            Assert.Equal(1, database.Model.Functions.Count);
            Assert.Null(database.Model.Functions.Find("PercentOfTotal"));
            Assert.NotNull(database.Model.Functions.Find("SafeDivide"));
        }

        [Fact]
        public void ApplyTemplates_FunctionLibraryTemplateDisabledAfterEnabled_RemovesAllFunctions()
        {
            // Arrange: apply the enabled Functions config once, so both functions exist.
            var database = FunctionOfflineModelFixture.Build();
            var engineEnabled = new Engine(Package.LoadFromFile(FunctionsTemplatePath));
            engineEnabled.ApplyTemplates(database.Model);

            Assert.Equal(2, database.Model.Functions.Count); // sanity check on the initial state

            // Act: re-apply a config whose FunctionLibraryTemplate entry is disabled.
            var engineDisabled = new Engine(Package.LoadFromFile(FunctionsDisabledTemplatePath));
            engineDisabled.ApplyTemplates(database.Model);

            // Assert: every function this template created is removed.
            Assert.Equal(0, database.Model.Functions.Count);
            Assert.Null(database.Model.Functions.Find("PercentOfTotal"));
            Assert.Null(database.Model.Functions.Find("SafeDivide"));
        }

        [Fact]
        public void ApplyTemplates_FunctionLibraryTemplateDisabledWithNoExistingFunctions_DoesNotThrow()
        {
            // Arrange: a fresh fixture has no functions at all.
            var database = FunctionOfflineModelFixture.Build();
            Assert.Equal(0, database.Model.Functions.Count); // sanity check on the initial state
            var engineDisabled = new Engine(Package.LoadFromFile(FunctionsDisabledTemplatePath));

            // Act
            var exception = Record.Exception(() => engineDisabled.ApplyTemplates(database.Model));

            // Assert: disabled + nothing-to-remove is a safe no-op, matching every sibling handler.
            Assert.Null(exception);
            Assert.Equal(0, database.Model.Functions.Count);
        }

        [Fact]
        public void ApplyTemplates_FunctionLibraryBelowMinimumCompatibilityLevel_ThrowsInvalidConfigurationException()
        {
            // Arrange: the shared OfflineModelFixture is pinned at compatibility level 1600, well below the
            // 1702 floor FunctionLibraryTemplate enforces.
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(FunctionsTemplatePath));

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(() => engine.ApplyTemplates(database.Model));

            // Assert
            Assert.Contains("1702", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_ScalarParameterWithoutSubtype_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Test",
                        Parameters = [new ParameterDefinition { Name = "X", Type = "SCALAR" }],
                        Body = "X",
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("X", exception.Message);
            Assert.Contains("SCALAR", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_PassingModeOnReferenceTypeParameter_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Test",
                        Parameters = [new ParameterDefinition { Name = "T", Type = "TABLEREF", PassingMode = "VAL" }],
                        Body = "1",
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("T", exception.Message);
            Assert.Contains("TABLEREF", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_MandatoryParameterAfterOptionalParameter_ThrowsInvalidConfigurationException()
        {
            // Arrange: "A" has a DefaultExpression (optional); "B" follows without one (mandatory).
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Test",
                        Parameters =
                        [
                            new ParameterDefinition { Name = "A", Type = "INT64", DefaultExpression = "1" },
                            new ParameterDefinition { Name = "B", Type = "INT64" },
                        ],
                        Body = "A + B",
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("B", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_RawExpressionAndBodyBothSet_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Both",
                        RawExpression = "( X: INT64 ) => X",
                        Body = "X",
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("Both", exception.Message);
            Assert.Contains("both", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_DuplicateFunctionNames_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition { Name = "Dup", RawExpression = "( X: INT64 ) => X" },
                    new FunctionDefinition { Name = "Dup", RawExpression = "( X: INT64 ) => X + 1" },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("Dup", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_ParameterWithPassingModeButNoType_ThrowsInvalidConfigurationException()
        {
            // Arrange: "P" has a PassingMode but no Type; PassingMode requires an explicit Type.
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Test",
                        Parameters = [new ParameterDefinition { Name = "P", PassingMode = "VAL" }],
                        Body = "P",
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("P", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_FunctionWithBlankName_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions = [new FunctionDefinition { Name = "  ", RawExpression = "( X: INT64 ) => X" }],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("Undefined Name", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_ParameterWithBlankName_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Test",
                        Parameters = [new ParameterDefinition { Name = "  ", Type = "INT64" }],
                        Body = "1",
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("Test", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_FunctionWithNeitherRawExpressionNorBody_ThrowsInvalidConfigurationException()
        {
            // Arrange: RawExpression, Body, and MultiLineBody are all unset — the "neither" branch of the
            // RawExpression-XOR-Body check (the "both" branch is covered by
            // ApplyTemplate_RawExpressionAndBodyBothSet_ThrowsInvalidConfigurationException).
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions =
                [
                    new FunctionDefinition
                    {
                        Name = "Neither",
                        Parameters = [new ParameterDefinition { Name = "X", Type = "INT64" }],
                    },
                ],
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true));

            // Assert
            Assert.Contains("Neither", exception.Message);
            Assert.Contains("neither", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_RawExpressionOnly_ProducesFunctionWithVerbatimExpression()
        {
            // Arrange
            var database = FunctionOfflineModelFixture.Build();
            var definition = new FunctionLibraryTemplateDefinition
            {
                Functions = [new FunctionDefinition { Name = "Increment", RawExpression = "( X: INT64 ) => X + 1" }],
            };

            // Act
            new FunctionLibraryTemplate(definition).ApplyTemplate(database.Model, isEnabled: true);

            // Assert
            var increment = database.Model.Functions.Find("Increment");
            Assert.NotNull(increment);
            Assert.Equal("( X: INT64 ) => X + 1", increment!.Expression);
        }

        /// <summary>
        /// Opt-in: applies the Functions config against a real connected model (env-configured) and
        /// verifies the engine produces model changes without persisting them. Skipped unless live-server
        /// env vars are set. This is also the only place real server-side compatibility-1702 enforcement for
        /// user-defined functions is exercised (see <see cref="Functions.FunctionLibraryTemplate"/>).
        /// </summary>
        [LiveServerFact]
        public void FunctionLibraryStandardConfig_LiveServerApply_ProducesModelChanges()
        {
            var serverConn = Environment.GetEnvironmentVariable(LiveServerFactAttribute.ServerEnvVar)!;
            var databaseName = Environment.GetEnvironmentVariable(LiveServerFactAttribute.DatabaseEnvVar)!;

            using var server = new Server();
            server.Connect(serverConn);
            try
            {
                var database = server.Databases[databaseName];
                var engine = new Engine(Package.LoadFromFile(FunctionsTemplatePath));

                engine.ApplyTemplates(database.Model);
                var changes = Engine.GetModelChanges(database.Model);

                Assert.NotNull(changes);
                // intentionally not calling SaveChanges: changes are discarded on disconnect.
            }
            finally
            {
                server.Disconnect();
            }
        }
    }
}