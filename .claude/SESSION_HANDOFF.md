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

### Phase 1 — Calendars (not started)
- [ ] backend: CalendarTemplateDefinition POCO + ApplyCalendarTemplate handler in Engine dispatch +
      additive config/TemplateEntry extension + idempotency via SQLBI_Template annotation.
      (Resolve the Calendar-binding RISK decision first.)
- [ ] qa: Calendar fixtures + offline assertions + opt-in live-server test
- [ ] docs: document new Class + JSON schema fields
- [ ] reviewer gate

### Phase 2 — Calculation groups (not started): backend -> qa + docs -> reviewer
### Phase 3 — User-defined functions (not started): backend -> qa + docs -> reviewer (revisit compat level)

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
2. Resolve Open Question #1 (Calendar column-binding: TMDL/JSON injection vs reflection) BEFORE Phase 1 backend.
3. Begin Phase 1 (Calendars): CalendarTemplateDefinition POCO + ApplyCalendarTemplate handler in Engine dispatch
   + additive TemplateEntry/config + idempotency via SQLBI_Template annotation. Add a Calendar golden test next
   to ApplyTemplatesGoldenTests (extend OfflineModelFixture as needed) + opt-in live-server check.
