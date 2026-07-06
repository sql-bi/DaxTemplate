# Design docs — Dax.Template

Detail docs for the `Dax.Template` architecture.
These are **not** loaded automatically into an agent's context — read on demand, when you need to go deeper than [AGENTS.md](../../AGENTS.md).

- [overview.md](overview.md) — system context, package purpose, project layout, dependency direction.
- [apply-templates-lifecycle.md](apply-templates-lifecycle.md) — `Engine.ApplyTemplates` dispatch by `Class`, handlers, TOM mutation, `GetModelChanges`.
- [table-generation.md](table-generation.md) — the `Tables/` class hierarchy, columns/hierarchies/levels, the date-table branch, the `Tabular*` back-reference convention, attaching a native TOM `Calendar` to an existing table (`CalendarTemplate`), and generating a native TOM calculation-group table (`CalculationGroupTemplate`).
- [measures.md](measures.md) — `MeasuresTemplate` / `MeasureTemplateBase`, target-measure expansion, `SQLBI_Template` idempotency.
- [domain-model-and-conventions.md](domain-model-and-conventions.md) — `Model/*`, `EntityBase`, the additive-JSON rule, the `Syntax/` DAX expression subsystem and dependency-sort machinery.
- [testing.md](testing.md) — offline golden-file harness, live-server opt-in tests, `InternalsVisibleTo`.
- [coverage.md](coverage.md) — coverlet coverage configuration and baseline, CI threshold gate, Stryker.NET mutation-testing scaffold.

## Keeping these docs current

Whenever a change affects the behavior described in one of these docs, update that doc **in the same change** (see the Documentation maintenance rule in [AGENTS.md](../../AGENTS.md)).
If a doc cannot be updated right away, add a short status banner at its top instead of leaving it silently wrong.
