# Code coverage & mutation testing

> Phase M / Stage 0 / P0-b (test hardening). See [.claude/SESSION_HANDOFF.md](../../.claude/SESSION_HANDOFF.md)
> ("Phase M" Stage 0 P0, "Phase M — locked decisions" #5) for the target this document tracks toward.

This doc covers the **coverage** tooling for `Dax.Template` (the core library only —
`Dax.Template.TestUI` is out of scope for the metric) and the **Stryker.NET** mutation-testing
scaffold on its highest-risk subsystems. It complements [testing.md](testing.md), which describes the
functional (golden-file) test harness itself.

## Locked target (decision #5, 2026-07-01)

- CI-enforced floor: **80% line coverage on `Dax.Template` only** (`Dax.Template.TestUI` excluded).
- **~90%** on the refactor-target subsystems: `Tables` (incl. the `Tables/Dates` date branch), `Measures`,
  `Model`, `Extensions` (dependency sort), and `Engine`/`Package` dispatch.
- Justified, attributed exclusions for live-server-only branches and generated/trivial members
  (DTO/`ToString`/`Reset` boilerplate) so the percentage reflects reachable code. **No such attributes
  have been applied to library source yet** — that is Stage 2 work; this Stage 0 pass only sets up the
  filter mechanism (`ExcludeFromCodeCoverage` etc. are already recognized by the coverlet configuration
  below).
- Add Stryker.NET mutation testing on the 2–3 highest-risk subsystems as a stronger refactor-safety
  signal than raw line coverage.
- 100% remains aspirational for the core transformation logic; the CI floor may be raised (e.g. to 85%)
  once Stage 0's P1/P2 characterization tests land.

## Coverage baseline (Stage 0, recorded 2026-07-01)

Measured with `coverlet.collector` 10.0.1 (already referenced by `Dax.Template.Tests`, previously wired
but never invoked) via the `dotnet test --collect:"XPlat Code Coverage"` data collector, scoped to the
`Dax.Template` assembly only (see [coverlet.runsettings](../../src/Dax.Template.Tests/coverlet.runsettings)),
report rendered with ReportGenerator 5.5.10.

**Overall: 68.4% line coverage (1,447 / 2,113 coverable lines), 42.8% branch coverage, 41 classes.**
This is **below the locked 80% floor** — see "CI gate — current status" below for how this is wired.

### Per-subsystem breakdown

Grouped per the subsystem list in `.claude/SESSION_HANDOFF.md` ("Phase M" intro). `Interfaces`, `Enums`,
and `Constants` have zero executable/coverable lines (interfaces, enum members, const string literals) —
they are intentionally absent from the coverage % (not a gap; there is nothing to instrument).

| Subsystem | Covered / Coverable lines | Line coverage | Notes |
|---|---|---|---|
| `Tables` (+ `Tables/Dates`) | 976 / 1,253 | **77.9%** | `Tables` alone 81.7%, `Tables/Dates` alone 75.0%. `SimpleDateTable` is 0% (unused by the current golden config); `TableTemplateBase`/`CalculatedTableTemplateBase` ~71% |
| `Measures` | 177 / 284 | **62.3%** | `MeasureTemplateBase` 49.3% is the weakest file; `MeasuresTemplate` 77% |
| `Model` | 5 / 180 | **2.8%** | Dominated by `ModelChanges.cs` (0/158) — the `GetModelChanges` reflection-diff path has no dedicated tests yet |
| `Extensions` (dependency sort) | 180 / 212 | **84.9%** | `ComputeDependencies`/`GetDependencies`/`GetScanColumns`/`TSort` all 94–100%; `ReflectionHelper` is 0% (untested reflection helper) |
| `Syntax` | 4 / 8 | 50.0% | Small subsystem; `DaxElement`/`Var` under-covered |
| `Exceptions` | 0 / 23 | 0.0% | Exception types/messages have no direct tests (only exercised indirectly, uncovered by golden-file paths so far) |
| root (`Engine`, `Package`, `CustomTemplateDefinition`, `Translations`) | 105 / 153 | **68.6%** | `Engine` 77.6%, `Package` 44.2% (config-load/error paths under-tested), the other two 100% |
| **Total (`Dax.Template`)** | **1,447 / 2,113** | **68.4%** | |

### Refactor-target subsystems vs. the ~90% target

None of the five refactor-target subsystems named in decision #5 are near 90% yet:

| Target subsystem | Current | Gap to ~90% |
|---|---|---|
| Tables (incl. date branch) | 77.9% | ~12 pts |
| Measures | 62.3% | ~28 pts |
| Model | 2.8% | ~87 pts (`ModelChanges`/`GetModelChanges` essentially untested) |
| Extensions (dependency sort) | 84.9% | ~5 pts (closest to target) |
| Engine/Package dispatch | 68.6% | ~21 pts |

This is exactly the gap the Stage 0 P1/P2 characterization-test backlog in `.claude/SESSION_HANDOFF.md`
targets (dependency ordering / TSort DAG+cycle tests, `ModelChanges`/reflection-path tests, Engine dispatch
per-`Class` tests, `Package` load/invalid-config tests, `MeasuresTemplate` wrapping tests).

## Running coverage locally

```
dotnet test src/Dax.Template.Tests/Dax.Template.Tests.csproj --configuration Release \
  --collect:"XPlat Code Coverage" --settings src/Dax.Template.Tests/coverlet.runsettings \
  --results-directory ./artifacts/coverage
```

This writes `./artifacts/coverage/<run-guid>/coverage.cobertura.xml`. Render a human-readable report
(overall + per-class breakdown, plus an HTML browsable report) with the repo-local
[ReportGenerator](https://reportgenerator.io/) tool (already declared in
[.config/dotnet-tools.json](../../.config/dotnet-tools.json); run `dotnet tool restore` once):

```
dotnet tool restore
dotnet tool run reportgenerator -reports:./artifacts/coverage/*/coverage.cobertura.xml \
  -targetdir:./artifacts/coverage/report -reporttypes:"Cobertura;TextSummary;Html"
```

- `./artifacts/coverage/report/Summary.txt` — the per-class % breakdown used to build the table above.
- `./artifacts/coverage/report/index.html` — a browsable, line-by-line coverage report.
- `./artifacts/coverage/report/Cobertura.xml` — the merged Cobertura file the CI threshold gate parses
  (`/coverage/@line-rate`).

`./artifacts/` is already gitignored — coverage output is never committed.

## `coverlet.runsettings`

[src/Dax.Template.Tests/coverlet.runsettings](../../src/Dax.Template.Tests/coverlet.runsettings) configures
coverlet's "XPlat Code Coverage" data collector:

- `Include=[Dax.Template]*` — an allow-list, so **only** the shipped core library is measured (this is
  what keeps `Dax.Template.TestUI` and the test assembly itself out of the metric, per decision #5).
- `ExcludeByAttribute` includes `ExcludeFromCodeCoverage` (the sanctioned mechanism for Stage 2 to mark
  generated/trivial members and live-server-only branches once that attribute work happens — no library
  source has been annotated yet).
- `Format=cobertura` for ReportGenerator/CI consumption.

## CI gate — current status (2026-07-01)

`.github/workflows/ci.yml` now:
1. Restores the repo-local dotnet tool manifest (`.config/dotnet-tools.json`: ReportGenerator, Stryker.NET).
2. Runs the test step with `--collect:"XPlat Code Coverage" --settings .../coverlet.runsettings`.
3. Renders a Cobertura + text + Markdown summary report with ReportGenerator, and appends the Markdown
   summary to the job's `$GITHUB_STEP_SUMMARY`.
4. Runs a threshold-gate step that parses the merged Cobertura `line-rate` and compares it against
   `COVERAGE_LINE_THRESHOLD`.
5. Uploads the report directory as a build artifact (`coverage-report-<os>`).

**Blocking vs. report-only decision:** the locked target is 80%, but the measured baseline (68.4%) is
below it. Per the Stage 0 P0-b task brief, the gate is **NOT** wired to hard-fail at 80% yet — that would
immediately red the pipeline for pre-existing, already-accepted debt. Instead:

- `COVERAGE_LINE_THRESHOLD` is set to **65%** — a non-regression floor comfortably under the 68.4%
  baseline (small buffer against normal test-count/ordering noise), so the gate **is blocking**, but only
  against a material regression below current coverage, not against the 80% target.
- The gate step and its comment in `ci.yml` both point back here and flag explicitly that 80% is not yet
  enforced.
- **Sequencing for the lead:** raise `COVERAGE_LINE_THRESHOLD` incrementally (and ultimately switch the
  comparison to the full 80%) as the Stage 0 P1/P2 characterization tests land and move the real number
  up. Re-run the "Running coverage locally" steps above after each batch of new tests to check progress
  before bumping the threshold.

## Stryker.NET mutation testing (scaffold)

[src/Dax.Template.Tests/stryker-config.json](../../src/Dax.Template.Tests/stryker-config.json) scopes
Stryker.NET to the three subsystems flagged as highest-risk in decision #5 and the handoff: the date-table
branch (`Tables/Dates`), `Measures`, and the `Extensions` dependency-sort
(`ComputeDependencies`/`GetDependencies`/`GetScanColumns`/`TSort`/`ReflectionHelper`). It mutates
`Dax.Template` and runs the existing `Dax.Template.Tests` suite as the test runner (`vstest`) — no new
test infrastructure needed.

Run it locally (from `src/Dax.Template.Tests`, after `dotnet tool restore` at the repo root):

```
cd src/Dax.Template.Tests
dotnet tool run dotnet-stryker
```

Reports land under `src/Dax.Template.Tests/StrykerOutput/<timestamp>/reports/` (gitignored) as HTML and
Markdown. `mutate` uses glob patterns relative to the mutated project's directory (`src/Dax.Template`),
e.g. `Tables/Dates/**/*.cs` — **not** relative to the config file's own directory.

**Validated 2026-07-01:** ran to completion in ~2 minutes (concurrency 4, on this dev machine) — 537
mutants tested (984 filtered out by the subsystem scope, 121 pre-existing compile-error mutants across
the whole project, 454 not covered by any test). This was a real, full pass over the scoped subsystems,
not a dry run — it confirms the config file is valid end-to-end (correct project/test-project resolution,
correct `mutate` glob syntax, successful build + initial test run + mutant generation + test execution).

**Result: mutation score 2.02%** (20 killed / 537 tested). This is deliberately not gated in CI yet (per
the P0-b task scope — "you need NOT run a full mutation pass in this task... it's slow") but is a strong,
concrete signal for Stage 0 P1/P2 test-writing: line coverage in these subsystems (Tables/Dates 75.0%,
Measures 62.3%, Extensions dependency-sort 84.9%) substantially **overstates** real test strength — most
covered lines are exercised but not meaningfully asserted on. Per-file mutation scores from the validation
run: `ComputeDependencies.cs` 4.55%, `GetDependencies.cs`/`ReflectionHelper.cs`/`TSort.cs`/
`StringExtensions.cs`/date-table files 0.00–3.67%, `BaseDateTemplate.cs` 3.00%. Expect a full run (all
mutants, default concurrency) to take longer on CI hardware — budget several minutes; not currently
wired into `ci.yml`.

### Thresholds in the scaffold

The generated defaults (`thresholds.high=80`, `low=60`, `break=0`) are left at Stryker's stock values and
are **not** enforced — `break=0` means Stryker never fails the process regardless of score. Revisit once
Stage 0/1 test-writing has moved the mutation score to a level worth gating on.
