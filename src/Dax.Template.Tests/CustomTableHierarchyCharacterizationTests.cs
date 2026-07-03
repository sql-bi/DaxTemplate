namespace Dax.Template.Tests
{
    using Dax.Template.Exceptions;
    using Dax.Template.Tables;
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using Xunit;

    /// <summary>
    /// Characterization tests for <see cref="CustomTableTemplate{T}"/>'s hierarchy construction on the
    /// NON-date path (levels/ordinals/column binding), i.e. the private `GetHierarchies` step invoked from
    /// `InitTemplate` during construction, followed by the shared `TableTemplateBase.AddHierarchies` step
    /// invoked from `ApplyTemplate`.
    ///
    /// Reachability: there is no generic non-date custom-table `Class` wired into
    /// <see cref="Engine.ApplyTemplates"/> (see .claude/SESSION_HANDOFF.md Phase M Stage 0, P1 findings),
    /// so this drives <c>CustomTableTemplate&lt;TemplateConfiguration&gt;</c> directly at the unit level --
    /// <see cref="TemplateConfiguration"/> is a convenient, already-public <c>ICustomTableConfig</c>
    /// implementation, used purely as a data holder here (no JSON template file involved).
    /// </summary>
    public class CustomTableHierarchyCharacterizationTests
    {
        private static CustomTemplateDefinition BuildDefinition(params CustomTemplateDefinition.Hierarchy[] hierarchies)
        {
            return new CustomTemplateDefinition
            {
                Columns = new[]
                {
                    new CustomTemplateDefinition.Column { Name = "Year", DataType = "Int64", Expression = "1" },
                    new CustomTemplateDefinition.Column { Name = "Month", DataType = "Int64", Expression = "1" },
                },
                Hierarchies = hierarchies,
            };
        }

        private static Table BuildDisconnectedTable()
        {
            var database = new Database
            {
                Name = "CustomTableHierarchyFixture",
                ID = "CustomTableHierarchyFixture",
                CompatibilityLevel = OfflineModelFixture.CompatibilityLevel,
                StorageEngineUsed = Microsoft.AnalysisServices.StorageEngineUsed.TabularMetadata,
            };
            database.Model = new Model();

            var table = new Table { Name = "CustomHierarchyTable" };
            database.Model.Tables.Add(table);
            return table;
        }

        [Fact]
        public void ApplyTemplate_ValidHierarchyLevels_WiresLevelsInDeclarationOrderWithColumnBinding()
        {
            // Arrange
            var definition = BuildDefinition(new CustomTemplateDefinition.Hierarchy
            {
                Name = "Calendar",
                Description = "Calendar hierarchy",
                Levels = new[]
                {
                    new CustomTemplateDefinition.HierarchyLevel { Name = "Year", Column = "Year" },
                    new CustomTemplateDefinition.HierarchyLevel { Name = "Month", Column = "Month", Description = "Month level" },
                }
            });
            var table = BuildDisconnectedTable();
            var template = new CustomTableTemplate<TemplateConfiguration>(new TemplateConfiguration(), definition, model: null);

            // Act
            template.ApplyTemplate(table, hideTable: false);

            // Assert: the hierarchy and its levels are created in declaration order with 0-based ordinals,
            // and each level's Column is bound to the actual TOM column added for that name.
            var tabularHierarchy = table.Hierarchies.Find("Calendar");
            Assert.NotNull(tabularHierarchy);
            Assert.Equal(2, tabularHierarchy!.Levels.Count);

            Assert.Equal("Year", tabularHierarchy.Levels[0].Name);
            Assert.Equal(0, tabularHierarchy.Levels[0].Ordinal);
            Assert.Same(table.Columns.Find("Year"), tabularHierarchy.Levels[0].Column);

            Assert.Equal("Month", tabularHierarchy.Levels[1].Name);
            Assert.Equal(1, tabularHierarchy.Levels[1].Ordinal);
            Assert.Same(table.Columns.Find("Month"), tabularHierarchy.Levels[1].Column);
        }

        [Fact]
        public void ApplyTemplate_HierarchyAndLevelDescriptions_AreNotCopiedToTheTabularObjects()
        {
            // Arrange: current-behavior surprise -- GetHierarchies DOES capture Description from the JSON
            // definition onto the internal Dax.Template.Model.Hierarchy/Level objects, but
            // TableTemplateBase.AddHierarchies never copies hierarchy.Description or level.Description onto
            // the Microsoft.AnalysisServices.Tabular.Hierarchy/Level it creates -- only Name, IsHidden,
            // DisplayFolder (hierarchy) and Name, Column, Ordinal (level) are set. Configured descriptions
            // are silently dropped from the generated model.
            var definition = BuildDefinition(new CustomTemplateDefinition.Hierarchy
            {
                Name = "Calendar",
                Description = "Calendar hierarchy",
                Levels = new[]
                {
                    new CustomTemplateDefinition.HierarchyLevel { Name = "Year", Column = "Year", Description = "Year level" },
                }
            });
            var table = BuildDisconnectedTable();
            var template = new CustomTableTemplate<TemplateConfiguration>(new TemplateConfiguration(), definition, model: null);

            // Act
            template.ApplyTemplate(table, hideTable: false);

            // Assert
            var tabularHierarchy = table.Hierarchies.Find("Calendar");
            Assert.NotNull(tabularHierarchy);
            Assert.True(string.IsNullOrEmpty(tabularHierarchy!.Description));
            Assert.True(string.IsNullOrEmpty(tabularHierarchy.Levels[0].Description));
        }

        [Fact]
        public void Construction_HierarchyWithMissingName_ThrowsTemplateException()
        {
            // Arrange
            var definition = BuildDefinition(new CustomTemplateDefinition.Hierarchy
            {
                Name = null,
                Levels = new[] { new CustomTemplateDefinition.HierarchyLevel { Name = "Year", Column = "Year" } }
            });

            // Act & Assert: GetHierarchies runs synchronously from InitTemplate during construction.
            Assert.Throws<TemplateException>(() => new CustomTableTemplate<TemplateConfiguration>(new TemplateConfiguration(), definition, model: null));
        }

        [Fact]
        public void Construction_HierarchyLevelWithMissingName_ThrowsTemplateException()
        {
            // Arrange
            var definition = BuildDefinition(new CustomTemplateDefinition.Hierarchy
            {
                Name = "Calendar",
                Levels = new[] { new CustomTemplateDefinition.HierarchyLevel { Name = null, Column = "Year" } }
            });

            // Act & Assert
            Assert.Throws<TemplateException>(() => new CustomTableTemplate<TemplateConfiguration>(new TemplateConfiguration(), definition, model: null));
        }

        [Fact]
        public void Construction_HierarchyLevelWithMissingColumn_ThrowsTemplateException()
        {
            // Arrange
            var definition = BuildDefinition(new CustomTemplateDefinition.Hierarchy
            {
                Name = "Calendar",
                Levels = new[] { new CustomTemplateDefinition.HierarchyLevel { Name = "Year", Column = null } }
            });

            // Act & Assert
            Assert.Throws<TemplateException>(() => new CustomTableTemplate<TemplateConfiguration>(new TemplateConfiguration(), definition, model: null));
        }

        [Fact]
        public void Construction_HierarchyLevelReferencesUnknownColumn_ThrowsTemplateException()
        {
            // Arrange: fixed behavior -- GetHierarchies resolves a level's Column via
            // `Columns.FirstOrDefault(column => column.Name == level.Column) ?? throw new TemplateException(...)`.
            // An unmatched name now throws a Dax.Template-specific TemplateException naming the offending
            // column, level, and hierarchy, instead of the generic BCL InvalidOperationException.
            var definition = BuildDefinition(new CustomTemplateDefinition.Hierarchy
            {
                Name = "Calendar",
                Levels = new[] { new CustomTemplateDefinition.HierarchyLevel { Name = "Year", Column = "NoSuchColumn" } }
            });

            // Act & Assert
            var ex = Assert.Throws<TemplateException>(() => new CustomTableTemplate<TemplateConfiguration>(new TemplateConfiguration(), definition, model: null));
            Assert.Contains("NoSuchColumn", ex.Message);
            Assert.Contains("Calendar", ex.Message);
            Assert.Contains("Year", ex.Message);
        }
    }
}