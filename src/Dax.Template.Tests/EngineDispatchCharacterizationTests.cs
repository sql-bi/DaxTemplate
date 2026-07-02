namespace Dax.Template.Tests
{
    using System;
    using Dax.Template.Exceptions;
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using Xunit;

    /// <summary>
    /// Characterization tests pinning the CURRENT dispatch behavior of <see cref="Engine.ApplyTemplates"/>:
    /// how an unrecognized template <c>Class</c>, missing required per-class configuration, and
    /// <c>IsEnabled = false</c> entries are handled today. These exist as a safety net before any
    /// refactor of the dispatch mechanism (see .claude/SESSION_HANDOFF.md Phase M Stage 0).
    /// </summary>
    public class EngineDispatchCharacterizationTests
    {
        private const string TemplatesDirectory = @".\_data\Templates";

        [Fact]
        public void ApplyTemplates_UnknownClass_ThrowsInvalidOperationException()
        {
            // Arrange: current behavior -- the LINQ `.First(predicate)` dispatch lookup in
            // Engine.ApplyTemplates throws InvalidOperationException ("Sequence contains no matching
            // element") when the configured Class does not match any of the four known handlers.
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-01 - UnknownClass.template.json"));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => engine.ApplyTemplates(database.Model));
        }

        [Fact]
        public void ApplyTemplates_CustomDateTableWithEmptyTemplate_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-02 - CustomDateTable-EmptyTemplate.template.json"));

            // Act & Assert
            Assert.Throws<InvalidConfigurationException>(() => engine.ApplyTemplates(database.Model));
        }

        [Fact]
        public void ApplyTemplates_CustomDateTableWithEmptyTable_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-03 - CustomDateTable-EmptyTable.template.json"));

            // Act & Assert
            Assert.Throws<InvalidConfigurationException>(() => engine.ApplyTemplates(database.Model));
        }

        [Fact]
        public void ApplyTemplates_MeasuresTemplateWithEmptyTemplate_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-04 - MeasuresTemplate-EmptyTemplate.template.json"));

            // Act & Assert
            Assert.Throws<InvalidConfigurationException>(() => engine.ApplyTemplates(database.Model));
        }

        [Fact]
        public void ApplyTemplates_HolidaysDefinitionTableWithEmptyTemplate_ThrowsInvalidConfigurationException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-05 - HolidaysDefinitionTable-EmptyTemplate.template.json"));

            // Act & Assert
            Assert.Throws<InvalidConfigurationException>(() => engine.ApplyTemplates(database.Model));
        }

        [Fact]
        public void ApplyTemplates_HolidaysTableDisabled_RemovesExistingTableAndDisablesHolidaysReference()
        {
            // Arrange: a pre-existing "Holidays" table simulates a previous run's output.
            var database = OfflineModelFixture.Build();
            database.Model.Tables.Add(new Table { Name = "Holidays" });
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-06 - HolidaysTable-Disabled.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert: current behavior actively removes the pre-existing table and flips the shared
            // HolidaysReference.IsEnabled flag off, unlike a disabled CustomDateTable (see next test).
            Assert.Null(database.Model.Tables.Find("Holidays"));
            Assert.False(engine.Configuration.HolidaysReference!.IsEnabled);
        }

        [Fact]
        public void ApplyTemplates_CustomDateTableDisabled_DoesNotCreateTable()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-07 - CustomDateTable-Disabled.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert: unlike Holidays* classes, a disabled CustomDateTable entry returns early without
            // creating anything -- Table/Template must still be non-empty to pass the earlier guard
            // checks, but IsEnabled=false itself is a pure no-op for CustomDateTable.
            Assert.Null(database.Model.Tables.Find("Date"));
        }

        [Fact]
        public void ApplyTemplates_CustomDateTableDisabled_DoesNotRemovePreExistingTable()
        {
            // Arrange: a pre-existing "Date" table simulates a previous run's output.
            var database = OfflineModelFixture.Build();
            var preExistingDateTable = new Table { Name = "Date" };
            preExistingDateTable.Columns.Add(new DataColumn { Name = "Marker", DataType = DataType.String, SourceColumn = "Marker" });
            database.Model.Tables.Add(preExistingDateTable);
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-07 - CustomDateTable-Disabled.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert: the pre-existing table survives untouched -- CustomDateTable's disabled path never
            // looks it up, unlike HolidaysTable/HolidaysDefinitionTable which actively remove it.
            var dateTable = database.Model.Tables.Find("Date");
            Assert.NotNull(dateTable);
            Assert.Contains(dateTable!.Columns, c => c.Name == "Marker");
        }
    }
}
