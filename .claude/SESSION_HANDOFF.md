# Session Handoff — DAX Template: new DAX entities

> Resume instructions: open this repo in Claude Code and say
> **"Read .claude/SESSION_HANDOFF.md and resume Phase 0."**
> Last updated: 2026-07-01

## Goal
Extend the Dax.Template library (creates TOM objects from JSON templates) to support three new DAX
entities, one at a time, with tests and no regressions:
1. **Calendars** (priority 1) — native TOM `Calendar`, attached to a table; extends the existing calculated-table branch
2. **Calculation groups** (priority 2)
3. **User-defined functions / UDFs** (priority 3)

## Decisions locked in
- Implementation order: **Calendars -> Calc groups -> UDFs**
- TOM upgrade to **latest released 19.114.0**, **no preview features**
- ~~Keep `net6.0;net8.0`; drop net6 only if forced (it was NOT forced — kept)~~
  **SUPERSEDED 2026-07-01:** now **net10.0-only / C# 14 / SDK 10** — see "Toolchain upgrade" entry
  under Progress below.
- CI gates on **offline** tests; **live-server** tests included but NOT required for pipeline sign-off
- All JSON template config changes must be **purely additive** (existing templates keep working)
- **NEW (2026-07-01):** a codebase-wide **Modernization & Refactor initiative (Phase M)** now precedes
  Phase 1 (Calendars) — see "Phase M" under Progress below.
  Goal: modernize to C# 14 / .NET 10 idioms, uniform style, improved readability, safe
  behavior-preserving refactors.
  Guardrails: golden-file BIM must stay byte-identical; JSON templates stay additive; no public-API
  breaks without explicit sign-off; style changes are never bundled with behavior changes;
  subsystem-scoped commits; every change reviewed; CI offline tests gate.

## Architecture notes (verified)
- Dispatch: `Engine.ApplyTemplates` routes each `Templates[]` entry by its `Class` string to a handler.
  See `src/Dax.Template/Engine.cs` (~lines 67-211). Current classes: HolidaysDefinitionTable,
  HolidaysTable, CustomDateTable, MeasuresTemplate.
  ADDING AN ENTITY = new Class + handler + `*TemplateDefinition` POCO + JSON sub-templates.
- Closest analog for calc groups / functions: `src/Dax.Template/Measures/MeasuresTemplate.cs`
  (POCO from JSON; `ApplyTemplate(model,...)`; idempotency via `SQLBI_Template` annotation; re-runs
  replace prior output and clean orphans).
- Calendar branch extends `src/Dax.Template/Tables/CalculatedTableTemplateBase.cs`
  (and CustomDateTable / ReferenceCalculatedTable).
- Config schema: top-level `*.template.json` with `Templates[]`; `TemplateEntry` in
  `src/Dax.Template/Interfaces/ITemplates.cs`.
- Reflection precedent: `Engine.GetModelChanges` already pokes internal TOM members (TxManager/AllBodies)
  via `src/Dax.Template/Extensions/ReflectionHelper.cs`.

## TOM object model for the new entities (confirmed in released 19.114.0)
- Calendar: Microsoft.AnalysisServices.Tabular.Calendar -> attaches to `Table.Calendars`.
  Public: Name, Description, LineageTag, CalendarColumnGroups.
  `CalendarColumnGroup` key members CalendarColumnReferences and TimeUnit are INTERNAL (not public)
  in 19.114.0 — see RISK below.
- CalculationGroup / CalculationItem: attaches to `Table.CalculationGroup`.
  Public + complete: Expression, Ordinal, FormatStringDefinition, etc.
- Function (UDF): attaches to `Model.Functions`. Public: Name, Expression, IsHidden, Description.
- Compatibility level enforced SERVER-SIDE (not a hard TOM constant) — confirm exact minimum via the
  opt-in live-server test in each phase.

## RISK — Calendar column binding (affects Phase 1 design)
CalendarColumnGroup.CalendarColumnReferences and .TimeUnit are internal in 19.114.0, so a Calendar's
meaningful content can't be set through the normal public API. Options:
1. Reflection to set internal members (consistent with existing code; fragile across TOM versions)
2. TMDL/JSON injection — build calendar definition as serialized metadata and deserialize
   (most robust / version-tolerant) — LEANING TOWARD THIS
3. Re-check a newer released TOM for a public surface
OPEN DECISION — needs user input before implementing Phase 1.

## Progress

### Phase 0 — Foundations
- [x] TOM upgrade — `src/Dax.Template/Dax.Template.csproj` bumped 19.86.6 -> 19.114.0 (both
      Microsoft.AnalysisServices and ...AdomdClient). Build GREEN on net6.0 + net8.0, zero source
      changes, existing 5 tests pass on both TFMs. (Pre-existing CS8602 warning in
      ApplyDaxTemplate.cs:315, unrelated.)
- [x] Compatibility/preview spike — done; findings above. No preview binaries needed.
- [x] Offline test harness — DONE (worktree branch claude/magical-wilbur-213ce1). Approach:
      - `Infrastructure/OfflineModelFixture.cs` — builds a synthetic in-memory `Database` (disconnected,
        compat 1600) with a Sales fact (Order Date + target measures Sales Amount/Total Cost/Margin/Margin %)
        and an Orders table. Built in code rather than a committed .bim — easier to maintain.
      - `Infrastructure/GoldenFile.cs` — serializes the DB to BIM via `JsonSerializer.SerializeDatabase`,
        normalizes the ONLY volatile content (lineageTag GUIDs -> "") + CRLF->LF, snapshot-compares against
        `_data/Golden/{name}.bim`. `UPDATE_GOLDEN=1` regenerates. Snapshot path found by walking up from
        `[CallerFilePath]` to the .csproj dir (so it reads/writes committed source, not bin output).
        Verified deterministic across runs and proven to fail on a 1-char change (has teeth).
      - `ApplyTemplatesGoldenTests.cs` — runs the REAL `Engine.ApplyTemplates` dispatch offline and golden-files
        the Standard config (covers holidays-def, holidays, custom date table + reference table, AND the
        time-intelligence MeasuresTemplate output). Golden = 3681 lines.
      - `Infrastructure/LiveServerFactAttribute.cs` — opt-in skippable live-server category; runs only when
        `DAXTEMPLATE_LIVE_SERVER` + `DAXTEMPLATE_LIVE_DATABASE` env vars are set (applies + GetModelChanges,
        does NOT SaveChanges). Currently SKIPPED in CI.
      - Copied `HolidaysDefinition.json` into test `_data/Templates` (Standard config references it; was missing).
      - Suite: 7 passed + 1 skipped on net6.0 AND net8.0.
      ENABLER (production change, see below): guarded the 3 `Table.RequestRefresh` calls in `Engine.cs`.
- [x] Reviewer gate for Phase 0 — APPROVED by user (guard signed off). Phase 0 COMMITTED as `972c8cc` and
      consolidated onto the `add-calendar` feature branch (fast-forward). Branch `claude/magical-wilbur-213ce1`
      also points at it. The duplicate uncommitted TOM bump in the main checkout was reverted; the bump now
      lives only in committed history. Main checkout retains its independent `.gitignore` (+.serena/) and
      `.claude/` WIP. Nothing pushed yet.

### Phase 0 production change to confirm — Engine.RequestTableRefresh guard
`Engine.cs` had 3 unconditional `table.RequestRefresh(RefreshType.Full)` calls (holidays-def, holidays, date).
TOM throws "A disconnected object is read only and cannot be refreshed" on an in-memory model, which blocked
offline testing of the table-creation paths (exactly the branch Calendars/Phase 1 extends). Added a private
helper `RequestTableRefresh(Table)` that only requests refresh when `table.Model?.Server != null`. Rationale:
a disconnected model genuinely cannot be refreshed; real deployments always operate on a server-connected
model (TestUI does `server.Connect`), so production behavior is unchanged — this only stops the offline throw.
Confirmed correct semantics via MS Learn docs. Low risk, but it IS a touch to the shipping engine — flagging
for explicit sign-off.

### Hierarchy back-reference fix (2026-07-01)
- **Bug:** `AddHierarchies` in `src/Dax.Template/Tables/TableTemplateBase.cs` populated the internal
  back-references `Model.Level.TabularLevel` / `Model.Hierarchy.TabularHierarchy` incorrectly. A first
  loop created a `TabularLevel`, stored it on `level.TabularLevel`, then discarded it; a second loop
  created a DIFFERENT `TabularLevel` (the one actually added to the model) but never stored it back.
  `Hierarchy.TabularHierarchy` was never assigned (stayed null). Emitted BIM was correct, so the
  golden-file test could not catch it — the defect was purely in internal back-reference bookkeeping.
- **Fix:** Collapsed to a single loop mirroring the correct `AddColumns` pattern (same file): create the
  `TabularHierarchy`/`TabularLevel` once, assign it to `hierarchy.TabularHierarchy` / `level.TabularLevel`,
  and add that SAME instance to the model. Preserved Ordinal (0-based, declaration order),
  Name/Column/IsHidden/DisplayFolder, and the `CompatibilityLevel >= 1540` LineageTag guard. Also restored
  a per-level `cancellationToken.ThrowIfCancellationRequested()` inside the inner loop (parity with
  `AddColumns`).
- **Secondary cleanup:** Removed a redundant `modelLevel.Description = level.Description;` re-assignment
  in `src/Dax.Template/Tables/CustomTableTemplate.cs` (value already set in the object initializer; pure
  no-op).
- **Tests added:** `src/Dax.Template.Tests/HierarchyTabularReferenceTests.cs` — 6 xUnit tests via a
  minimal `TableTemplateBase` subclass exercising the real `ApplyTemplate` -> `AddHierarchies` path
  offline. Two guard the back-reference identity contract (`Assert.Same` between
  `Level.TabularLevel`/`Hierarchy.TabularHierarchy` and the instances in the model); four characterize
  ordinal order, level->column binding, and `Level.Reset()`/`Hierarchy.Reset()`. Uses existing
  `[InternalsVisibleTo("Dax.Template.Tests")]`.
- **Output impact:** None — golden BIM snapshot byte-identical (internal back-references are not
  serialized). Change is behavior-preserving for serialized output; fixes internal state relied on by
  future consumers.
- **Validation:** Build clean (0 warnings/errors); offline suite 13/13 pass on net6.0 + net8.0.
- **Reviewer:** APPROVED (GO) on both the test file and the fix.
- **Relevance to Calendars:** The date-table hierarchy path (Calendar/Fiscal) exercised here is the same
  branch Phase 1 extends, so the now-correct `TabularHierarchy`/`TabularLevel` back-references are a
  foundation for that work.

### Toolchain upgrade: .NET 10 / C# 14 / SDK 10 (2026-07-01)
- **What changed:**
  - `global.json`: SDK `8.0.400` -> `10.0.301` (`rollForward: latestFeature`, `allowPrerelease: false`
    unchanged).
  - `src/Dax.Template/Dax.Template.csproj`: target `net6.0;net8.0` -> `net10.0` (single TFM);
    `LangVersion` `12.0` -> `14.0`.
  - `src/Dax.Template.Tests/Dax.Template.Tests.csproj`: same change (`net6.0;net8.0` -> `net10.0`,
    `LangVersion` `12.0` -> `14.0`).
  - `src/Dax.Template.TestUI/Dax.Template.TestUI.csproj`: `net8.0-windows` -> `net10.0-windows`; added
    explicit `LangVersion 14.0`.
  - `.github/workflows/ci.yml`: `setup-dotnet` now installs `10.0.x` (was `6.0.x`); still resolves the
    exact SDK via `global-json-file`.
- **Consumer impact:** the published `Dax.Template` NuGet package now targets **net10.0 only**
  (previously multi-targeted `net6.0;net8.0`) — this NARROWS supported frameworks for consumers on
  older TFMs. See `CHANGELOG.md` `### Changed` under `[Unreleased]`.
- **Verification:** build GREEN on the single net10.0 target; offline suite 13/13 pass; golden BIM
  snapshot (`_data/Golden/Config-01 - Standard.bim`) byte-identical/unchanged. One pre-existing CS8602
  warning remains in `Dax.Template.TestUI` — unrelated to this upgrade (predates it).
- **Supersedes** the "Keep `net6.0;net8.0`" decision recorded above under "Decisions locked in".

### Phase M — Modernization & Refactor (planned, precedes Phase 1)
Codebase inventory (verified 2026-07-01): 61 library `.cs` files, ~93 public types.
Subsystems: Model(7) / Tables(11) / Measures(2) / Syntax(11) / Extensions(6) / Interfaces(7) /
Enums(2) / Exceptions(7) / Constants(2) / root(6).
All namespaces are block-scoped (0 file-scoped).
A comprehensive `.editorconfig` (266 lines) exists but is under-enforced — no `Directory.Build.props`,
no `dotnet format` CI gate, many rules at `:suggestion`.
1 shipped template config has golden coverage; coverlet is wired but inert (no coverage baseline).
Only 4 members use `= default!`.
`Dax.Template` is a published NuGet. LOCKED (2026-07-01): the public API is open to improvement and
breaking changes are acceptable — there is no Web API surface to preserve, and NuGet consumers may need
to adapt on the next major version. This is NOT a hard freeze constraint (see "Phase M — locked
decisions" below).

- **Stage 0 — Safety net first (test hardening before refactor)** — qa + devops.
  Prioritized tests:
  - P0: public-API baseline (PublicApiAnalyzers or a committed API-dump snapshot). LOCKED scope: this
    is a change-detector to surface intended vs. accidental public-surface changes for review in each
    PR — NOT a hard freeze/gate (public API is open to improvement; see "Phase M — locked decisions").
  - P0: coverage baseline (activate the currently-inert coverlet, record baseline). LOCKED target
    (2026-07-01): CI-enforced floor of 80% line coverage on the core library `Dax.Template` only
    (`Dax.Template.TestUI` excluded from the metric); ~90% on the refactor-target subsystems (Tables,
    Measures, Model, Extensions dependency-sort, Engine/Package dispatch); justified, attributed
    exclusions for live-server-only branches and generated/trivial members; add Stryker.NET mutation
    testing on the 2-3 highest-risk subsystems alongside the golden-file gate. 100% remains aspirational
    for the core transformation logic; the CI floor may be raised (e.g. to 85%) once Stage 0 reveals the
    real baseline.
  - P1: broaden golden coverage with synthetic configs beyond Config-01 (custom non-date table w/
    hierarchies, measures-only, holidays variants); Engine dispatch tests (each Class -> handler,
    unknown/invalid Class); idempotency (apply-twice identical normalized BIM + SQLBI_Template orphan
    cleanup); dependency ordering (TSort DAG + cycle -> CircularDependencyException,
    ComputeDependencies/GetDependencies/GetScanColumns); reflection paths
    (ReflectionHelper/GetModelChanges diff correctness).
  - P2: StringExtensions macro/var substitution, Package load/invalid-config,
    CustomTableTemplate.GetHierarchies non-date path, MeasuresTemplate wrapping, determinism +
    cancellation honoring.
  - Exit: API + coverage baselines committed, new tests green.
- **Stage 1 — Style/analyzer infrastructure** — devops.
  Add `Directory.Build.props` (centralize TargetFramework/LangVersion 14/Nullable/analyzers); enable
  .NET analyzers (+ optional Roslynator); escalate key `.editorconfig` rules suggestion->warning;
  file-scoped namespaces are house style (LOCKED); add CI `dotnet format --verify-no-changes` gate;
  wire warnings-as-errors into the CI build (LOCKED, 2026-07-01: CI-only, not necessarily local dev
  builds); fix the pre-existing CS8602 in `TestUI/ApplyDaxTemplate.cs:315`.
  Exit: clean `dotnet format` baseline + CI enforcement; conventions recorded in AGENTS.md/docs.
- **Stage 2 — Mechanical modernization sweeps (low-risk), subsystem by subsystem** — backend (+
  frontend for the TestUI WinForms project).
  Order leaf->core: Constants/Enums -> Exceptions -> Extensions -> Model -> Syntax -> Measures ->
  Tables (date branch last) -> Engine/Package -> TestUI.
  Per sweep: file-scoped namespaces, using cleanup/sort, target-typed new, collection expressions,
  pattern matching/switch expressions, nameof, raw string literals for embedded DAX/JSON,
  expression-bodied members where clearer, `required` for the 4 `= default!` members (LOCKED, 2026-07-01:
  approved — source-breaking for NuGet consumers, ships under a MAJOR VERSION BUMP), primary
  constructors where they cut boilerplate (LOCKED as house style alongside file-scoped namespaces).
  Gate each: golden byte-identical + full offline suite + API baseline unchanged + reviewer.
- **Stage 3 — Deeper refactors (higher-risk, opt-in per item)** — backend.
  De-duplicate AddAnnotations vs MeasuresTemplateBase.ApplyAnnotations (existing TODO) and unify
  column/hierarchy add patterns; encapsulate/modernize reflection (ReflectionHelper, GetModelChanges)
  with documented TOM-version fragility; readability pass on the Syntax subsystem; consistency pass on
  exceptions/messages.
  Each item proposed/reviewed/gated individually; behavior-preserving.
- **Stage 4 — Docs sync & closeout** — docs + reviewer.
  Update AGENTS.md/docs/design for any changed conventions; final reviewer gate.

### Phase M — dotnet-claude-kit alignment (2026-07-01)
Additive to the five LOCKED decisions below — nothing here changes scope, targets, or coverage numbers;
it only specifies which kit skills / Roslyn Navigator tools / kit agents each stage uses. Per CLAUDE.md
"Using dotnet-claude-kit", the kit is the repo-wide default for ALL phases, not just Phase M — the
per-stage detail below is simply Phase M's heaviest, most mechanical usage of it. The feature phases
(1-3) share the common baseline captured in "Feature phases (Phase 1–3) — kit defaults" below.
Composition happens at the lead.

- **Stage 0 — Safety net (test hardening)** — kit `testing` / `tdd` skills ONLY for xUnit v3 idioms, the
  AAA pattern, `FakeTimeProvider` (determinism), and Verify-style snapshot testing. SCOPE-OUT: their
  WebApplicationFactory / Testcontainers / HTTP / Postgres guidance does NOT apply — DaxTemplate's
  harness is offline golden-file BIM snapshots with no web/DB surface. Roslyn Navigator: `get_public_api`
  -> the P0 public-API baseline change-detector; `get_test_coverage_map` -> a heuristic complement to
  (not a replacement for) the coverlet-enforced coverage floor.
- **Stage 1 — Style/analyzer infrastructure** — kit `ci-cd` skill for the `dotnet format
  --verify-no-changes` gate and warnings-as-errors wiring — SCOPE-OUT the deploy / NuGet-push /
  DB-service YAML (this CI is offline golden-file). Roslyn `get_diagnostics` to inventory current
  analyzer/nullability warnings before escalating `.editorconfig` rules suggestion->warning. Kit
  `build-error-resolver` agent for warnings-as-errors fallout.
- **Stage 2 — Mechanical modernization sweeps** — kit `modern-csharp` skill IS the reference for the
  feature list this stage already enumerates (primary constructors, collection expressions,
  pattern/switch expressions, raw string literals for embedded DAX/JSON, `required`, the `field`
  keyword) — fully consistent with the LOCKED house-style decisions. Kit `de-sloppify` skill provides
  the ordered per-sweep engine (Step 1 format -> 2 unused usings -> 3 analyzer warnings -> 4 dead code ->
  5 TODOs -> 6 seal -> 7 CancellationToken) with commit-per-step and build+test verification after each
  step. CRITICAL: de-sloppify's "safe removals only" rule — verify no reflection / DI / serialization /
  annotation string-references before deleting anything — is essential here because DaxTemplate is
  reflection-heavy (`ReflectionHelper`, `GetModelChanges`, `SQLBI_Template` annotation lookups);
  dead-code removal must cross-check string-based usage, not just Roslyn `find_references`. Kit agents:
  `refactor-cleaner` for the structural de-sloppify steps (dead code / sealing / CancellationToken),
  `dotnet-architect` for primary-constructor conversions that touch constructor shape.
- **Stage 3 — Deeper refactors** — Roslyn `find_dead_code`, `detect_antipatterns` (async void,
  sync-over-async, `DateTime.Now`), and `detect_circular_dependencies` (to validate the Extensions
  dependency-sort / TSort subsystem) to target the refactors. Kit `error-handling` skill for the
  exceptions/messages consistency pass (scoped to naming/message-consistency guidance only — NOT its
  Result / RFC 9457 `ProblemDetails` HTTP-response patterns, which don't apply to this class library); `refactor-cleaner` / `dotnet-architect` agents for the
  AddAnnotations-vs-ApplyAnnotations dedup and reflection encapsulation.
- **Stage 4 — Docs sync & closeout** — kit `verify` skill's 7-phase pipeline as the closeout gate wrapper
  (build -> `get_diagnostics` -> `detect_antipatterns` -> tests -> security -> format -> diff), with the
  security phase SCOPED to `dotnet list package --vulnerable` + secrets detection only (Layers 1-2 of
  the `security-scan` skill); the OWASP / auth / CORS layers do not apply to a class library. Kit
  `code-reviewer` / `security-auditor` agents may supplement `experiment-team:reviewer` on C#-heavy
  diffs.

**Per-sweep gate (unchanged):** the existing hard gates — byte-identical golden BIM + full offline suite
+ public-API baseline unchanged + `experiment-team:reviewer` — REMAIN authoritative and unchanged; kit
`verify` phases 1-4 and 6 (build, diagnostics, antipatterns, tests, format) serve as the specialist's
pre-reviewer self-check, not a replacement for those gates.

**Specialist cadence (per subsystem):** qa (characterization tests; `testing` skill, scoped) ->
devops (infra once; `ci-cd` scoped + `get_diagnostics`) -> backend/frontend (modernize; `modern-csharp`
+ `de-sloppify`, `refactor-cleaner`/`dotnet-architect`) -> qa (verify green + byte-identical; kit
`verify` pipeline) -> reviewer (gate; + kit `code-reviewer`/`security-auditor`) -> docs (sync).

### Feature phases (Phase 1-3) — kit defaults
Kit baseline for the greenfield template work (Calendars / Calc groups / UDFs), scoped to this offline
TOM class library — not a change to the Phase 1/2/3 checklists below, just the kit framing for them.

- **All new C#**: `modern-csharp` skill (C# 14 baseline); `dotnet-architect` agent for POCO + handler
  design (e.g. `CalendarTemplateDefinition` + Engine dispatch wiring).
- **Wiring into the engine**: Serena `find_symbol` / `find_implementations` (+ Roslyn `find_callers` /
  `get_type_hierarchy`) to place each new `Class` handler in `Engine` dispatch and reuse the existing
  template hierarchy (`BaseDateTemplate<T>` etc.); Roslyn `get_public_api` as the API-baseline
  change-detector for each new public surface.
- **Tests**: `tdd` / `testing` skills, scoped — red-green + xUnit v3 + AAA + `FakeTimeProvider` for
  deterministic date/time fixtures (esp. Calendars) + Verify-style golden BIM snapshots; NOT
  WebApplicationFactory / Testcontainers / HTTP.
- **Gate**: kit `verify` (phases 1-4, 6) as the pre-reviewer self-check; `code-reviewer` /
  `security-auditor` may supplement the mandatory `experiment-team:reviewer` gate. The existing hard
  rules stay authoritative — additive JSON config, byte-identical golden BIM, opt-in-only live-server
  tests.
- **Out of scope** (same as Phase M): `api-designer`, `ef-core-specialist`, `ci-cd` deploy / NuGet-push
  YAML, and the web/OWASP/auth/CORS layers of `security-scan` — no HTTP/EF/web surface here.

### Phase 1 — Calendars (not started)
- [ ] backend: CalendarTemplateDefinition POCO + ApplyCalendarTemplate handler in Engine dispatch +
      additive config/TemplateEntry extension + idempotency via SQLBI_Template annotation.
      (Resolve the Calendar-binding RISK decision first.)
- [ ] qa: Calendar fixtures + offline assertions + opt-in live-server test
- [ ] docs: document new Class + JSON schema fields
- [ ] reviewer gate

### Phase 2 — Calculation groups (not started): backend -> qa + docs -> reviewer
### Phase 3 — User-defined functions (not started): backend -> qa + docs -> reviewer (revisit compat level)

## Phase M — locked decisions (2026-07-01)
All five decisions below are LOCKED by the user. Phase M remains PLANNED/under user review — nothing in
Stage 0-4 has started execution as a result of locking these.

1. **Warnings-as-errors: YES, CI-only.** Treat warnings as errors in the CI build, not necessarily in
   local dev builds. Stage 1 wires this into the CI pipeline(s).
2. **File-scoped namespaces + primary constructors: APPROVED as house style.** Stage 2 converts all 61
   library files to file-scoped namespaces and uses primary constructors where they reduce boilerplate.
3. **`required` migration + MAJOR VERSION BUMP: APPROVED.** Stage 2 converts the 4 `= default!` non-null
   members (e.g. `Level.Column`) to `required`. This is a source-breaking change for NuGet consumers and
   will ship under a major version bump.
4. **Public API: OPEN TO IMPROVEMENT; breaking changes ACCEPTABLE.** There is no published Web API
   surface to preserve; consumers of the `Dax.Template` NuGet package may need to adapt on the next
   major version, which is acceptable. Consequence for Stage 0: the public-API baseline test is RETAINED
   but REFRAMED — it is a change-detector to surface intended vs. accidental public-surface changes for
   review in each PR, NOT a hard freeze/gate.
5. **Coverage target (confirmed alternative to a flat 100%):**
   - CI-enforced floor of **80% line coverage on the core library `Dax.Template` only** (exclude
     `Dax.Template.TestUI` from the metric).
   - **~90% on the refactor-target subsystems**: `Tables`, `Measures`, `Model`, `Extensions` (dependency
     sort), and `Engine`/`Package` dispatch.
   - **Justified, attributed exclusions** for live-server-only branches (can't run in offline CI) and
     generated/trivial members (DTO/`ToString`/`Reset` boilerplate), so the percentage reflects reachable
     code.
   - **Add Stryker.NET mutation testing** on the 2-3 highest-risk subsystems as a stronger
     refactor-safety signal than raw line coverage (pairs with the byte-identical golden-file gate).
   - 100% remains aspirational for the core transformation logic; the CI floor may be raised (e.g. to
     85%) once Stage 0 reveals the real baseline.

## Open questions for the user
1. Calendar column-binding approach: TMDL/JSON injection (preferred) vs reflection?
2. Resume the test harness solo, or first get the experiment-team specialist subagents reachable?

## Environment / delegation note (CORRECTED 2026-06-28)
SYMPTOM: invoking a specialist (e.g. `Agent(reviewer)`) fails with "Agent type 'reviewer' not found.
Available agents:" (empty list). The lead loads fine, but its team is invisible to it.

PRIOR ROOT CAUSE WAS WRONG. The earlier note blamed git worktrees / project-vs-user plugin scope. That
theory is DISPROVEN — re-verified 2026-06-28 directly in the MAIN checkout (NOT a worktree:
`git --git-dir` and `--git-common-dir` both = `.git` real dir):
- `installed_plugins.json` has experiment-team@my-claude-teams at `scope: project`, bound to exactly
  `C:\Users\MarcoRusso\source\repos\sql-bi\DaxTemplate`, version 0.5.0.
- All 7 agent files exist in `.../cache/my-claude-teams/experiment-team/0.5.0/agents/`
  (reviewer/backend/frontend/qa/devops/docs/experiment-lead) with valid frontmatter (`name:`, `model:`,
  `tools:`). So install / scope / worktree / frontmatter are all FINE.
- Yet the `Agent` tool registry is EMPTY. Delegation fails in the main repo too — location is irrelevant,
  and a restart never helps.

ACTUAL ROOT CAUSE: the `experiment-lead` is being run AS A SUBAGENT itself, and Claude Code enforces a
shallow delegation tree — a subagent cannot spawn further subagents. So when the lead is entered as the
active agent (e.g. via `--agent experiment-lead` / agent switch), the platform never populates the
child-agent registry, and every `Agent(<specialist>)` resolves to "not found". The lead's
`tools: Agent(backend)...Agent(reviewer)` allow-list is necessary but NOT sufficient — it only takes
effect when the lead runs at the TOP LEVEL.

FIX: the lead's coordinating behavior must belong to the TOP-LEVEL (main) agent, which IS allowed to spawn
the specialist subagents. Do NOT launch the session with `experiment-lead` pre-selected as the driver
(that demotes it to a subagent). Recommended, repo-local, auto-on-start approach: put the lead's
coordinating instructions in the project `CLAUDE.md` (or a project-scoped output style), keep the
specialists as plugin subagents, and let the normal top-level agent coordinate + delegate. Verify after
launch with a trivial probe (a one-line `reviewer` invocation) BEFORE relying on delegation.
andrej-karpathy-skills is only a dependency of experiment-team and exposes one on-demand skill
(`karpathy-guidelines`); it is never auto-injected, so it was not applied during Phase 0.

## Working-tree state
Phase 0 is COMMITTED as `972c8cc` on `add-calendar` (and `claude/magical-wilbur-213ce1` points at the same
commit). Not pushed. Commit contents: csproj bump, Engine.cs guard, ApplyTemplatesGoldenTests.cs,
Infrastructure/ (OfflineModelFixture, GoldenFile, LiveServerFactAttribute), _data/Golden/Config-01 - Standard.bim,
_data/Templates/HolidaysDefinition.json.
Main checkout (add-calendar) still has uncommitted, INDEPENDENT WIP: `.gitignore` (+ `.serena/`) and untracked
`.claude/`. Leave or commit separately as you see fit — unrelated to Phase 0.
Worktrees on the tree: main=add-calendar (@972c8cc), funny-blackwell-7789aa (@32b86b0, stale), magical-wilbur-213ce1
(@972c8cc). The funny-blackwell worktree is now behind; prune it if unused.

## Next session — start here
1. (Optional) Push `add-calendar` if you want it on the remote.
2. Phase M is now the immediate next work, BEFORE resolving the Calendar-binding question or starting
   Phase 1. Start with Stage 0 (test hardening): P0 public-API baseline + P0 coverage baseline first,
   then the P1/P2 characterization tests listed under "Phase M" above.
3. The "Phase M — locked decisions" (warnings-as-errors, file-scoped namespaces + primary constructors,
   `required` migration, public-API scope, coverage threshold) are now LOCKED — proceed straight to
   Stage 1 (style/analyzer infrastructure) once Stage 0 exits.
4. Once Phase M reaches its Stage 4 closeout, resolve Open Question #1 (Calendar column-binding:
   TMDL/JSON injection vs reflection) BEFORE Phase 1 backend.
5. Begin Phase 1 (Calendars): CalendarTemplateDefinition POCO + ApplyCalendarTemplate handler in Engine dispatch
   + additive TemplateEntry/config + idempotency via SQLBI_Template annotation. Add a Calendar golden test next
   to ApplyTemplatesGoldenTests (extend OfflineModelFixture as needed) + opt-in live-server check.
