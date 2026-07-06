# Table generation

All table templates live under [src/Dax.Template/Tables/](../../src/Dax.Template/Tables/) and [src/Dax.Template/Tables/Dates/](../../src/Dax.Template/Tables/Dates/).

## Class hierarchy

```
TableTemplateBase (abstract)
  CalculatedTableTemplateBase (abstract)
    HolidaysDefinitionTable                       -- direct: fixed JSON shape (list of HolidayLine), not Steps-driven
    ReferenceCalculatedTable
      CustomTableTemplate<T> : ICustomTableConfig  -- generic JSON-driven DAX table builder (Steps/Vars/Columns/Hierarchies)
        BaseDateTemplate<T> : IDateTemplateConfig
          CustomDateTable  : IDateTemplateConfig
          SimpleDateTable  : SimpleDateTemplateConfig
          HolidaysTable    : IHolidaysConfig
```

- `TableTemplateBase` (`Tables/TableTemplateBase.cs`) — shared, entity-agnostic machinery for any template applied to a TOM `Table`: `ApplyTemplate` orchestrates `AddColumns`, `AddHierarchies`, `AddAnnotations`, and `RemoveExistingElements` (columns/hierarchies no longer produced by the current template run); it also saves and restores relationships affected by a table/column rename (`SaveAffectedRelationships`/`RestoreAffectedRelationships`) and applies translations (`RenameWithTranslation`/`ApplyTranslations`). `AddAnnotations` shares its add-or-update logic with `Measures/MeasureTemplateBase` via the internal `Extensions/AnnotationCollectionExtensions.UpsertAnnotations` helper (de-duplicated, no behavior change).
- `CalculatedTableTemplateBase` (`Tables/CalculatedTableTemplateBase.cs`) — adds the calculated-table specifics: building the DAX table expression from the `Syntax/` step/variable model (`GetDaxTableExpression`), ISO-format handling, comment generation, and partition management (`AddPartitions`/`RemoveExistingPartitions`).
- `ReferenceCalculatedTable` (`Tables/ReferenceCalculatedTable.cs`) — supports a table whose DAX expression references a separate hidden table (`HiddenTable`/`QuotedHiddenTable`), so a shared calculation can be defined once and reused (e.g. hidden + visible date table pair).
- `CustomTableTemplate<T>` (`Tables/CustomTableTemplate.cs`) — parses a `CustomTemplateDefinition` (JSON: `Steps`, `GlobalVariables`, `RowVariables`, `Columns`, `Hierarchies`, `FormatPrefixes`) into the `Model`/`Syntax` object graph, including `GetHierarchies` for building `Model.Hierarchy`/`Level` entries. `GetHierarchies` throws a descriptive `TemplateException` (naming the hierarchy, level, and column) when a hierarchy level's `Column` references a column the template doesn't define.
  This is the generic engine that all date-table templates build on.
- `Tables/Dates/BaseDateTemplate<T>` — a thin date-specific specialization of `CustomTableTemplate<T>`. Its year-range DAX generation (`GenerateMinYearExpression`/`GenerateMaxYearExpression`/`GenerateCalendarExpression`) formats year integers with `CultureInfo.InvariantCulture`, so emitted DAX is locale-independent (guards against non-Latin-digit locales).
- Concrete date templates: `CustomDateTable` (arbitrary user-supplied calendar template, optionally paired with a hidden reference table), `SimpleDateTable` (a built-in, non-JSON-authored calendar shape), `HolidaysTable` (a calculated table of holiday dates), `HolidaysDefinitionTable` (the raw list of holiday definitions consumed by `HolidaysTable`'s DAX expression).

## Columns, hierarchies, levels

- `Model.Column` (`Model/Column.cs`) describes one generated column: `Expression`, `DataType`, `DataCategory`, `FormatString`, `DisplayFolder`, `IsHidden`/`IsTemporary`/`IsKey`, `Dependencies` (for topological sort — see [domain-model-and-conventions.md](domain-model-and-conventions.md)), and `SortByColumn`.
- `Model.Hierarchy` (`Model/Hierarchy.cs`) has `Levels` (an ordered list of `Model.Level`), plus `DisplayFolder`/`IsHidden`.
- `Model.Level` (`Model/Level.cs`) wraps a `Model.Column` reference for one level of a hierarchy.
- `TableTemplateBase.AddColumns`/`AddHierarchies` translate these model objects into actual TOM `Column`/`Hierarchy`/`Level` objects added to the target `Table`. `AddHierarchies` also copies `Description` onto the generated TOM `Hierarchy`/`Level` objects; no shipped template currently sets a hierarchy/level `Description`, so emitted BIM for existing configs is unchanged.

## The `Tabular*` back-reference convention

Model objects keep an **internal** back-reference to the live TOM object they created: `Column.TabularColumn`, `Hierarchy.TabularHierarchy`, `Level.TabularLevel`.
This is what lets `InternalsVisibleTo`-enabled test code (and internal engine code) inspect the actual TOM object a template produced.
Every `EntityBase`-derived type implements `Reset()` (see [domain-model-and-conventions.md](domain-model-and-conventions.md)) which nulls these references out; `TableTemplateBase.ResetTabularReferences` calls `Reset()` across a table's model objects before a template is (re-)applied, so re-running a template against a model that already has the previous run's output is safe and produces a clean re-attach rather than stale references.

## Calendars

`Tables/Calendars/CalendarTemplate` (+ `CalendarTemplateDefinition`) is not part of the class hierarchy above: it doesn't generate a table. It attaches a native TOM `Calendar` — and its `CalendarColumnGroups` — to a table some other template already created, using the public typed TOM Calendar API (`TimeUnitColumnAssociation`/`TimeRelatedColumnGroup`); no reflection, no TMSL. It is dispatched from the `CalendarTemplate` `Class` in `Engine.ApplyTemplates` (see [apply-templates-lifecycle.md](apply-templates-lifecycle.md)), which finds (but never creates) the target `Table` by `TemplateEntry.Table` and reads a `CalendarTemplateDefinition` from `TemplateEntry.Template`.

### JSON schema

The sub-template file referenced by `TemplateEntry.Template` has the shape:

```json
{
  "Name": "Calendar",
  "Description": "...",
  "ColumnGroups": [
    { "Type": "TimeUnit", "TimeUnit": "Year", "PrimaryColumn": "Year", "AssociatedColumns": [ "..." ] },
    { "Type": "TimeRelated", "Columns": [ "Day of Week" ] }
  ]
}
```

- `Name` (required) — the `Calendar.Name` to create or update on the target table; also the idempotency key (see below).
- `Description` (optional) — copied onto `Calendar.Description`.
- `ColumnGroups[]` — each entry is discriminated by `Type`:
  - `"TimeUnit"` → a `TimeUnitColumnAssociation`, built from `TimeUnit` (required; the TOM `Microsoft.AnalysisServices.Tabular.TimeUnit` enum, e.g. `Year`, `MonthOfYear`, `Date` — bound via `JsonStringEnumConverter`), `PrimaryColumn` (required column name), and optional `AssociatedColumns` (column names).
  - `"TimeRelated"` → a `TimeRelatedColumnGroup`, built from `Columns` (column names).
  - Any other `Type` value throws `InvalidConfigurationException`.
- All column names (`PrimaryColumn`, `AssociatedColumns`, `Columns`) are resolved against `targetTable.Columns` at apply time; an unresolved name throws `TemplateException`. A missing/blank `PrimaryColumn` or `Columns`/`AssociatedColumns` entry throws `InvalidConfigurationException`.

Example: `src/Dax.Template.Tests/_data/Templates/Calendar-Standard.json`.

### Compatibility level

TOM requires database compatibility level **>= 1701** to add a `Calendar` to a table — it throws `CompatibilityViolationException` at `Table.Calendars.Add(...)` itself, before `Model.Validate()` runs. On the enabled path, `CalendarTemplate.ApplyTemplate` first guards that the table is attached to a model with a database (`targetTable.Model?.Database`, otherwise `InvalidConfigurationException`), then checks `CompatibilityLevel` up front and throws a template-specific `InvalidConfigurationException` instead of surfacing the raw TOM exception. (The offline test harness uses a dedicated compat-1701 fixture, `CalendarOfflineModelFixture`, so the compat-1600 fixture used by every other golden test stays untouched.)

### Idempotency and its limitation

A `Calendar` has no `Annotations`, so the usual `SQLBI_Template`-annotation convention (see [measures.md](measures.md)) doesn't apply. Instead, `CalendarTemplate.ApplyTemplate` keys off `Calendar.Name`: it looks up `targetTable.Calendars.Find(Definition.Name)` and either creates a new `Calendar` or clears and rebuilds the existing one's `CalendarColumnGroups`. `IsEnabled: false` removes the named calendar and returns without creating anything.

**Known limitation:** because there is no provenance tag, renaming or deleting a `CalendarTemplate` entry between runs leaves the previously-created calendar in the model — the engine has no way to identify it as orphaned on a later run. This is the same class of gap as the `CustomDateTable` table-rename TODO and `MeasuresTemplate`'s entry-deletion behavior; a provenance-tracking fix is deferred to a later phase.
