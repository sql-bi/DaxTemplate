# Functions

Code: [src/Dax.Template/Functions/FunctionLibraryTemplate.cs](../../src/Dax.Template/Functions/FunctionLibraryTemplate.cs) and [src/Dax.Template/Functions/FunctionLibraryTemplateDefinition.cs](../../src/Dax.Template/Functions/FunctionLibraryTemplateDefinition.cs).

## Purpose

`FunctionLibraryTemplate` generates DAX **user-defined functions (UDFs)** directly onto `Model.Functions`. It is the only template class in this library that is **model-level**: every other `Class` (dates, holidays, measures, calendars, calculation groups) targets a TOM `Table`, but a `Function` attaches to the `Model` itself, so `FunctionLibraryTemplate` never finds or creates a table (`TemplateEntry.Table` is unused). One sub-template file is a **library** declaring one or more functions.

It is dispatched from the `FunctionLibraryTemplate` `Class` in `Engine.ApplyTemplates` (see [apply-templates-lifecycle.md](apply-templates-lifecycle.md)), which validates `TemplateEntry.Template` and reads a `FunctionLibraryTemplateDefinition` from it.

## The DAX UDF grammar

DAX user-defined functions (GA September 2025; the `REF` parameter-type family added March 2026) are declared as:

```dax
FUNCTION <Name> = ( <params> ) => <body>
```

TOM splits this in two: `Function.Name` holds `<Name>`, and `Function.Expression` holds the remainder, `( <params> ) => <body>`. Each parameter follows:

```
<Name> [ : <Type> <Subtype> <PassingMode> ] [ = <DefaultExpression> ]
```

- **Passing mode** — `VAL` (eager, the default for `SCALAR`/`TABLE` when omitted) or `EXPR` (lazy).
- **Type** — `ANYVAL` (the default when `Type` is omitted entirely), `SCALAR` (requires a `Subtype`), `TABLE`, or one of the **`REF` family** — `ANYREF`, `MEASUREREF`, `COLUMNREF`, `TABLEREF`, `CALENDARREF`. `REF` types always pass by reference (equivalent to a forced `EXPR`), so the grammar never renders a passing mode after one. The scalar subtypes are also usable directly as a `Type` shorthand: `VARIANT`, `INT64`, `DECIMAL`, `DOUBLE`, `STRING`, `BOOLEAN`, `DATETIME`, `NUMERIC`.
- **Default expression** — a trailing `= <DefaultExpression>` makes the parameter optional. `BLANK()` as the default, combined with `ISBLANK()` in the body, is the common "was this argument provided?" idiom.

## JSON schema

### Top-level entry

```json
{ "Class": "FunctionLibraryTemplate", "Template": "Functions-Statistics.json", "IsHidden": false, "IsEnabled": true }
```

This reuses the existing `Class`/`Template`/`IsHidden`/`IsEnabled` `TemplateEntry` fields (see `Interfaces/ITemplates.cs`) — no new field was added. `Table` is unused; `IsHidden` is likewise not read by `FunctionLibraryTemplate.ApplyTemplate` (a `Function` is hidden per-function via `FunctionDefinition.IsHidden`, not per-entry). JSON config is purely additive.

### Library (sub-template) file

The file referenced by `Template` declares one or more functions:

```json
{
  "Functions": [
    {
      "Name": "PercentOfTotal",
      "Description": "Returns Amount as a percentage of its total when Filter is removed, rounded to DecimalPlaces.",
      "Parameters": [
        { "Name": "Amount", "Type": "SCALAR", "Subtype": "DECIMAL", "PassingMode": "VAL" },
        { "Name": "Filter", "Type": "TABLEREF" },
        { "Name": "DecimalPlaces", "Type": "INT64", "DefaultExpression": "2" }
      ],
      "MultiLineBody": [
        "ROUND (",
        "    DIVIDE ( Amount, CALCULATE ( Amount, REMOVEFILTERS ( Filter ) ) ) * 100,",
        "    DecimalPlaces",
        ")"
      ]
    },
    {
      "Name": "SafeDivide",
      "Description": "Divides Numerator by Denominator, returning AlternateResult when Denominator is blank or zero.",
      "Parameters": [
        { "Name": "Numerator", "Type": "SCALAR", "Subtype": "DECIMAL" },
        { "Name": "Denominator", "Type": "SCALAR", "Subtype": "DECIMAL", "DefaultExpression": "BLANK()" },
        { "Name": "AlternateResult", "Type": "ANYVAL", "DefaultExpression": "BLANK()" }
      ],
      "Body": "IF ( ISBLANK ( Denominator ) || Denominator = 0, AlternateResult, Numerator / Denominator )"
    }
  ]
}
```

Example source: `src/Dax.Template.Tests/_data/Templates/Functions-Statistics.json`.

`PercentOfTotal` renders `Function.Expression` as `( Amount: SCALAR DECIMAL VAL, Filter: TABLEREF, DecimalPlaces: INT64 = 2 ) => ROUND ( ... )`. Note `Filter`'s `TABLEREF` type has no `PassingMode` (rejected on reference types — see Validation below), and `DecimalPlaces` is optional via `DefaultExpression`. `SafeDivide` shows an optional `SCALAR` parameter (`Denominator: SCALAR DECIMAL = BLANK()`) using the `BLANK()`-default idiom described above.

- `Functions[]` — each entry is a `FunctionDefinition`:
  - `Name` (required) — the `Function.Name`; also the idempotency/reconciliation key (see below).
  - `Description` (optional) — copied onto `Function.Description`.
  - `IsHidden` (optional, defaults to `false`) — copied onto `Function.IsHidden`.
  - `Parameters[]` (optional) — an array of `ParameterDefinition`, in declaration order, ignored when `RawExpression` is set:
    - `Name` (required).
    - `Type` (optional) — `ANYVAL` (default when omitted), `SCALAR` (requires `Subtype`), `TABLE`, a scalar-subtype shorthand, or a `REF` type.
    - `Subtype` (optional) — required iff `Type == "SCALAR"`.
    - `PassingMode` (optional) — `VAL` or `EXPR`; invalid on a `REF` type.
    - `DefaultExpression` (optional) — DAX; its presence makes the parameter optional.
  - `Body` (single-line DAX) or `MultiLineBody` (an array of lines) — the function body, used together with `Parameters[]` to assemble `Function.Expression` (see "Engine rendering" below). `FunctionDefinition.GetBody()` returns `Body` when set, otherwise joins `MultiLineBody` with a leading `\r\n` per line, mirroring the existing `MeasuresTemplateDefinition.MeasureTemplate.GetExpression` pattern used by `MeasuresTemplate`.
  - `RawExpression` (optional) — an **escape hatch**: the literal `( params ) => body` TOM expression string, used verbatim as `Function.Expression`. Mutually exclusive with `Parameters`/`Body`/`MultiLineBody` (see Validation below).

This is a **hybrid** schema: structured `Parameters` + `Body`/`MultiLineBody` is the first-class, recommended path (the engine assembles the signature for you per the grammar above); `RawExpression` exists for DAX the structured shape can't express yet, or for pasting an expression authored elsewhere verbatim.

PascalCase function/parameter names are guidance only — not enforced by `FunctionLibraryTemplate`.

## Engine rendering

`FunctionLibraryTemplate.BuildExpression` produces `Function.Expression`:

- If `RawExpression` is set, it is returned unchanged (no further assembly).
- Otherwise, each `ParameterDefinition` is rendered by `RenderParameter` (`<Name>[: <Type>[ <Subtype>]][ <PassingMode>][ = <DefaultExpression>]`) and joined with `", "`, then wrapped as `( <params> ) => <body>` — with no parentheses padding when there are no parameters.
- `RenderParameter`: a `SCALAR` type renders as `SCALAR <Subtype>`; any other `Type` renders as-is. `PassingMode` is appended only when `Type` is **not** one of the `REF` types (`IsReferenceType`), matching the grammar rule that reference types never carry an explicit passing mode.

## Validation (validate-before-mutate)

`FunctionLibraryTemplate.ApplyTemplate` validates the **entire** definition before mutating the model at all — the same "no phantom output on an invalid definition" discipline used elsewhere in this library (e.g. the Holidays Group B1 fix, `CalculationGroupTemplate`'s build-then-add). Each violation throws `InvalidConfigurationException`:

- A function's `Name` must be non-blank.
- Function names must be unique **within one library file** (`HashSet<string>` over `Definition.Functions`, ordinal comparison).
- A function must define **exactly one** of `RawExpression` or `Body`/`MultiLineBody` — both set, or neither set, is rejected. When `RawExpression` is set, `Parameters`/`Body` are not further validated (they're simply ignored by `BuildExpression`).
- A parameter's `Name` must be non-blank.
- `Type == "SCALAR"` requires a non-blank `Subtype`.
- `PassingMode` is rejected when `Type` is a `REF` type (`ANYREF`/`MEASUREREF`/`COLUMNREF`/`TABLEREF`/`CALENDARREF`).
- `PassingMode` requires an explicit `Type` — a parameter that sets `PassingMode` while leaving `Type` unset is rejected (otherwise the passing mode would be silently dropped from the rendered signature).
- Once a parameter has a `DefaultExpression`, every parameter **after** it must also have one — mandatory parameters may not follow an optional one.

**Known limitation:** name-uniqueness is checked only within a single library file. If two separate `FunctionLibraryTemplate` entries (two different `Template` files) each define a function with the same `Name`, the collision across files is **not** detected — whichever entry's `ApplyTemplates` dispatch runs last for that name wins in the model. This is a documented, deferred limitation, the same class of gap as `MeasuresTemplate`'s existing entry-deletion behavior.

## Compatibility level

TOM requires database compatibility level **>= 1702** for the DAX user-defined-function object model — it throws `CompatibilityViolationException` at `Model.Functions.Add(...)` itself (verified empirically), the same "enforced at assignment" pattern as `CalendarTemplate` (>= 1701) and `CalculationGroupTemplate` (>= 1470/1500/1605). `FunctionLibraryTemplate.ApplyTemplate` checks `model.Database.CompatibilityLevel` up front (`MinimumCompatibilityLevel = 1702`) and throws a template-specific `InvalidConfigurationException` instead of surfacing the raw TOM exception. Because 1702 is well above the `>= 1540` `LineageTag` threshold used elsewhere in this library, no separate `LineageTag` compatibility guard is needed for new functions.

The offline test harness uses a dedicated compat-1702 fixture, `FunctionOfflineModelFixture`, leaving the lower-compat shared/Calendar/CalcGroup fixtures and their goldens untouched — the same pattern as `CalendarOfflineModelFixture` (1701) and `CalcGroupOfflineModelFixture` (1605).

## Idempotency and orphan cleanup

A TOM `Function` **does** carry `Annotations` (unlike `Calendar`), so `FunctionLibraryTemplate` uses the standard `SQLBI_Template` convention (see [measures.md](measures.md)): every function it creates is stamped with `SQLBI_Template = "Functions"` (`Attributes.SqlbiTemplateFunctions`), directly on the `Function` itself (not on a containing table, since there is none).

On each `ApplyTemplate` call:

1. Collect every `Function` in `Model.Functions` already carrying that annotation value — these are candidates for cleanup.
2. If `isEnabled == false`, remove all of them and return (no validation, no creation).
3. Otherwise validate the full `Definition` (see above), then for each `FunctionDefinition`, find-or-create the `Function` by `Name`, set `Expression`/`Description`/`IsHidden` (and a new `LineageTag` if the function is new), and stamp the ownership annotation.
4. Remove any previously-tagged function whose name was **not** reproduced in this run — orphan cleanup, matching `MeasuresTemplate`/`CalculationGroupTemplate`.

Because idempotency is keyed by an annotation on the function itself (not by `Function.Name` alone, and not by a containing table), **renaming** a function between runs does not orphan it the way `CalendarTemplate`'s `Calendar.Name`-only keying would: the old name disappears from the current run's set and is swept up as an orphan, while the new name is created fresh. This is a meaningful improvement over the `Calendar`/`CustomDateTable` rename-limitation class of gap documented elsewhere in this library.

## Related docs

- [apply-templates-lifecycle.md](apply-templates-lifecycle.md) — the `FunctionLibraryTemplate` dispatch branch in `Engine.ApplyTemplates`.
- [measures.md](measures.md) — the `SQLBI_Template` annotation convention this template reuses.
- [table-generation.md](table-generation.md) — the sibling model-attachment templates (`CalendarTemplate`, `CalculationGroupTemplate`) that, unlike this one, target a `Table`.
