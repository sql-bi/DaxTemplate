namespace Dax.Template.Tests
{
    using Dax.Template.Constants;
    using Dax.Template.Exceptions;
    using Dax.Template.Tables.CalculationGroups;
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using Xunit;

    /// <summary>
    /// Offline regression tests for the CalculationGroup (Phase 2) feature: build a synthetic
    /// compatibility-1605 in-memory model via <see cref="CalcGroupOfflineModelFixture"/>, run the real
    /// <see cref="Engine.ApplyTemplates"/> dispatch for the <c>CalculationGroupTemplate</c> class, and
    /// assert both the resulting TOM <see cref="CalculationGroup"/> shape and a golden-file snapshot.
    /// Compatibility level 1605 (rather than the shared <see cref="OfflineModelFixture"/>'s 1600) is
    /// required because the standard config's sub-template sets both
    /// <see cref="CalculationGroup.MultipleOrEmptySelectionExpression"/> and
    /// <see cref="CalculationGroup.NoSelectionExpression"/>, which TOM enforces at assignment time — see
    /// <see cref="CalcGroupOfflineModelFixture"/>'s remarks. These guard
    /// <see cref="Tables.CalculationGroups.CalculationGroupTemplate"/> and its
    /// <c>Engine.ApplyCalculationGroupTemplate</c> dispatch so that later phases (UDFs) cannot silently
    /// change current CalculationGroup behavior. A parallel opt-in live-server test exercises the same
    /// path against a real engine.
    /// </summary>
    public class CalculationGroupGoldenTests
    {
        private const string CalcGroupTemplatePath = @".\_data\Templates\Config-03 - CalculationGroup.template.json";
        private const string CalcGroupDisabledTemplatePath = @".\_data\Templates\Config-03b - CalculationGroup-Disabled.template.json";

        [Fact]
        public void ApplyTemplates_CalculationGroupStandardConfig_CreatesTimeIntelligenceCalculationGroup()
        {
            // Arrange
            var database = CalcGroupOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(CalcGroupTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var table = database.Model.Tables.Find("Time Intelligence");
            Assert.NotNull(table);
            Assert.False(table!.IsHidden);
            Assert.Contains(table.Annotations, a => a.Name == Attributes.SqlbiTemplate && a.Value == Attributes.SqlbiTemplateTableCalculationGroup);

            var column = Assert.IsType<DataColumn>(table.Columns.Find("Time Intelligence"));
            Assert.Equal(DataType.String, column.DataType);

            var partition = Assert.Single(table.Partitions);
            Assert.IsType<CalculationGroupSource>(partition.Source);

            var calculationGroup = table.CalculationGroup;
            Assert.NotNull(calculationGroup);
            Assert.Equal(10, calculationGroup!.Precedence);
            Assert.Equal("Time intelligence calculation group", calculationGroup.Description);
            Assert.Equal(3, calculationGroup.CalculationItems.Count);

            var current = calculationGroup.CalculationItems.Find("Current");
            Assert.NotNull(current);
            Assert.Equal("SELECTEDMEASURE()", current!.Expression);
            Assert.Equal(0, current.Ordinal);
            Assert.Null(current.FormatStringDefinition);

            var mtd = calculationGroup.CalculationItems.Find("MTD");
            Assert.NotNull(mtd);
            Assert.Equal("CALCULATE ( SELECTEDMEASURE(), DATESMTD ( 'Date'[Date] ) )", mtd!.Expression);
            Assert.Equal(5, mtd.Ordinal);
            Assert.NotNull(mtd.FormatStringDefinition);
            Assert.Equal("SELECTEDMEASUREFORMATSTRING()", mtd.FormatStringDefinition!.Expression);

            var pctVsCurrent = calculationGroup.CalculationItems.Find("% vs Current");
            Assert.NotNull(pctVsCurrent);
            Assert.Equal(
                "\r\nDIVIDE (\r\n    SELECTEDMEASURE(),\r\n    CALCULATE ( SELECTEDMEASURE(), CALCULATIONGROUPRESETSELECTION() )\r\n)",
                pctVsCurrent!.Expression);
            Assert.Equal(2, pctVsCurrent.Ordinal);
            Assert.NotNull(pctVsCurrent.FormatStringDefinition);
            Assert.Equal("\"0.0%\"", pctVsCurrent.FormatStringDefinition!.Expression);

            Assert.NotNull(calculationGroup.MultipleOrEmptySelectionExpression);
            Assert.Equal("SELECTEDMEASURE()", calculationGroup.MultipleOrEmptySelectionExpression!.Expression);
            Assert.NotNull(calculationGroup.NoSelectionExpression);
            Assert.Equal("SELECTEDMEASURE()", calculationGroup.NoSelectionExpression!.Expression);
        }

        [Fact]
        public void ApplyTemplates_CalculationGroupStandardConfig_MatchesSnapshot()
        {
            // Arrange
            var database = CalcGroupOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(CalcGroupTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var actual = GoldenFile.SerializeNormalized(database);
            GoldenFile.AssertMatchesSnapshot(actual, "Config-03 - CalculationGroup");
        }

        [Fact]
        public void ApplyTemplates_CalculationGroupStandardConfigAppliedTwice_ProducesIdenticalNormalizedOutput()
        {
            // Arrange
            var database = CalcGroupOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(CalcGroupTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);
            var firstRun = GoldenFile.SerializeNormalized(database);

            engine.ApplyTemplates(database.Model);
            var secondRun = GoldenFile.SerializeNormalized(database);

            // Assert: re-running the same template against its own prior output is stable.
            Assert.Equal(firstRun, secondRun);
        }

        [Fact]
        public void ApplyTemplates_CalculationGroupTemplateDisabledAfterEnabled_RemovesCalculationGroupTable()
        {
            // Arrange: apply the enabled CalculationGroup config once, so "Time Intelligence" exists.
            var database = CalcGroupOfflineModelFixture.Build();
            var engineEnabled = new Engine(Package.LoadFromFile(CalcGroupTemplatePath));
            engineEnabled.ApplyTemplates(database.Model);

            Assert.NotNull(database.Model.Tables.Find("Time Intelligence")); // sanity check on the initial state

            // Act: re-apply a config whose CalculationGroupTemplate entry is disabled for the same table.
            var engineDisabled = new Engine(Package.LoadFromFile(CalcGroupDisabledTemplatePath));
            engineDisabled.ApplyTemplates(database.Model);

            // Assert: the calculation-group table is removed.
            Assert.Null(database.Model.Tables.Find("Time Intelligence"));
        }

        [Fact]
        public void ApplyTemplates_CalculationGroupTemplateDisabledWithMissingTargetTable_DoesNotThrow()
        {
            // Arrange: a fresh fixture has no "Time Intelligence" table, and the disabled config's
            // CalculationGroupTemplate entry targets it — simulating a prior entry in the same run having
            // already removed it.
            var database = CalcGroupOfflineModelFixture.Build();
            Assert.Null(database.Model.Tables.Find("Time Intelligence")); // sanity check on the initial state
            var engineDisabled = new Engine(Package.LoadFromFile(CalcGroupDisabledTemplatePath));

            // Act
            var exception = Record.Exception(() => engineDisabled.ApplyTemplates(database.Model));

            // Assert: disabled + already-missing target table is a safe no-op, matching every sibling
            // handler (HolidaysDefinitionTable, HolidaysTable, CustomDateTable, CalendarTemplate).
            Assert.Null(exception);
            Assert.Null(database.Model.Tables.Find("Time Intelligence"));
        }

        [Fact]
        public void ApplyTemplates_CalculationGroupTargetsForeignTable_ThrowsTemplateException()
        {
            // Arrange: pre-create a "Time Intelligence" table this template did not create (no ownership
            // annotation), then apply a config that targets the same table name.
            var database = CalcGroupOfflineModelFixture.Build();
            var foreignTable = new Table { Name = "Time Intelligence" };
            foreignTable.Columns.Add(new DataColumn { Name = "Foreign Column", DataType = DataType.String, SourceColumn = "Foreign Column" });
            foreignTable.Partitions.Add(new Partition
            {
                Name = "Time Intelligence",
                Source = new CalculatedPartitionSource { Expression = "{\"x\"}" }
            });
            database.Model.Tables.Add(foreignTable);

            var engine = new Engine(Package.LoadFromFile(CalcGroupTemplatePath));

            // Act
            var exception = Assert.Throws<TemplateException>(() => engine.ApplyTemplates(database.Model));

            // Assert: the guard fires before any mutation, so the foreign table is left completely unchanged.
            Assert.Contains("Time Intelligence", exception.Message);
            var stillForeign = database.Model.Tables.Find("Time Intelligence");
            Assert.NotNull(stillForeign);
            Assert.Single(stillForeign!.Columns);
            Assert.Equal("Foreign Column", stillForeign.Columns[0].Name);
            Assert.Null(stillForeign.CalculationGroup);
        }

        [Fact]
        public void ApplyTemplate_CalculationItemsWithDuplicateEffectiveOrdinal_ThrowsInvalidConfigurationException()
        {
            // Arrange: "Current" has no explicit Ordinal (effective ordinal = its array index, 0); "Also
            // Current" explicitly claims Ordinal 0 too, so the effective ordinals collide.
            var database = CalcGroupOfflineModelFixture.Build();
            var table = new Table { Name = "Duplicate Ordinal" };
            database.Model.Tables.Add(table);

            var definition = new CalculationGroupTemplateDefinition
            {
                Precedence = 1,
                ColumnName = "Duplicate Ordinal",
                CalculationItems =
                [
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition
                    {
                        Name = "Current",
                        Expression = "SELECTEDMEASURE()"
                    },
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition
                    {
                        Name = "Also Current",
                        Ordinal = 0,
                        Expression = "SELECTEDMEASURE()"
                    }
                ]
            };

            // Act
            var exception = Assert.Throws<InvalidConfigurationException>(
                () => new CalculationGroupTemplate(definition).ApplyTemplate(table, isHidden: false));

            // Assert
            Assert.Contains("Duplicate effective Ordinal", exception.Message);
        }

        [Fact]
        public void ApplyTemplate_ReappliedWithCalculationItemRemovedFromDefinition_RemovesOrphanedCalculationItem()
        {
            // Arrange: first apply produces calculation items {A, B, C}.
            var database = CalcGroupOfflineModelFixture.Build();
            var table = new Table { Name = "Orphan Removal" };
            database.Model.Tables.Add(table);

            var definitionWithThreeItems = new CalculationGroupTemplateDefinition
            {
                Precedence = 1,
                ColumnName = "Orphan Removal",
                CalculationItems =
                [
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "A", Expression = "SELECTEDMEASURE()" },
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "B", Expression = "SELECTEDMEASURE()" },
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "C", Expression = "SELECTEDMEASURE()" }
                ]
            };
            new CalculationGroupTemplate(definitionWithThreeItems).ApplyTemplate(table, isHidden: false);

            var calculationGroup = table.CalculationGroup;
            Assert.NotNull(calculationGroup);
            Assert.Equal(3, calculationGroup!.CalculationItems.Count); // sanity check on the initial state

            // Act: re-apply a definition whose CalculationItems dropped "B".
            var definitionWithTwoItems = new CalculationGroupTemplateDefinition
            {
                Precedence = 1,
                ColumnName = "Orphan Removal",
                CalculationItems =
                [
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "A", Expression = "SELECTEDMEASURE()" },
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "C", Expression = "SELECTEDMEASURE()" }
                ]
            };
            new CalculationGroupTemplate(definitionWithTwoItems).ApplyTemplate(table, isHidden: false);

            // Assert: "B" was removed as an orphan; "A" and "C" remain.
            Assert.Equal(2, calculationGroup.CalculationItems.Count);
            Assert.NotNull(calculationGroup.CalculationItems.Find("A"));
            Assert.Null(calculationGroup.CalculationItems.Find("B"));
            Assert.NotNull(calculationGroup.CalculationItems.Find("C"));
        }

        [Fact]
        public void ApplyTemplate_ReappliedWithEmptySelectionExpressions_ClearsPreviouslySetSelectionExpressions()
        {
            // Arrange: first apply sets both selection expressions to non-empty DAX. Compatibility level
            // 1605 (see CalcGroupOfflineModelFixture's remarks) is required for TOM to accept a non-null
            // MultipleOrEmptySelectionExpression / NoSelectionExpression, so the table must already be
            // attached to the fixture's Model before the first ApplyTemplate call.
            var database = CalcGroupOfflineModelFixture.Build();
            var table = new Table { Name = "Selection Expression Clearing" };
            database.Model.Tables.Add(table);

            var definitionWithSelectionExpressions = new CalculationGroupTemplateDefinition
            {
                Precedence = 1,
                ColumnName = "Selection Expression Clearing",
                CalculationItems =
                [
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "Current", Expression = "SELECTEDMEASURE()" }
                ],
                MultipleOrEmptySelectionExpression = "SELECTEDMEASURE()",
                NoSelectionExpression = "SELECTEDMEASURE()"
            };
            new CalculationGroupTemplate(definitionWithSelectionExpressions).ApplyTemplate(table, isHidden: false);

            var calculationGroup = table.CalculationGroup;
            Assert.NotNull(calculationGroup);
            Assert.NotNull(calculationGroup!.MultipleOrEmptySelectionExpression); // sanity check on the initial state
            Assert.NotNull(calculationGroup.NoSelectionExpression); // sanity check on the initial state

            // Act: re-apply a definition that omits both selection expressions.
            var definitionWithoutSelectionExpressions = new CalculationGroupTemplateDefinition
            {
                Precedence = 1,
                ColumnName = "Selection Expression Clearing",
                CalculationItems =
                [
                    new CalculationGroupTemplateDefinition.CalculationItemDefinition { Name = "Current", Expression = "SELECTEDMEASURE()" }
                ]
            };
            new CalculationGroupTemplate(definitionWithoutSelectionExpressions).ApplyTemplate(table, isHidden: false);

            // Assert: both previously-set selection expressions are cleared.
            Assert.Null(calculationGroup.MultipleOrEmptySelectionExpression);
            Assert.Null(calculationGroup.NoSelectionExpression);
        }

        /// <summary>
        /// Opt-in: applies the CalculationGroup config against a real connected model (env-configured) and
        /// verifies the engine produces model changes without persisting them. Skipped unless live-server
        /// env vars are set.
        /// </summary>
        [LiveServerFact]
        public void CalculationGroupStandardConfig_LiveServerApply_ProducesModelChanges()
        {
            var serverConn = Environment.GetEnvironmentVariable(LiveServerFactAttribute.ServerEnvVar)!;
            var databaseName = Environment.GetEnvironmentVariable(LiveServerFactAttribute.DatabaseEnvVar)!;

            using var server = new Server();
            server.Connect(serverConn);
            try
            {
                var database = server.Databases[databaseName];
                var engine = new Engine(Package.LoadFromFile(CalcGroupTemplatePath));

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