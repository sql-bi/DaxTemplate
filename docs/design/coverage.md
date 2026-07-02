# Coverage

Phase M / Stage 0 / P0-b (see `.claude/SESSION_HANDOFF.md`, locked decision #5). This doc records the
coverlet configuration, the measured baseline, the CI-enforced floor and its rationale, the exclusion
policy, and the Stryker.NET mutation-testing scaffold.

## Enforced CI floor

[.github/workflows/ci.yml](../../.github/workflows/ci.yml) runs the offline suite with
`--collect:"XPlat Code Coverage"` and [coverlet.runsettings](../../src/Dax.Template.Tests/coverlet.runsettings),
then `reportgenerator` produces a Cobertura summary, then a threshold gate step reads the top-level
`line-rate` and fails the build if it drops below `COVERAGE_LINE_THRESHOLD`.

- **Locked target (decision #5): 80% line coverage on `Dax.Template` only** (`Dax.Template.TestUI` is
  excluded from the metric — it's an interactive WinForms harness with no automated tests).
- **Current gate value: `COVERAGE_LINE_THRESHOLD = 80` — the locked target, now met.** A targeted
  Measures + Package characterization-test top-up (below) closed the remaining gap, raising measured
  overall coverage from 75.2% to 81.1%. The gate is no longer a non-regression floor below target; `80`
  is now the enforced target value itself, with ~1.1 points of measured headroom. It fails only on a
  material coverage regression below that locked target.
- 100% remains aspirational for the core transformation logic.

## Measured baseline (2026-07-02, post Measures/Package top-up)

Measured by reproducing the CI sequence locally against the full offline suite (130 tests: 129 passed, 1
skipped — the skipped test is a `[LiveServerFact]` that self-skips without live-server env vars). This
suite grew from 88 to 129 passing tests via a targeted characterization-test top-up focused on the two
subsystems with the largest gaps against the locked target, `Measures` and `Package` (41 new tests):

```
dotnet build ./src/Dax.Template.sln --configuration Release
dotnet test ./src/Dax.Template.Tests/Dax.Template.Tests.csproj --configuration Release --no-build \
  --collect:"XPlat Code Coverage" --settings ./src/Dax.Template.Tests/coverlet.runsettings \
  --results-directory ./artifacts/coverage
dotnet tool run reportgenerator "-reports:./artifacts/coverage/*/coverage.cobertura.xml" \
  "-targetdir:./artifacts/coverage/report" "-reporttypes:Cobertura;TextSummary;MarkdownSummaryGithub"
```

**Overall: 81.1% line coverage** (1,650 / 2,034 coverable lines; 1 assembly instrumented — `Dax.Template`
only, confirmed by the coverlet `Include` filter — see [Exclusion policy](#exclusion-policy)). This clears
the locked 80% target with ~1.1 points of headroom. It is up from the prior recorded baseline of 75.2%
(2026-07-02, pre-top-up), because the Measures + Package characterization-test top-up added 41 tests
covering `MeasuresTemplate`, `MeasureTemplateBase`, and `Package` — the two subsystems previously furthest
from the ~90% per-subsystem target (67.3% and 48.1% respectively) — without touching any other subsystem
(`Tables`, `Engine`, `Model`, and `Extensions` line counts are unchanged from the prior measurement, as
expected since no test or production code in those areas changed).

### Per-subsystem line coverage

The locked ~90% target applies to the refactor-target subsystems: `Tables`, `Measures`, `Model`,
`Extensions` (dependency-sort specifically), and `Engine`/`Package` dispatch.

| Subsystem                                  | Covered / Coverable | Line % | vs ~90% target |
| ------------------------------------------- | -------------------: | -----: | --------------- |
| `Extensions` — dependency-sort (`TSort.cs`, `ComputeDependencies.cs`, `GetDependencies.cs`, `GetScanColumns.cs`) | 176 / 180 | **97.8%** | met |
| `Extensions` — full namespace (adds `ReflectionHelper`, `StringExtensions`) | 207 / 212 | 97.6% | met |
| `Tables` (incl. `Tables.Dates`)             |          1001 / 1253 |  79.9% | gap ~10 pts |
| `Engine`                                    |               69 / 85 |  81.2% | gap ~9 pts |
| `Measures`                                  |             281 / 284 |  98.9% | met |
| `Package`                                   |               52 / 52 | 100.0% | met |
| `Engine` + `Package` combined (dispatch)    |             121 / 137 |  88.3% | gap ~2 pts |
| `Model`                                     |               7 / 101 |   6.9% | gap ~83 pts |
| `Exceptions` (not a locked-target subsystem, reported for completeness) | 13 / 23 | 56.5% | — |
| `Syntax` (not a locked-target subsystem)    |                4 / 8 |  50.0% | — |
| **Overall (`Dax.Template`)**                |         **1650 / 2034** | **81.1%** | **80% target met** |

Notable per-class detail behind the rollups above (from `reportgenerator`'s `Summary.txt`):
`CustomTableTemplate<T>` 99.5%, `HolidaysTable`/`HolidaysDefinitionTable`/`HolidaysConfig`/
`ReferenceCalculatedTable` 100%, `CustomDateTable` 89.3%, `CalculatedTableTemplateBase` 82.3%,
`BaseDateTemplate<T>` 81.7%, `TableTemplateBase` 76.2%, `SimpleDateTable` 0% (see gaps below);
`MeasuresTemplate` 100%, `MeasureTemplateBase` 98.1%, `MeasuresTemplateDefinition` 100%, `Package` 100%
(all three moved from the prior 86% / 51.2% / — / 48.1% via the 2026-07-02 Measures/Package
characterization-test top-up); `Column` 20%, `Hierarchy`/`Level` 100%, `Measure` 0%, `ModelChanges` 2.2%
(post-exclusion — see below; unchanged by this top-up, still a P1/P2 gap).

**Why `Model` is so far below target:** most of `ModelChanges`' bulk (`PopulatePreview` and its
exclusive live-query helper `GetPreviewData`) is excluded as offline-unreachable (see below) — the
*remaining* 2.2% reflects that its diff-building methods (`AddTable`/`AddColumn`/`AddMeasure`/
`AddHierarchy`/`SimplifyRemovedObjects`) and its now-uncovered-but-testable query-string helpers
(`GetQueryTablesDefinition`/`RenameTableReferences` — no longer excluded, see below) ARE reachable
offline (they don't need a live connection, just TOM objects / string inputs) but aren't directly
unit-tested today; the only current test (`GetModelChanges_AfterOfflineApply_StillReturnsEmptyBecauseModelIsDisconnected`)
exercises the empty/disconnected path, not the diff-building internals. Likewise `Model.Measure` and
`Model.Column` are thin data holders whose non-trivial members (`Reset()`, `GetDebugInfo()`,
explicit-interface accessors) simply aren't exercised yet. **These are genuine coverage gaps, not
offline-unreachable code** — they are good P1/P2 characterization-test candidates (see
[testing.md](testing.md) and `.claude/SESSION_HANDOFF.md`), not exclusion candidates.

**Other known, deliberately-not-excluded gaps:**
- `Tables.Dates.SimpleDateTable` (0%): a fully offline-instantiable production class with no test
  exercising it yet — a real gap, not unreachable code.
- `Exceptions.*` (0–100%): each custom exception has a single-message constructor; several still aren't
  triggered by any test scenario (e.g. `ExistingTableException`, `InvalidAttributeException`,
  `InvalidVariableReferenceException`, still 0%), while others (`TemplateException`,
  `InvalidConfigurationException`, `TemplateConfigurationException`, `TemplateUnexpectedException`) picked
  up partial coverage as a side effect of the 2026-07-02 Measures/Package top-up exercising error paths
  that construct them. All remain reachable offline by constructing the right failure scenario — future
  characterization-test candidates, not exclusions.
- ~~`Package.SaveTo`~~ — closed by the 2026-07-02 Measures/Package characterization-test top-up;
  `Package` is now 100% covered.
- `Model/ModelChanges.cs`'s `GetQueryTablesDefinition` and `RenameTableReferences` (0%): pure
  offline-reachable string-building/substitution helpers with no `AdomdConnection` dependency of their
  own — a code-review pass (2026-07-02) found the original `[ExcludeFromCodeCoverage]` on these two was
  over-broad (it was justified only by their *caller*, `PopulatePreview`, needing a live connection, not
  by these methods themselves needing one) and removed it. They are left as an honest, uncovered gap —
  future characterization-test candidates, not exclusions.

## Exclusion policy

[coverlet.runsettings](../../src/Dax.Template.Tests/coverlet.runsettings) scopes the metric to
`Dax.Template` only (`<Include>[Dax.Template]*</Include>`, with `<Exclude>[Dax.Template.Tests]*</Exclude>`
as a defense-in-depth backstop), applies `<SkipAutoProps>true</SkipAutoProps>` so trivial
auto-implemented property accessors aren't counted as coverable lines, and honors
`[ExcludeFromCodeCoverage]` / `[GeneratedCode]` / `[CompilerGenerated]` via `<ExcludeByAttribute>`.

Per the locked decision, **production-code exclusions must be small, justified, and individually
commented** — they exist only for genuinely offline-unreachable (live-server-only) branches or
clearly trivial/generated boilerplate that materially distorts the metric. They are NOT a tool for
hiding untested-but-testable code (see the gaps listed above, which are intentionally left unexcluded).

Exactly three exclusions are in place, all in `Dax.Template`, all tagged with
`[ExcludeFromCodeCoverage]` (`System.Diagnostics.CodeAnalysis`) and an inline justification comment.
(An earlier pass on 2026-07-02 had also excluded `GetQueryTablesDefinition` and `RenameTableReferences`
in `Model/ModelChanges.cs`; a code-review pass the same day found both over-broad — neither has a live
`AdomdConnection` dependency of its own, they are pure offline-reachable string-building/substitution
logic — and removed the attributes, leaving them as an honest, uncovered gap rather than a metric
exclusion; see the gaps list above.)

1. **`Model/ModelChanges.cs` — `PopulatePreview` (public) and its exclusive private helper
   `GetPreviewData`**: `PopulatePreview` runs live DAX queries over an `AdomdConnection` to fetch
   preview rows for a calculated table, and `GetPreviewData` is the helper that actually opens the
   connection and calls `ExecuteReader`. Neither has an offline equivalent (their only callers are in
   `Dax.Template.TestUI`, which is itself excluded from the metric and not part of CI) and neither can
   be exercised without a real Analysis Services / Power BI connection.
2. **`Model/EntityBase.cs` — `ToString()` override**: a debugger/diagnostics-only display helper
   (`"{TypeName} : {Name}"`) never invoked by production logic or the offline suite. This is the
   `ToString()`/boilerplate case named explicitly in the locked decision.

Net effect: coverable lines dropped from 2,113 (zero exclusions) to 2,034 (-79 lines) with covered lines
unchanged (1,529), moving the overall metric from 72.3% to 75.2%.

**Verification performed:** `PublicApiGoldenTests` (the `PublicApi.txt` golden) stayed green after
adding/removing these attributes — the API dump captures types/members/modifiers, not attributes, so it
is unaffected by `[ExcludeFromCodeCoverage]`. The full 88-passed/1-skipped suite and all BIM/Config
goldens are unaffected (no test file, snapshot, or JSON config was touched).

## Stryker.NET mutation testing

[.config/dotnet-tools.json](../../.config/dotnet-tools.json) pins `dotnet-stryker` 4.15.0.
[stryker-config.json](../../src/Dax.Template.Tests/stryker-config.json) (run from
`src/Dax.Template.Tests/`, Stryker's default working directory for this project) scopes mutation to the
2–3 highest-risk subsystems rather than the whole library, because a full-project run is far too slow to
be a routine local/CI signal (an earlier exploratory full run recorded ~2,200+ candidate mutants across
the whole `Dax.Template` assembly).

**Scope and rationale — `Tables/**`, `Measures/**`, and the `Extensions` dependency-sort files
(`TSort.cs`, `ComputeDependencies.cs`, `GetDependencies.cs`, `GetScanColumns.cs`):**
- `Tables` and `Measures` are the two largest, most complex subsystems and the primary Phase M
  refactor targets (Stage 2/3) — mutation testing here is the strongest signal that the
  byte-identical golden-file gate is actually pinning down *behavior*, not just producing identical
  output for the specific inputs the goldens happen to cover.
- The dependency-sort machinery (`TSort` + its `ComputeDependencies`/`GetDependencies`/`GetScanColumns`
  helpers) is the correctness-critical core that every table/measure generation path relies on
  (topological ordering, cycle detection); a silent logic bug there would corrupt output broadly
  without necessarily breaking any single golden file that doesn't probe that exact edge case.

**Recorded baseline (timeboxed run, 2026-07-02):** a full run across `Tables/**` + `Measures/**` was not
timeboxed locally (the committed `stryker-config.json` scopes to all three areas and can be run as-is
when time allows); the representative run recorded here was scoped to the **`Extensions`
dependency-sort files only** (the fourth, smallest area in the committed config), run via:

```
cd src/Dax.Template.Tests
dotnet tool run dotnet-stryker -- -m "Extensions/TSort.cs" -m "Extensions/ComputeDependencies.cs" \
  -m "Extensions/GetDependencies.cs" -m "Extensions/GetScanColumns.cs"
```

- **Mutation score: 52.25%** — 222 non-ignored, non-compile-error mutants across the 4 dependency-sort
  files: 106 killed, 42 survived, 10 timeout (counted as detected), 64 no-coverage (counted as
  undetected — none of these 4 files have `[ExcludeFromCodeCoverage]` members, so "no coverage" here
  means a mutated statement genuinely isn't exercised by any test, e.g. rarely-hit branches in
  `GetScanColumns`/`ComputeDependencies`). 130 further mutants were pre-filtered as
  "excluded from code coverage" (elsewhere in `Dax.Template`, not in these 4 files — an artifact of
  Stryker analyzing the whole assembly's coverage map even when `mutate` scopes which files get
  mutants) and 121 caused compile errors unrelated to this run (pre-existing `Safe Mode` mutations in
  `TableTemplateBase.cs`/`MeasureTemplateBase.cs`, outside the scoped files, auto-excluded by Stryker
  itself). Full report at
  `src/Dax.Template.Tests/StrykerOutput/2026-07-02.15-09-31/reports/mutation-report.html`
  (git-ignored, per the pre-existing `StrykerOutput/` `.gitignore` rule).
- Reproduce with:
  ```
  cd src/Dax.Template.Tests
  dotnet tool run dotnet-stryker -- -m "Extensions/TSort.cs" -m "Extensions/ComputeDependencies.cs" \
    -m "Extensions/GetDependencies.cs" -m "Extensions/GetScanColumns.cs"
  ```
- **Interpretation:** 52.25% is a real, moderate signal — most of the "no coverage" gap traces back to
  `GetScanColumns.cs` (68/70 lines covered but several mutated branches/conditions aren't independently
  exercised by the current test inputs) and `ComputeDependencies.cs`. This is a good, concrete Stage 2/3
  refactor-safety target: the 42 survived + 64 no-coverage mutants are the next actionable worklist for
  strengthening `DependencySortCharacterizationTests.cs`, even though line coverage for this subsystem
  is already high (97.8%) — the gap between 97.8% line coverage and 52.25% mutation score is exactly the
  kind of blind spot mutation testing is meant to surface (tests that execute a line without actually
  asserting on its effect).
- Not wired as a hard CI gate yet (per the locked decision — mutation testing is a stronger
  refactor-safety signal alongside the golden-file gate, not (yet) a blocking check).

**Recommended cadence:**
- Run the full committed scope (`Tables/**`, `Measures/**`, dependency-sort files) manually before/after
  a Stage 2/3 refactor of those subsystems, and record the score here.
- Do not run it on every CI build — even the scoped config mutates hundreds of candidate sites across
  `Tables`/`Measures`; it's a periodic/pre-refactor safety net, not a per-PR gate.
- Widen the `mutate` scope in `stryker-config.json` to additional subsystems (e.g. `Engine`/`Package`
  dispatch) once the line-coverage floor for those areas is closer to the ~90% target — mutation testing
  is most informative once there's substantial line coverage for mutants to be "seen" by.

## Path to the locked targets

1. **Overall 80% target: met.** The 2026-07-02 Measures + Package characterization-test top-up (41 new
   tests) closed the gap from 75.2% to 81.1%. `COVERAGE_LINE_THRESHOLD` is now `80` in
   [ci.yml](../../.github/workflows/ci.yml), enforced as the locked target (not an interim
   non-regression floor), with ~1.1 points of measured headroom.
2. **Remaining per-subsystem gaps below the ~90% bar:** `Model.ModelChanges` diff-building methods,
   `Model.Measure`/`Column`, `Tables.Dates.SimpleDateTable`, and the still-0% `Exceptions.*` members
   (`ExistingTableException`, `InvalidAttributeException`, `InvalidVariableReferenceException`) remain
   good P1/P2 characterization-test candidates — see `.claude/SESSION_HANDOFF.md` Stage 0 P1/P2 list.
   `Measures` (98.9%) and `Package` (100%) now meet the ~90% per-subsystem bar and can be dropped from
   that worklist.
3. **Per-subsystem ~90%:** track `Tables` (79.9%), `Model` (6.9%), and `Engine`/`Package` dispatch
   combined (88.3%) against the per-subsystem table above as tests land; `Extensions` dependency-sort
   (97.8%), `Measures` (98.9%), and `Package` (100%) already meet the ~90% bar.
4. **100%** remains aspirational for the core transformation logic and is not a near-term gate.
