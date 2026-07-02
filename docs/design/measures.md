# Measures

Code: [src/Dax.Template/Measures/MeasuresTemplate.cs](../../src/Dax.Template/Measures/MeasuresTemplate.cs) and [src/Dax.Template/Measures/MeasureTemplateBase.cs](../../src/Dax.Template/Measures/MeasureTemplateBase.cs).

## Purpose

`MeasuresTemplate` generates a family of derived measures (typically time-intelligence variants — YTD, previous period, etc.) from a JSON `MeasuresTemplateDefinition` and a set of **target measures** already present in the model, without hand-writing DAX for each combination.

## Flow (`MeasuresTemplate.ApplyTemplate`)

1. Find every existing measure carrying the current template's `SQLBI_Template` annotation value (`GetSqlbiTemplateValue`) — these are candidates for cleanup at the end.
2. If the entry is disabled (`isEnabled == false`), remove all of those measures and return.
3. Resolve `targetMeasures` (`GetTargetMeasures`) — the measures the template will be applied to — and the destination `Table` (`GetTargetTable`).
4. For each `MeasureTemplate` in `Template.MeasureTemplates` marked `IsSingleInstance`, generate one measure not tied to any target measure (applied once, to `TableSingleInstanceMeasures` or the target table).
5. For every other `MeasureTemplate`, generate one derived measure **per target measure** (`GetTargetMeasureName` composes the new name), via the local `ApplyMeasureTemplate` closure, which builds a `MeasureTemplateBase` (macro-substituted `Expression`, `DisplayFolder` via `GetDisplayFolder`'s placeholder rules, `Comments`, `Annotations`) and calls its own `ApplyTemplate(model, targetTable, overrideExistingMeasures, cancellationToken)`.
6. If `overrideExistingMeasures` (default `true`), any previously-generated measure with the same `SQLBI_Template` annotation that was **not** re-produced in this run is removed — this is the orphan cleanup.

## Idempotency: the `SQLBI_Template` annotation

`Constants/Attributes.cs` defines `SqlbiTemplate = "SQLBI_Template"`.
Every measure `MeasuresTemplate` generates carries this annotation (value identifies the specific template).
On each run, the template:

- looks up all measures already tagged with its own annotation value,
- regenerates the full expected set,
- removes any previously-tagged measure that the current run did not regenerate.

This makes re-applying a `MeasuresTemplate` entry (e.g. after editing the JSON, or after a target measure was renamed/removed) safe and repeatable: the net result always matches the current template + current target measures, with no leftover measures from earlier configurations.

## Placeholders

`MeasuresTemplate` supports macro placeholders resolved via regex substitution before the DAX expression is handed to `MeasureTemplateBase`: `@_MEASURE_@`, `@_TEMPLATE_@`, `@_MEASUREFOLDER_@`, `@_TEMPLATEFOLDER_@` (used in `GetDisplayFolder`), plus `ReplaceMacros` for the expression body itself (e.g. min/max date placeholders via `regexGetMinDates`/`regexGetMaxDates`).
