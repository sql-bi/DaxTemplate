namespace Dax.Template.Tests
{
    using Dax.Template.Exceptions;
    using Dax.Template.Tests.Infrastructure;
    using Xunit;

    /// <summary>
    /// Characterization tests for two code-reviewer follow-ups identified while auditing Phase M Stage 0
    /// P1/P2 work (see .claude/SESSION_HANDOFF.md):
    ///
    /// A. A date/measures config that uses the `@@GETMINDATE`/`@@GETMAXDATE` macros but omits `AutoScan`
    ///    entirely throws <see cref="InvalidMacroReferenceException"/>, because
    ///    <c>Engine.ApplyConfigurationDefaults</c> has no default for `AutoScan` (unlike every other
    ///    IScanConfig/IHolidaysConfig/IMeasureTemplateConfig property, which all get a `??=` default).
    ///
    /// B. (FIXED -- Phase M Stage 3, Group B1) <c>Engine.ApplyHolidaysDefinitionTable</c> used to create the
    ///    target Table and ADD it to <c>model.Tables</c> BEFORE validating <c>templateEntry.Template</c> and
    ///    throwing <see cref="InvalidConfigurationException"/>, leaving a phantom half-created table behind.
    ///    The validation now runs before the create/add, matching <c>ApplyCustomDateTable</c> and
    ///    <c>ApplyMeasuresTemplate</c>, which validate first and never touch the model on that failure path.
    /// </summary>
    public class ReviewerFollowUpCharacterizationTests
    {
        private const string TemplatesDirectory = @".\_data\Templates";

        [Fact]
        public void ApplyTemplates_DateMacrosUsedWithAutoScanOmitted_ThrowsInvalidMacroReferenceException()
        {
            // Arrange: AutoScanOmitted-01.template.json has no top-level "AutoScan" property at all, and
            // Engine.ApplyConfigurationDefaults never defaults IScanConfig.AutoScan, so it stays null.
            // The single-instance measure's expression references @@GETMINDATE()/@@GETMAXDATE(), which
            // MeasuresTemplate.ReplaceMacros resolves via model.GetScanColumns(Config) -- with AutoScan
            // null, GetScanColumns returns null, and since null != AutoScan.Disabled, ReplaceMacros
            // throws instead of silently falling back to TODAY().
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\AutoScanOmitted-01.template.json"));

            // Act & Assert
            var ex = Assert.Throws<InvalidMacroReferenceException>(() => engine.ApplyTemplates(database.Model));
            Assert.Contains("Invalid configuration for scan columns", ex.Message);
        }

        [Fact]
        public void ApplyTemplates_HolidaysDefinitionTableWithEmptyTemplate_DoesNotLeaveHalfCreatedTableInModel()
        {
            // Arrange: reuses the same config as
            // EngineDispatchCharacterizationTests.ApplyTemplates_HolidaysDefinitionTableWithEmptyTemplate_ThrowsInvalidConfigurationException
            // (Table = "HolidaysDefinition", Template = ""), but asserts the model's resulting STATE after
            // the throw rather than only the exception type.
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\Dispatch-05 - HolidaysDefinitionTable-EmptyTemplate.template.json"));

            // Act
            Assert.Throws<InvalidConfigurationException>(() => engine.ApplyTemplates(database.Model));

            // Assert: fixed behavior -- ApplyHolidaysDefinitionTable now validates templateEntry.Template
            // BEFORE creating/adding the target Table, so the throw leaves the model exactly as it was
            // before the call: no phantom half-created "HolidaysDefinition" table.
            var halfCreatedTable = database.Model.Tables.Find("HolidaysDefinition");
            Assert.Null(halfCreatedTable);
        }
    }
}