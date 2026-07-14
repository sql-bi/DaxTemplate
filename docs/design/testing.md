# Testing

All automated tests live in [src/Dax.Template.Tests/](../../src/Dax.Template.Tests/) (xUnit, `net10.0`).

## Offline golden-file (snapshot) harness

- `Infrastructure/OfflineModelFixture.cs` — builds a small, synthetic, **disconnected** TOM `Database` in memory (a `Sales` table with a date column and measures, an `Orders` table with a second date column).
  Because the `Database` is never attached to a `Server`, the model is disconnected — this is what makes `Engine.RequestTableRefresh`'s guard skip refresh requests (see [apply-templates-lifecycle.md](apply-templates-lifecycle.md)), so the whole `ApplyTemplates` path runs without any live Analysis Services / Power BI connection.
- `Infrastructure/GoldenFile.cs` — serializes the resulting `Database` to BIM JSON (`SerializeNormalized`), normalizes non-deterministic content (freshly-generated `lineageTag` GUIDs are blanked out, line endings normalized), and compares against a committed snapshot file under `_data/Golden/*.bim` (`AssertMatchesSnapshot`).
- Set the environment variable `UPDATE_GOLDEN=1` to (re)write the snapshot files instead of asserting against them — used when a template change intentionally changes the generated model.
- Test entry points: `PackageTests.cs`, `ApplyTemplatesGoldenTests.cs`, `HierarchyTabularReferenceTests.cs`.

## Live-server tests (opt-in, not required for CI)

- `Infrastructure/LiveServerFactAttribute.cs` — a `FactAttribute` that self-skips unless both `DAXTEMPLATE_LIVE_SERVER` and `DAXTEMPLATE_LIVE_DATABASE` environment variables are set.
  Tests using `[LiveServerFact]` stay discoverable/runnable on demand but never gate the pipeline.
- CI ([.github/workflows/ci.yml](../../.github/workflows/ci.yml)) runs `dotnet test ./src/Dax.Template.Tests/Dax.Template.Tests.csproj` with no filter; live-server tests simply report as *skipped* rather than failing, because the env vars are unset in CI.
- The three `[LiveServerFact]` tests — `CalendarStandardConfig_LiveServerApply_ProducesModelChanges` ([CalendarGoldenTests.cs](../../src/Dax.Template.Tests/CalendarGoldenTests.cs)), `CalculationGroupStandardConfig_LiveServerApply_ProducesModelChanges` ([CalculationGroupGoldenTests.cs](../../src/Dax.Template.Tests/CalculationGroupGoldenTests.cs)), and `FunctionLibraryStandardConfig_LiveServerApply_ProducesModelChanges` ([FunctionLibraryGoldenTests.cs](../../src/Dax.Template.Tests/FunctionLibraryGoldenTests.cs)) — all target the same committed empty model, [pbix/EmptyTestTemplates.pbix](../../src/Dax.Template.Tests/pbix/EmptyTestTemplates.pbix). Publish/deploy it to a Tabular server (Power BI Premium/PPU/Fabric XMLA read-write endpoint, or a recent Analysis Services instance) and point `DAXTEMPLATE_LIVE_SERVER` at that endpoint and `DAXTEMPLATE_LIVE_DATABASE` at the deployed model's name.
- An empty model is sufficient — no tables need to be pre-created — because each config is self-contained: Functions (`Config-04 - Functions.template.json`) is model-level and its UDF bodies reference only their own parameters; calculation groups (`Config-03 - CalculationGroup.template.json`) generate their own `Time Intelligence` table and store calculation-item DAX as opaque, never-compiled strings; and Calendar (`Config-02 - Calendar.template.json`) first generates its own `Date` table via `CustomDateTable`, then binds the `Calendar` column groups to the columns it just created.
- Each test runs `Engine.ApplyTemplates` + `Engine.GetModelChanges` and deliberately never calls `SaveChanges`, so the run is non-destructive — changes are discarded on disconnect.
- The deployed model must report `CompatibilityLevel >= 1702` (required for user-defined functions); the Functions test throws `InvalidConfigurationException` on its compat pre-check otherwise. It is also the only one of the three that actually validates/compiles the generated `(params) => body` DAX signatures server-side, since TOM does not validate `Function.Expression` offline.
- To run only these tests: `dotnet test ./src/Dax.Template.Tests/Dax.Template.Tests.csproj --filter "FullyQualifiedName~LiveServer"` (with the two env vars set).

## `InternalsVisibleTo`

[src/Dax.Template/AssemblyInfo.cs](../../src/Dax.Template/AssemblyInfo.cs) declares `[InternalsVisibleTo("Dax.Template.Tests")]` (with a signed-build variant under the `SIGNED` compilation symbol, matching the Azure DevOps release pipeline's assembly signing).
This lets test code assert on internal state — notably the `Tabular*` back-references described in [table-generation.md](table-generation.md) — without needing a public API surface just for testing.

## Running tests

- All offline tests: `dotnet test ./src/Dax.Template.Tests/Dax.Template.Tests.csproj --configuration Release`
- Single test: `dotnet test ./src/Dax.Template.Tests/Dax.Template.Tests.csproj --filter "FullyQualifiedName~<TestName>"`
- Explicitly excluding live-server tests: add `--filter "FullyQualifiedName!~LiveServer"` (not required, since they self-skip without the env vars, but useful to avoid the "skipped" noise).
