namespace Dax.Template.Tests
{
    using Dax.Template.Constants;
    using Dax.Template.Tests.Infrastructure;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Characterization tests pinning re-run idempotency of <see cref="Engine.ApplyTemplates"/>: applying
    /// the same template configuration twice against a fresh offline model must produce identical output,
    /// and shrinking a MeasuresTemplate's target-measure list on a second run must clean up the
    /// previously-generated (now orphaned) measures via the <c>SQLBI_Template</c> annotation contract
    /// (see Constants/Attributes.cs and Measures/MeasuresTemplate.cs ApplyTemplate, around line 159-215).
    /// </summary>
    public class IdempotencyCharacterizationTests
    {
        private const string StandardTemplatePath = @".\_data\Templates\Config-01 - Standard.template.json";
        private const string FewerTargetsTemplatePath = @".\_data\Templates\Idempotency-01 - Standard-FewerTargets.template.json";

        [Fact]
        public void ApplyTemplates_AppliedTwice_ProducesIdenticalNormalizedOutput()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(StandardTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);
            var firstRun = GoldenFile.SerializeNormalized(database);

            engine.ApplyTemplates(database.Model);
            var secondRun = GoldenFile.SerializeNormalized(database);

            // Assert: re-running the same template against its own prior output is stable.
            Assert.Equal(firstRun, secondRun);
        }

        [Fact]
        public void ApplyTemplates_MeasuresTemplateReRunWithFewerTargetMeasures_RemovesOrphanedGeneratedMeasures()
        {
            // Arrange: apply the Standard config once, in full.
            var database = OfflineModelFixture.Build();
            var engineFullTargets = new Engine(Package.LoadFromFile(StandardTemplatePath));
            engineFullTargets.ApplyTemplates(database.Model);

            var sales = database.Model.Tables.Find("Sales")!;
            var generatedFromMargin = sales.Measures
                .Where(m => m.Annotations.Any(a => a.Name == Attributes.SqlbiTemplate) && m.Name.Contains("Margin"))
                .ToArray();
            Assert.NotEmpty(generatedFromMargin); // sanity check on the initial state

            // Act: re-apply with a config whose TargetMeasures no longer includes "Margin"/"Margin %".
            var engineFewerTargets = new Engine(Package.LoadFromFile(FewerTargetsTemplatePath));
            engineFewerTargets.ApplyTemplates(database.Model);

            // Assert: the measures generated from the now-removed target measures are gone...
            var remainingGeneratedFromMargin = sales.Measures
                .Where(m => m.Annotations.Any(a => a.Name == Attributes.SqlbiTemplate) && m.Name.Contains("Margin"))
                .ToArray();
            Assert.Empty(remainingGeneratedFromMargin);

            // ...while measures generated for the still-configured target measures remain.
            var remainingGeneratedFromSalesAmount = sales.Measures
                .Where(m => m.Annotations.Any(a => a.Name == Attributes.SqlbiTemplate) && m.Name.Contains("Sales Amount"))
                .ToArray();
            Assert.NotEmpty(remainingGeneratedFromSalesAmount);
        }
    }
}