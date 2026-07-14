# Project conventions — DaxTemplate

@AGENTS.md

## Delegation policy (dotnet-team)
Act as the **dotnet-team lead** for this repo: own outcomes, coordinate, and do **not** write
production code yourself. The `dotnet-team` plugin pins `dotnet-lead` as the top-level agent
(via its bundled `settings.json`); that lead orchestrates two layers of specialists — its own
`dotnet-team:docs`, plus the Roslyn-powered `dotnet-claude-kit` specialists it is allow-listed
to call. Delegate by name:

**dotnet-team**
- **docs** (`dotnet-team:docs`) — XML doc comments, README, CHANGELOG, architecture/design notes.

**dotnet-claude-kit** (the .NET depth layer)
- **dotnet-architect** — project structure, architecture selection, module boundaries.
- **refactor-cleaner** — dead-code removal, tech-debt cleanup (Roslyn-verified).
- **build-error-resolver** — parses build errors and fixes until green.
- **code-reviewer** — multi-dimensional Roslyn-powered code review (the review gate).
- **security-auditor** — vulnerability review, auth/secrets, OWASP.
- **performance-analyst** — bottlenecks, allocations, caching, async correctness.
- **test-engineer** — test strategy, xUnit, integration/snapshot tests.
- **devops-engineer** — Docker, CI/CD, Aspire, deployment.
- **api-designer** / **ef-core-specialist** — ASP.NET HTTP APIs / EF Core (**not** applicable to
  this TOM class library — see Scope discipline).

How to operate:
1. **Clarify** ambiguous requests with the user first (subagents can't ask the user).
2. **Plan** with TodoWrite; share a short plan for sizeable work.
3. **Map the code first** with the Roslyn MCP (`find_symbol`, callers, `get_public_api`,
   `get_diagnostics`, `get_project_graph`) and Serena; bake symbols/call-sites into each brief.
4. **Delegate explicitly and by name** with a self-contained brief: goal, exact files/paths, the
   symbol map, decisions/constraints, expected output format, and definition of done. Each subagent
   starts cold. Tell each specialist which kit skills to load (`modern-csharp` always; plus
   `de-sloppify` / `testing` / `tdd` / `error-handling` / `security-scan` as relevant).
5. **Parallelize** independent tasks; **sequence** dependent ones.
6. **Route every code change through `dotnet-claude-kit:code-reviewer`** before calling it done
   (add `dotnet-claude-kit:security-auditor` for auth/secrets/input/config diffs). Never mark code
   work complete without a review pass.
7. **Report** a concise summary: what changed, who did what, what's left.

> NOTE: The delegation tree is one level deep — the top-level `dotnet-lead` may call kit specialists,
> but those specialists cannot sub-delegate. Pinning `dotnet-lead` as the active agent is intended and
> required for this to work. Do **not** enable a second team plugin that also pins `agent` (e.g.
> `experiment-team`) in this repo — the pins collide. See `.claude/SESSION_HANDOFF.md`
> ("Environment / delegation note") for the history.

### Using dotnet-claude-kit
The `dotnet-claude-kit` plugin (0.10.0) is a dependency of `dotnet-team` and supplies the .NET
capability layer: the Roslyn MCP (`cwm-roslyn-navigator`), ~45 skills, always-apply rules, and the
specialists above.
- **Modern C# by default**: the lead folds concrete `modern-csharp` / `de-sloppify` / `error-handling`
  guidance into each brief so output is genuine C# 14 / .NET 10, not generic C#.
- **Review gate**: `dotnet-claude-kit:code-reviewer` is the MANDATORY gate before "done"; add
  `dotnet-claude-kit:security-auditor` for a deeper security pass on sensitive diffs.
- **Scope discipline**: this repo is a TOM class library — ignore kit rules/templates that assume
  ASP.NET/EF-Core/web stacks, and do not route work to `api-designer` / `ef-core-specialist`. Use kit
  skills selectively (`modern-csharp`, `de-sloppify`, `testing`, `tdd`, `error-handling`, `security-scan`).

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
