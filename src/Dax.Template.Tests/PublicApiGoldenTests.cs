namespace Dax.Template.Tests
{
    using Dax.Template.Tests.Infrastructure;
    using Xunit;

    /// <summary>
    /// P0 public-API baseline change-detector (Phase M Stage 0). Reflects over the shipped
    /// <c>Dax.Template</c> assembly, dumps its public (and protected) surface deterministically via
    /// <see cref="PublicApiSurface.Dump"/>, and snapshots it through the existing golden-file harness
    /// (<see cref="GoldenFile.AssertMatchesSnapshot"/>).
    ///
    /// This is a CHANGE-DETECTOR, not a hard freeze/gate: it exists so intended vs. accidental
    /// public-surface changes are visible for review in each PR. When the public surface legitimately
    /// changes, regenerate the snapshot (set <c>UPDATE_GOLDEN=1</c> and re-run this test) and review the
    /// resulting diff to <c>_data/Golden/PublicApi.txt</c> as part of the PR.
    /// </summary>
    public class PublicApiGoldenTests
    {
        [Fact]
        public void ShippedAssembly_PublicApiDump_MatchesSnapshot()
        {
            var assembly = typeof(Engine).Assembly;

            var dump = PublicApiSurface.Dump(assembly);

            GoldenFile.AssertMatchesSnapshot(dump, "PublicApi", extension: "txt");
        }
    }
}