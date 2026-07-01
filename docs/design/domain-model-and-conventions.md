# Domain model & conventions

## `Model/*` — the template's own object model

Before code touches TOM, templates build an intermediate, template-agnostic object graph under [src/Dax.Template/Model/](../../src/Dax.Template/Model/):

- `EntityBase` (`Model/EntityBase.cs`) — abstract base for every generated entity: `Name`, `Description`, an abstract `Reset()` (clears internal TOM back-references — see [table-generation.md](table-generation.md)), and a `ToString()` override for debugging.
- `Column` / `DateColumn` (`Model/Column.cs`, `Model/DateColumn.cs`) — a generated column: expression, data type/category, format string, display folder, hidden/temporary/key flags, `Dependencies` (for dependency ordering, see below), and the internal `TabularColumn` back-reference.
- `Hierarchy` / `Level` (`Model/Hierarchy.cs`, `Model/Level.cs`) — a generated hierarchy and its levels.
- `Measure` (`Model/Measure.cs`) — a generated measure: expression, `DaxReference`, format string, display folder, annotations.
- `ModelChanges` (`Model/ModelChanges.cs`) — the diff type produced by `Engine.GetModelChanges` (see [apply-templates-lifecycle.md](apply-templates-lifecycle.md)); also used internally to build a data preview (`GetPreviewData`/`PopulatePreview`) of a table's calculated expression.

## Additive-JSON rule

Template configuration is JSON, deserialized into `ITemplates`/`TemplateConfiguration` and the various `*TemplateDefinition`/`CustomTemplateDefinition` POCOs (nullable, defaulted properties).
**Changes to these JSON shapes must be purely additive**: existing template JSON files must keep deserializing and behaving the same way after a change.
This is why config classes favor optional, nullable, defaulted properties over required ones — see `ITemplates.TemplateEntry` (`Interfaces/ITemplates.cs`) and `TemplateConfiguration` (`Tables/TemplateConfiguration.cs`) for the pattern to follow when adding new configuration surface.

## The `Syntax/` DAX expression subsystem

Calculated-table and measure templates don't concatenate DAX strings by hand; they build a small expression graph under [src/Dax.Template/Syntax/](../../src/Dax.Template/Syntax/):

- `DaxBase` / `DaxElement` — base types for anything that can appear as a named DAX expression.
- `DaxStep` — one step of a multi-step calculated-table expression (e.g. a `VAR`/`ADDCOLUMNS` stage).
- `Var` (abstract) / `VarGlobal` / `VarRow` / `VarScope` — DAX variables scoped either globally to the table expression or per-row; `Var` implements `IDependencies<DaxBase>`, `IDaxName`, `IDaxComment`.
- `IDependencies<T>`, `IGlobalScope`, `IDaxName`, `IDaxComment` — the contracts the dependency-sort and code-generation machinery below operate against.

## Dependency resolution & topological sort

Expressions reference each other by name (`__VarName` or `[ColumnName]` tokens).
Three extension methods under [src/Dax.Template/Extensions/](../../src/Dax.Template/Extensions/) turn that into a safe generation order:

- `ComputeDependencies.cs` (`AddDependenciesFromExpression`) — scans each element's `Expression` text with a regex, resolves referenced tokens against the set of known `IDaxName` elements, and throws `InvalidVariableReferenceException` for an unresolved reference.
- `GetDependencies.cs` (`GetDependencies`) — walks an item's `Dependencies` graph.
- `TSort.cs` (`TSort`) — topologically sorts elements by dependency, assigning each a nesting "level" (used to decide which `VAR`s belong at which step of the generated DAX); it detects and reports cycles via `CircularDependencyException`.
- `GetScanColumns.cs` (`GetScanColumns`) — given an `IScanConfig` (`OnlyTablesColumns`/`ExceptTablesColumns`/`AutoScan`), finds the model columns to consider for auto-detection (e.g. the min/max date range for `MeasuresTemplate`, or the date columns for the date-table templates' `AutoScanEnum`-driven year-range detection).

`AutoScanEnum` (`Enums/AutoScanEnum.cs`, `[Flags]`) controls *how* columns are auto-detected (`Disabled`, `SelectedTablesColumns`, `ScanActiveRelationships`, `ScanInactiveRelationships`, `Full`).
`AutoNamingEnum` (`Enums/AutoNamingEnum.cs`) controls whether generated measure names use a `Suffix` or `Prefix` naming style.

## Constants & exceptions

- `Constants/Attributes.cs` — well-known TOM annotation names, including `SQLBI_TEMPLATE_ATTRIBUTE = "SQLBI_Template"` (see [measures.md](measures.md) for its idempotency role).
- `Constants/Prefixes.cs` — string prefixes used when a template needs to rename a conflicting existing object out of the way (`CONFLICT_RENAME_PREFIX = "_old"`).
- `Exceptions/` — typed exceptions raised by the subsystems above: `CircularDependencyException`, `InvalidVariableReferenceException`, `InvalidMacroReferenceException`, `InvalidAttributeException`, `InvalidConfigurationException`, `ExistingTableException`, `TemplateException`.
