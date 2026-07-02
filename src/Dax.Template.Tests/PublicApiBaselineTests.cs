namespace Dax.Template.Tests
{
    using Dax.Template.Tests.Infrastructure;
    using Xunit;

    /// <summary>
    /// Phase M Stage 0, item P0-a: a change-detector for the public API surface of the shipped
    /// <c>Dax.Template</c> library assembly. Builds a deterministic reflection-based dump of every
    /// externally-visible (public / protected / protected-internal) type and member and snapshot-compares
    /// it against a committed baseline (<c>_data/Golden/PublicApi.txt</c>), following the same
    /// <see cref="GoldenFile"/> convention as the BIM golden-file tests.
    ///
    /// This is NOT a hard freeze/gate: per .claude/SESSION_HANDOFF.md "Phase M — locked decisions" #4, the
    /// public API is open to improvement and breaking changes are acceptable. A failing assertion here
    /// simply means the public surface changed since the baseline was captured — review the diff, and if
    /// the change is intentional, regenerate the baseline with <c>UPDATE_GOLDEN=1</c> as part of the same PR.
    /// </summary>
    public class PublicApiBaselineTests
    {
        [Fact]
        public void DaxTemplateAssembly_PublicApi_MatchesBaseline()
        {
            var assembly = typeof(Engine).Assembly;

            var actual = PublicApiSnapshot.Build(assembly);

            GoldenFile.AssertMatchesSnapshot(actual, "PublicApi", "txt");
        }

        /// <summary>
        /// The dump must be stable across independent invocations against the same assembly (no reliance on
        /// reflection's unordered member enumeration, hash-based dictionary iteration, etc.) so that the
        /// snapshot comparison above is meaningful rather than flaky.
        /// </summary>
        [Fact]
        public void PublicApiSnapshot_Build_IsDeterministicAcrossRuns()
        {
            var assembly = typeof(Engine).Assembly;

            var first = PublicApiSnapshot.Build(assembly);
            var second = PublicApiSnapshot.Build(assembly);

            Assert.Equal(first, second);
        }
    }
}
