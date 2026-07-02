namespace Dax.Template.Tests
{
    using Dax.Template.Tests.Infrastructure;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Golden-file coverage for template configuration shapes not exercised by Config-01 - Standard:
    /// a measures-only config (no date tables) and a holidays-only config (no CustomDateTable or
    /// MeasuresTemplate). Each has its own committed snapshot under _data/Golden, generated the same way
    /// as <see cref="ApplyTemplatesGoldenTests"/>. New JSON configs under _data/Templates are additive;
    /// the existing "Config-01 - Standard" config and golden are untouched.
    /// </summary>
    public class BroadenedGoldenCoverageTests
    {
        [Fact]
        public void MeasuresOnlyConfig_OfflineApply_MatchesSnapshot()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(@".\_data\Templates\Config-02 - MeasuresOnly.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var actual = GoldenFile.SerializeNormalized(database);
            GoldenFile.AssertMatchesSnapshot(actual, "Config-02 - MeasuresOnly");
        }

        [Fact]
        public void MeasuresOnlyConfig_OfflineApply_AddsWrapperMeasuresWithoutAnyDateTable()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(@".\_data\Templates\Config-02 - MeasuresOnly.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert: no date table is created by a MeasuresTemplate-only config.
            var tableNames = database.Model.Tables.Select(t => t.Name).ToArray();
            Assert.DoesNotContain("Date", tableNames);
            Assert.DoesNotContain("DateAutoTemplate", tableNames);

            var sales = database.Model.Tables.Find("Sales")!;
            Assert.Contains(sales.Measures, m => m.Name == "Sales Amount Rounded");
            Assert.Contains(sales.Measures, m => m.Name == "Total Cost Abs");
        }

        [Fact]
        public void HolidaysOnlyConfig_OfflineApply_MatchesSnapshot()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(@".\_data\Templates\Config-03 - HolidaysOnly.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var actual = GoldenFile.SerializeNormalized(database);
            GoldenFile.AssertMatchesSnapshot(actual, "Config-03 - HolidaysOnly");
        }

        [Fact]
        public void HolidaysOnlyConfig_OfflineApply_AddsHolidaysTablesWithoutDateOrMeasuresTemplates()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(@".\_data\Templates\Config-03 - HolidaysOnly.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var tableNames = database.Model.Tables.Select(t => t.Name).ToArray();
            Assert.Contains("Holidays", tableNames);
            Assert.Contains("HolidaysDefinition", tableNames);
            Assert.DoesNotContain("Date", tableNames);

            var sales = database.Model.Tables.Find("Sales")!;
            Assert.Equal(4, sales.Measures.Count); // unchanged from OfflineModelFixture.Build(): no MeasuresTemplate applied
        }
    }
}