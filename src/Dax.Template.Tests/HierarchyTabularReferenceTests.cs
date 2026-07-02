namespace Dax.Template.Tests
{
    using Dax.Template.Model;
    using Dax.Template.Tables;
    using Microsoft.AnalysisServices.Tabular;
    using System.Linq;
    using System.Threading;
    using Xunit;
    using Column = Dax.Template.Model.Column;
    using Hierarchy = Dax.Template.Model.Hierarchy;
    using Level = Dax.Template.Model.Level;

    /// <summary>
    /// Characterization tests for <see cref="TableTemplateBase.AddHierarchies"/> that observe the internal
    /// "Tabular*" back-reference contract (<see cref="Level.TabularLevel"/> and
    /// <see cref="Hierarchy.TabularHierarchy"/>), which is NOT exposed by the serialized BIM output that the
    /// golden-file tests snapshot. These tests exercise the real production code path (via the public
    /// <see cref="TableTemplateBase.ApplyTemplate(Table, bool, CancellationToken)"/> entry point) against a
    /// minimal, table-agnostic subclass, so they are independent of any JSON template configuration.
    ///
    /// Reachability: <see cref="Level.TabularLevel"/>, <see cref="Hierarchy.TabularHierarchy"/> and
    /// <see cref="Column.TabularColumn"/> are all `internal` and are made visible to this assembly via
    /// [InternalsVisibleTo("Dax.Template.Tests")] declared in src/Dax.Template/AssemblyInfo.cs.
    ///
    /// These tests act as regression guards for the internal "Tabular*" back-reference contract established
    /// by AddHierarchies: level.TabularLevel and hierarchy.TabularHierarchy must reference the same
    /// Microsoft.AnalysisServices.Tabular.Level / Hierarchy instances that are actually added to the model.
    /// </summary>
    public class HierarchyTabularReferenceTests
    {
        /// <summary>
        /// Minimal, non-abstract TableTemplateBase used purely to exercise AddHierarchies (via the public
        /// ApplyTemplate entry point) without any JSON template / DAX-generation machinery.
        /// </summary>
        private class MinimalHierarchyTemplate : TableTemplateBase
        {
            protected override bool RemoveExistingPartitions(Table dateTable) => false;
            protected override void AddPartitions(Table dateTable, CancellationToken cancellationToken = default) { }
        }

        private static (MinimalHierarchyTemplate template, Table table, Hierarchy hierarchy, Level[] levels) BuildTemplateWithHierarchy()
        {
            var database = new Database
            {
                Name = "HierarchyRefFixture",
                ID = "HierarchyRefFixture",
                CompatibilityLevel = 1600,
                StorageEngineUsed = Microsoft.AnalysisServices.StorageEngineUsed.TabularMetadata,
            };
            database.Model = new Model();

            var table = new Table { Name = "TestTable" };
            database.Model.Tables.Add(table);

            var yearColumn = new Column { Name = "Year", DataType = DataType.Int64 };
            var monthColumn = new Column { Name = "Month", DataType = DataType.Int64 };
            var dateColumn = new Column { Name = "Date", DataType = DataType.DateTime };

            var template = new MinimalHierarchyTemplate();
            template.Columns.Add(yearColumn);
            template.Columns.Add(monthColumn);
            template.Columns.Add(dateColumn);

            var levels = new[]
            {
                new Level { Name = "Year", Column = yearColumn },
                new Level { Name = "Month", Column = monthColumn },
                new Level { Name = "Date", Column = dateColumn },
            };
            var hierarchy = new Hierarchy { Name = "Calendar", Levels = levels };
            template.Hierarchies.Add(hierarchy);

            return (template, table, hierarchy, levels);
        }

        /// <summary>
        /// Regression guard: level.TabularLevel must reference the same Microsoft.AnalysisServices.Tabular.Level
        /// instance that was actually added to the hierarchy in the model (tabularHierarchy.Levels).
        /// </summary>
        [Fact]
        public void AddHierarchies_LevelTabularLevel_ReferencesLevelActuallyInModel()
        {
            var (template, table, _, levels) = BuildTemplateWithHierarchy();

            template.ApplyTemplate(table, hideTable: false);

            var tabularHierarchy = table.Hierarchies.Find("Calendar");
            Assert.NotNull(tabularHierarchy);

            for (int i = 0; i < levels.Length; i++)
            {
                var modelLevel = tabularHierarchy!.Levels.FirstOrDefault(l => l.Name == levels[i].Name);
                Assert.NotNull(modelLevel);
                Assert.Same(modelLevel, levels[i].TabularLevel);
            }
        }

        /// <summary>
        /// Regression guard: hierarchy.TabularHierarchy must reference the Microsoft.AnalysisServices.Tabular.Hierarchy
        /// instance actually added to the table.
        /// </summary>
        [Fact]
        public void AddHierarchies_HierarchyTabularHierarchy_ReferencesHierarchyActuallyInModel()
        {
            var (template, table, hierarchy, _) = BuildTemplateWithHierarchy();

            template.ApplyTemplate(table, hideTable: false);

            var tabularHierarchy = table.Hierarchies.Find("Calendar");
            Assert.NotNull(tabularHierarchy);
            Assert.Same(tabularHierarchy, hierarchy.TabularHierarchy);
        }

        /// <summary>
        /// Characterizes correct behavior: levels are added to the model hierarchy in declaration order with a
        /// 0-based, sequential Ordinal.
        /// </summary>
        [Fact]
        public void AddHierarchies_LevelOrdinal_MatchesDeclarationOrder()
        {
            var (template, table, _, levels) = BuildTemplateWithHierarchy();

            template.ApplyTemplate(table, hideTable: false);

            var tabularHierarchy = table.Hierarchies.Find("Calendar");
            Assert.NotNull(tabularHierarchy);

            for (int i = 0; i < levels.Length; i++)
            {
                var modelLevel = tabularHierarchy!.Levels.FirstOrDefault(l => l.Name == levels[i].Name);
                Assert.NotNull(modelLevel);
                Assert.Equal(i, modelLevel!.Ordinal);
            }
        }

        /// <summary>
        /// Characterizes correct behavior: each level in the model hierarchy is bound to the same
        /// Microsoft.AnalysisServices.Tabular.Column instance as the Column.TabularColumn of the Level's
        /// declared Column.
        /// </summary>
        [Fact]
        public void AddHierarchies_LevelColumn_MapsToDeclaredColumnTabularColumn()
        {
            var (template, table, _, levels) = BuildTemplateWithHierarchy();

            template.ApplyTemplate(table, hideTable: false);

            var tabularHierarchy = table.Hierarchies.Find("Calendar");
            Assert.NotNull(tabularHierarchy);

            for (int i = 0; i < levels.Length; i++)
            {
                var modelLevel = tabularHierarchy!.Levels.FirstOrDefault(l => l.Name == levels[i].Name);
                Assert.NotNull(modelLevel);
                Assert.Same(levels[i].Column.TabularColumn, modelLevel!.Column);
            }
        }

        /// <summary>
        /// Level.Reset() must clear the internal TabularLevel back-reference so a subsequent re-application of
        /// the template cannot observe a stale Tabular object from a previous run.
        /// </summary>
        [Fact]
        public void Level_Reset_ClearsTabularLevelReference()
        {
            var column = new Column { Name = "Year", DataType = DataType.Int64 };
            var level = new Level { Name = "Year", Column = column };
            level.TabularLevel = new Microsoft.AnalysisServices.Tabular.Level { Name = "Year" };

            Assert.NotNull(level.TabularLevel);

            level.Reset();

            Assert.Null(level.TabularLevel);
        }

        /// <summary>
        /// Hierarchy.Reset() must clear the internal TabularHierarchy back-reference so a subsequent
        /// re-application of the template cannot observe a stale Tabular object from a previous run.
        /// </summary>
        [Fact]
        public void Hierarchy_Reset_ClearsTabularHierarchyReference()
        {
            var hierarchy = new Hierarchy { Name = "Calendar", Levels = System.Array.Empty<Level>() };
            hierarchy.TabularHierarchy = new Microsoft.AnalysisServices.Tabular.Hierarchy { Name = "Calendar" };

            Assert.NotNull(hierarchy.TabularHierarchy);

            hierarchy.Reset();

            Assert.Null(hierarchy.TabularHierarchy);
        }
    }
}