# Overview

## What it is

`Dax.Template` is a .NET library, published as the `Dax.Template` NuGet package, described in [src/Dax.Template/Dax.Template.csproj](../../src/Dax.Template/Dax.Template.csproj) as:

> Engine that creates DAX columns, measures, tables and calculation groups based on JSON templates.

It generates and mutates objects in a Tabular Object Model (TOM) `Microsoft.AnalysisServices.Tabular.Model` — columns, measures, calculated tables, hierarchies — driven by JSON template configuration files, rather than the consumer hand-authoring TOM/TMSL/DAX.

## System context

- The **consumer application** owns a TOM `Database`/`Model` — either connected to an Analysis Services / Power BI server, or a disconnected in-memory model (used by the offline test suite).
- The consumer loads a template configuration file (a `*.template.json`, optionally packaged with its referenced sub-templates as embedded JSON) via `Package.LoadFromFile` (see [src/Dax.Template/Package.cs](../../src/Dax.Template/Package.cs)).
- The consumer constructs an `Engine` around that `Package` and calls `Engine.ApplyTemplates(model)` to mutate the model in place.
- The consumer may then call `Engine.GetModelChanges(model)` to obtain a diff (added/removed/modified tables, columns, measures, hierarchies) — e.g. for a preview UI or a change report — before or after committing changes.
- See [apply-templates-lifecycle.md](apply-templates-lifecycle.md) for the full flow.

## Project layout & dependency direction

Solution: [src/Dax.Template.sln](../../src/Dax.Template.sln), 3 projects:

- [src/Dax.Template/](../../src/Dax.Template/) — the library (`IsPackable=true`; the shipped package).
- [src/Dax.Template.Tests/](../../src/Dax.Template.Tests/) — xUnit offline test suite.
- [src/Dax.Template.TestUI/](../../src/Dax.Template.TestUI/) — WinForms manual harness (`net10.0-windows`), not automated.

Dependency direction is one-way: `Dax.Template.Tests` and `Dax.Template.TestUI` each hold a `ProjectReference` to `Dax.Template`; `Dax.Template` itself has no dependency on either.

## Toolchain

- Single target `net10.0` (`net10.0-windows` for `Dax.Template.TestUI`), `LangVersion 14.0`.
- SDK pinned in [global.json](../../global.json) to `10.0.301` (`rollForward: latestFeature`).
- Depends on `Microsoft.AnalysisServices` / `Microsoft.AnalysisServices.AdomdClient` 19.114.0.
