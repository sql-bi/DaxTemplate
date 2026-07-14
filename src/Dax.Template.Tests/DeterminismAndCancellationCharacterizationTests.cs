namespace Dax.Template.Tests
{
    using Dax.Template.Tests.Infrastructure;
    using System;
    using System.Linq;
    using System.Threading;
    using Xunit;

    /// <summary>
    /// Characterization tests pinning two cross-cutting behaviors of <see cref="Engine.ApplyTemplates"/>:
    /// determinism across fully independent runs (finer-grained than
    /// <see cref="IdempotencyCharacterizationTests"/>'s same-engine/same-model re-run check), and
    /// cancellation honoring via a pre-cancelled <see cref="CancellationToken"/>.
    /// </summary>
    public class DeterminismAndCancellationCharacterizationTests
    {
        private const string StandardTemplatePath = @".\_data\Templates\Config-01 - Standard.template.json";

        [Fact]
        public void ApplyTemplates_TwoIndependentEnginesAndModels_ProduceIdenticalNormalizedOutput()
        {
            // Arrange: unlike IdempotencyCharacterizationTests (same Engine instance, same Model, applied
            // twice in sequence), this builds two entirely independent Package/Engine/Database instances
            // from scratch to rule out any cross-run state leaking through shared/static state (e.g. cached
            // regexes, GUID generation) beyond the lineage-tag GUIDs that GoldenFile already normalizes.
            var databaseA = OfflineModelFixture.Build();
            var engineA = new Engine(Package.LoadFromFile(StandardTemplatePath));
            engineA.ApplyTemplates(databaseA.Model);
            var outputA = GoldenFile.SerializeNormalized(databaseA);

            var databaseB = OfflineModelFixture.Build();
            var engineB = new Engine(Package.LoadFromFile(StandardTemplatePath));
            engineB.ApplyTemplates(databaseB.Model);
            var outputB = GoldenFile.SerializeNormalized(databaseB);

            // Act & Assert
            Assert.Equal(outputA, outputB);
        }

        [Fact]
        public void ApplyTemplates_PreCancelledToken_ThrowsOperationCanceledExceptionBeforeMutatingModel()
        {
            // Arrange: Engine.ApplyTemplates calls cancellationToken.ThrowIfCancellationRequested() as the
            // first statement inside the Templates.ForEach loop body, before the per-class dispatch action
            // runs. With a pre-cancelled token and a non-empty Templates array (Config-01 has four
            // entries), the very first iteration throws immediately -- the actual type thrown by
            // ThrowIfCancellationRequested is System.OperationCanceledException (not TaskCanceledException,
            // since there is no async Task involved here).
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(StandardTemplatePath));
            var originalTableNames = database.Model.Tables.Select(t => t.Name).ToArray();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.Throws<OperationCanceledException>(() => engine.ApplyTemplates(database.Model, cts.Token));

            // The apply never started mutating the model: no template-generated tables were added.
            var tableNamesAfter = database.Model.Tables.Select(t => t.Name).ToArray();
            Assert.Equal(originalTableNames, tableNamesAfter);
        }
    }
}