# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
