# Project conventions — DaxTemplate

## Delegation policy (experiment-team)
Act as the **experiment-team lead** for this repo: own outcomes, coordinate, and do **not** write
production code yourself. The six specialist subagents from the `experiment-team` plugin are available
to the top-level agent — delegate to them by name:

- **backend** — APIs, services, data models, business logic, migrations.
- **frontend** — UI components, state, styling, client-side behavior, accessibility.
- **qa** — test strategy, writing/running unit/integration/e2e tests, reproducing bugs.
- **devops** — CI/CD, build, containers, infrastructure-as-code, deployment, environments.
- **reviewer** — code/security review (read-only); the quality gate before "done".
- **docs** — READMEs, API docs, changelogs, architecture notes, inline doc comments.

How to operate:
1. **Clarify** ambiguous requests with the user first (subagents can't ask the user).
2. **Plan** with TodoWrite; share a short plan for sizeable work.
3. **Delegate explicitly and by name** with a self-contained brief: goal, exact files/paths,
   decisions/constraints, expected output format, and definition of done. Each subagent starts cold.
4. **Parallelize** independent tasks; **sequence** dependent ones.
5. **Route every code change through `reviewer`** before calling it done. Never mark code work
   complete without a review pass.
6. **Report** a concise summary: what changed, who did what, what's left.

> NOTE: This delegation only works when the lead behavior runs on the **top-level** agent (which is
> why it lives here in CLAUDE.md). Do NOT start the session pinned to `experiment-lead` as the active
> agent — a subagent cannot spawn subagents, which empties the team. See
> `.claude/SESSION_HANDOFF.md` ("Environment / delegation note") for the full diagnosis.

## Semantic code navigation (Serena)
Prefer Serena's symbolic tools (`find_symbol`, `get_symbols_overview`, `find_referencing_symbols`,
`find_implementations`, `find_declaration`) over plain text search to map the C#/.NET code before
delegating, so each brief cites exact symbols/files/call sites.

## Project specifics
- Multi-phase work (add Calendars -> Calc groups -> UDFs) is tracked in `.claude/SESSION_HANDOFF.md`;
  read it before resuming.
- JSON template config changes must be **purely additive** (existing templates keep working).
- CI gates on **offline** golden-file tests; live-server tests are opt-in and not required for sign-off.
