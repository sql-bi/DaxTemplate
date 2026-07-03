# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **BREAKING (next release: 2.0.0):** `EntityBase.Name` (and therefore `Column.Name`,
  `DateColumn.Name`, `Hierarchy.Name`, `Level.Name`, `Measure.Name`), `Level.Column`,
  `Var.Name` (and therefore `VarGlobal.Name`, `VarRow.Name`), and
  `DaxStep.Name` are now `required` members. This is source-breaking for consumers who
  construct these types via object initializers without setting these properties; it is
  behavior-preserving at runtime and does not affect JSON template configuration, which is
  unaffected (these types are never deserialized from JSON).
- Target framework is now **.NET 10** only; the package no longer targets `net6.0` or `net8.0`.
  **This is a breaking change** for consumers building against those older target frameworks.
- Language version raised to **C# 14**; build/SDK pinned to **.NET SDK 10**.
- **BREAKING (next release: 2.0.0):** public API naming cleanup (Roslyn CA1707/CA1711/CA1716/CA1725) —
  identifiers only; the underlying string/enum-member values, emitted DAX/BIM output, and JSON template
  configuration are all unaffected.
  - De-underscored public constants to PascalCase, e.g. `Attributes.SQLBI_TEMPLATE_ATTRIBUTE` →
    `Attributes.SqlbiTemplate` (and its 7 siblings), `Prefixes.CONFLICT_RENAME_PREFIX` →
    `Prefixes.ConflictRenamePrefix`, `Package.TEMPLATE_FILE_EXTENSION` / `PACKAGE_CONFIG` →
    `Package.TemplateFileExtension` / `PackageConfig`, `MeasureTemplateBase.ENTITY_*` →
    `MeasureTemplateBase.Entity*`, `BaseDateTemplate<T>.DATACATEGORY_TIME` / `ANNOTATION_CALENDAR_TYPE` →
    `DataCategoryTime` / `AnnotationCalendarType`, `TableTemplateBase.ANNOTATION_ATTRIBUTE_TYPE` →
    `AnnotationAttributeType`. Assigned string values are byte-identical.
  - Dropped the `Enum` suffix from enum types: `Enums.AutoNamingEnum` → `Enums.AutoNaming`,
    `Enums.AutoScanEnum` → `Enums.AutoScan` (the `AutoNaming`/`AutoScan` config properties now share
    their name with their enum type, which is legal C# and compiles/runs unchanged),
    `HolidaysDefinitionTable.SubstituteEnum` → `HolidaysDefinitionTable.Substitute`. Enum member names
    and values are unchanged, so JSON template configuration (which binds enum members by name) is
    unaffected.
  - `CustomTemplateDefinition.Step` (nested type) → `CustomTemplateDefinition.TemplateStep`, to avoid
    the reserved-keyword name `Step`. The unrelated `Column.Step` JSON-bound string property keeps its
    name.
  - `CustomTableTemplate<T>.GetColumns`/`InitTemplate` (and the `CustomDateTable.InitTemplate`
    override) rename their `template` parameter to `templateDefinition`, to avoid the reserved-keyword
    name `template`.
  - `BaseDateTemplate<T>.ApplyTemplate`'s `dateTable` parameter is renamed to `tabularTable` to match
    the base `TableTemplateBase.ApplyTemplate` signature.
- **BREAKING (next release: 2.0.0):** the 4 remaining visible instance fields flagged by Roslyn
  CA1051 (do not declare visible instance fields) are now properties: `MeasureTemplateBase.Template`
  (`protected`, get-only), `TableTemplateBase.FixRelationshipsTo` / `FixRelationshipsFrom` (`protected`,
  get/set), and `Translations.LanguageDefinitions` (`protected`, get/set). This is source/binary-breaking
  for subclasses that referenced these as fields (e.g. via `ref`/`out`, though no such usage exists in
  this codebase); read/write access from subclasses is otherwise unaffected. No runtime behavior, emitted
  DAX/BIM output, or JSON template configuration is affected — these hold template-build state, not
  JSON-deserialized or emitted values.
- **BREAKING (next release: 2.0.0):** exception constructor parameter renames (identifier-only; messages
  and runtime behavior are otherwise unchanged): `daxExpressionmessage` → `daxExpression` on
  `CircularDependencyException`, `InvalidVariableReferenceException`, and all three
  `InvalidMacroReferenceException` constructors; `entitymessage` → `entityName` on
  `InvalidAttributeException`. Also fixes a message typo in `CircularDependencyException`: "Circulare
  dependency" → "Circular dependency".
- **BREAKING (next release: 2.0.0):** `Extensions.ReflectionHelper` (and its `GetPropertyValue`/
  `SetPropertyValue` extension methods) is now `internal` (was `public`) and removed from the public API
  surface — it is TOM-internal reflection plumbing used by `Engine.GetModelChanges`; test code reaches it
  via `InternalsVisibleTo`. `PublicApi.txt` was regenerated accordingly.

### Fixed

- `TableTemplateBase.AddHierarchies` now correctly links the internal back-references
  used to track hierarchies and levels: `Hierarchy.TabularHierarchy` is assigned to the
  `TabularHierarchy` instance actually added to the model (previously it was never set
  and stayed `null`), and `Level.TabularLevel` is assigned to the `TabularLevel` instance
  actually added to the model (previously it referenced an orphaned object not present in
  the model). The generated/serialized model output (BIM) is unchanged; this fix corrects
  internal state that consumers of `Hierarchy`/`Level` rely on.
- Removed a redundant, no-op `Description` re-assignment in `CustomTableTemplate` when
  building hierarchies.
- `ApplyHolidaysDefinitionTable` no longer leaves a phantom empty table in the model when
  `TemplateEntry.Template` is blank — the `InvalidConfigurationException` validation now runs
  before the target table is found-or-created, instead of after.
- `CustomTableTemplate<T>.GetHierarchies` now throws a descriptive `TemplateException` naming the
  hierarchy, level, and unknown column (e.g. `Hierarchy 'X' level 'Y' references unknown column
  'Z'`) instead of a bare `InvalidOperationException` when a hierarchy level references a column
  that doesn't exist.
- `TableTemplateBase.AddHierarchies` now copies `Description` onto the generated TOM `Hierarchy`
  and `Level` objects (previously silently dropped). No shipped template configures hierarchy/level
  descriptions today, so emitted BIM for existing configs is unchanged. (This is a distinct fix from
  the redundant no-op `Description` re-assignment removed above — that one was dead code; this one
  is a genuine behavior fix.)
- Dependency-sort cycle detection (`Extensions.TSort`) now detects multi-node (2+) cycles promptly
  via DFS recursion-path tracking, throwing `CircularDependencyException` naming the offending
  node's expression — previously a 2+ node cycle was only caught after a 1000-nested-call backstop,
  with a generic "stack overflow" message. The `MAX_NESTED_CALLS` guard was removed. Note: a valid
  but pathologically deep (>~1000 levels) *acyclic* dependency graph now fails via a CLR stack
  overflow instead of a catchable exception; no current template approaches this depth.
- A disabled `CustomDateTable` entry (`IsEnabled == false`) now removes its previously-created date
  table, and its reference table if `ReferenceTable` is configured, consistent with the
  `HolidaysDefinitionTable`/`HolidaysTable` handlers — previously it left both tables in the model.
- Generated DAX now formats year integers with `CultureInfo.InvariantCulture`
  (`BaseDateTemplate<T>.GenerateMinYearExpression`/`GenerateMaxYearExpression`/
  `GenerateCalendarExpression`), making emitted DAX locale-independent (guards against non-Latin-digit
  locales). Output on Latin-digit cultures is unchanged.

### Added

- `HierarchyTabularReferenceTests`, covering the hierarchy/level back-reference contract
  fixed above, level ordinal ordering, column binding on levels, and `Reset()` behavior
  for hierarchies and levels.
- Characterization tests for the defects fixed above were converted to fix-tests (asserting the
  corrected behavior instead of documenting the prior bug), plus a new `Dispatch-08` fixture and
  test covering the disabled-`CustomDateTable` reference-table cleanup. The offline suite now
  stands at 130 passing + 1 skipped.
