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

### Added

- `HierarchyTabularReferenceTests`, covering the hierarchy/level back-reference contract
  fixed above, level ordinal ordering, column binding on levels, and `Reset()` behavior
  for hierarchies and levels.
