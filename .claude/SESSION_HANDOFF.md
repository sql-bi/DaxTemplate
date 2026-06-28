# Session Handoff — DAX Template: new DAX entities

> Resume instructions: open this repo in Claude Code and say
> **"Read .claude/SESSION_HANDOFF.md and resume Phase 0."**
> Last updated: 2026-06-28

## Goal
Extend the Dax.Template library (creates TOM objects from JSON templates) to support three new DAX
entities, one at a time, with tests and no regressions:
1. **Calendars** (priority 1) — native TOM `Calendar`, attached to a table; extends the existing calculated-table branch
2. **Calculation groups** (priority 2)
3. **User-defined functions / UDFs** (priority 3)

## Decisions locked in
- Implementation order: **Calendars -> Calc groups -> UDFs**
- TOM upgrade to **latest released 19.114.0**, **no preview features**
- Keep `net6.0;net8.0`; drop net6 only if forced (it was NOT forced — kept)
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

## Environment / delegation note
The experiment-team@my-claude-teams specialist subagents (backend/frontend/qa/devops/reviewer/docs/
experiment-lead) are NOT invocable from worktree sessions — and a restart does NOT fix it (verified
2026-06-28 after a restart: agent probe still returned only built-ins + the user-scoped Power BI plugin
agents). ROOT CAUSE: experiment-team, brainstorm, and andrej-karpathy-skills are installed at
`"scope": "project"` bound to the MAIN repo path `c:\...\sql-bi\DaxTemplate`. Claude Code treats each
git worktree (`.claude/worktrees/*`) as a SEPARATE project, so those project-scoped plugins don't load
here. The user-scoped Power BI plugins (tabular-editor etc.) DO load. andrej-karpathy-skills is only a
dependency of experiment-team and exposes one on-demand skill (`karpathy-guidelines`) — it is never
auto-injected, so it was not applied during Phase 0.
FIX (user must do via interactive `/plugin`): enable experiment-team at USER scope (pulls in brainstorm +
andrej-karpathy-skills) so it applies across worktrees; OR run Claude Code from the main repo root; then
verify with a trivial agent probe before delegating. Until then, work solo (as Phase 0 was done).

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
