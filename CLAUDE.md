# Project conventions — DaxTemplate

@AGENTS.md

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

### Using dotnet-claude-kit
The `dotnet-claude-kit` plugin (0.10.0) is project-scoped alongside `experiment-team`. Because the
delegation tree is one level deep — a subagent cannot spawn a subagent — **experiment-team specialists
cannot themselves call kit agents**; all cross-plugin composition happens at the top-level lead.

- **Modern C# by default**: keep coordinated feature work on `experiment-team:*` specialists, but the
  lead folds concrete `modern-csharp` / `error-handling` / `de-sloppify` guidance into each brief so
  output is genuine C# 14 / .NET 10, not generic C#.
- **Direct kit delegation**: for .NET-idiom-heavy or modernization tasks — refactor-to-modern-C#,
  performance, build-error triage — the lead MAY delegate straight to the matching
  kit agent (`refactor-cleaner`, `performance-analyst`, `build-error-resolver`, `dotnet-architect`).
  Pick the cheapest capable specialist. (The kit's `ef-core-specialist` / `api-designer` target
  EF Core / ASP.NET HTTP APIs and do **not** apply to this TOM class library — see Scope discipline.)
- **Review gate unchanged**: `experiment-team:reviewer` remains the MANDATORY gate before "done". For
  C#-heavy diffs the lead MAY add the kit's Roslyn-powered `code-reviewer` / `security-auditor` as a
  deeper pass and reconcile findings with `reviewer`'s.
- **Scope discipline**: ignore kit rules/templates that assume ASP.NET/EF-Core/web stacks — this repo
  is a TOM class library. Use kit skills selectively (`modern-csharp`, `de-sloppify`, `testing`, `tdd`,
  `error-handling`, `security-scan`).

## Semantic code navigation (Serena + Roslyn Navigator)
Serena remains the primary tool for editing, cross-file work, and project memories: prefer its
symbolic tools (`find_symbol`, `get_symbols_overview`, `find_referencing_symbols`,
`find_implementations`, `find_declaration`) over plain text search to map the C#/.NET code before
delegating, so each brief cites exact symbols/files/call sites.

The `cwm-roslyn-navigator` MCP server (from `dotnet-claude-kit`) is complementary: prefer it for deep
C#-semantic queries the LSP handles less precisely — call graphs (`find_callers`, `find_overrides`),
type hierarchy (`get_type_hierarchy`), compiler/analyzer diagnostics (`get_diagnostics`), and
anti-pattern / dead-code / circular-dependency detection (`detect_antipatterns`, `find_dead_code`,
`detect_circular_dependencies`). The lead uses both to map code before delegating and bakes the
findings into each brief.

## Project specifics
- Multi-phase work (add Calendars -> Calc groups -> UDFs) is tracked in `.claude/SESSION_HANDOFF.md`;
  read it before resuming.
- JSON template config changes must be **purely additive** (existing templates keep working).
- CI gates on **offline** golden-file tests; live-server tests are opt-in and not required for sign-off.
- Kit hooks: `post-edit-format` (auto `dotnet format` on `.cs` edits) is **DISABLED** for this repo —
  do not rely on auto-format; run `dotnet format` manually or let `reviewer` flag style. `pre-bash-guard`
  (blocks force-push / `reset --hard` / `clean -f` / `checkout .` / non-allowlisted `rm -rf`) and
  `post-scaffold-restore` (`dotnet restore` after `.csproj` edits) remain **active**.
