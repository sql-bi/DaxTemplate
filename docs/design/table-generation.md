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

- `TableTemplateBase` (`Tables/TableTemplateBase.cs`) — shared, entity-agnostic machinery for any template applied to a TOM `Table`: `ApplyTemplate` orchestrates `AddColumns`, `AddHierarchies`, `AddAnnotations`, and `RemoveExistingElements` (columns/hierarchies no longer produced by the current template run); it also saves and restores relationships affected by a table/column rename (`SaveAffectedRelationships`/`RestoreAffectedRelationships`) and applies translations (`RenameWithTranslation`/`ApplyTranslations`).
- `CalculatedTableTemplateBase` (`Tables/CalculatedTableTemplateBase.cs`) — adds the calculated-table specifics: building the DAX table expression from the `Syntax/` step/variable model (`GetDaxTableExpression`), ISO-format handling, comment generation, and partition management (`AddPartitions`/`RemoveExistingPartitions`).
- `ReferenceCalculatedTable` (`Tables/ReferenceCalculatedTable.cs`) — supports a table whose DAX expression references a separate hidden table (`HiddenTable`/`QuotedHiddenTable`), so a shared calculation can be defined once and reused (e.g. hidden + visible date table pair).
- `CustomTableTemplate<T>` (`Tables/CustomTableTemplate.cs`) — parses a `CustomTemplateDefinition` (JSON: `Steps`, `GlobalVariables`, `RowVariables`, `Columns`, `Hierarchies`, `FormatPrefixes`) into the `Model`/`Syntax` object graph, including `GetHierarchies` for building `Model.Hierarchy`/`Level` entries.
  This is the generic engine that all date-table templates build on.
- `Tables/Dates/BaseDateTemplate<T>` — a thin date-specific specialization of `CustomTableTemplate<T>`.
- Concrete date templates: `CustomDateTable` (arbitrary user-supplied calendar template, optionally paired with a hidden reference table), `SimpleDateTable` (a built-in, non-JSON-authored calendar shape), `HolidaysTable` (a calculated table of holiday dates), `HolidaysDefinitionTable` (the raw list of holiday definitions consumed by `HolidaysTable`'s DAX expression).

## Columns, hierarchies, levels

- `Model.Column` (`Model/Column.cs`) describes one generated column: `Expression`, `DataType`, `DataCategory`, `FormatString`, `DisplayFolder`, `IsHidden`/`IsTemporary`/`IsKey`, `Dependencies` (for topological sort — see [domain-model-and-conventions.md](domain-model-and-conventions.md)), and `SortByColumn`.
- `Model.Hierarchy` (`Model/Hierarchy.cs`) has `Levels` (an ordered list of `Model.Level`), plus `DisplayFolder`/`IsHidden`.
- `Model.Level` (`Model/Level.cs`) wraps a `Model.Column` reference for one level of a hierarchy.
- `TableTemplateBase.AddColumns`/`AddHierarchies` translate these model objects into actual TOM `Column`/`Hierarchy`/`Level` objects added to the target `Table`.

## The `Tabular*` back-reference convention

Model objects keep an **internal** back-reference to the live TOM object they created: `Column.TabularColumn`, `Hierarchy.TabularHierarchy`, `Level.TabularLevel`.
This is what lets `InternalsVisibleTo`-enabled test code (and internal engine code) inspect the actual TOM object a template produced.
Every `EntityBase`-derived type implements `Reset()` (see [domain-model-and-conventions.md](domain-model-and-conventions.md)) which nulls these references out; `TableTemplateBase.ResetTabularReferences` calls `Reset()` across a table's model objects before a template is (re-)applied, so re-running a template against a model that already has the previous run's output is safe and produces a clean re-attach rather than stale references.
