using Dax.Template.Constants;
using Dax.Template.Exceptions;
using Dax.Template.Extensions;
using Microsoft.AnalysisServices.Tabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;

namespace Dax.Template.Functions;

/// <summary>
/// Applies a <see cref="FunctionLibraryTemplateDefinition"/> to a TOM <see cref="TabularModel"/> by
/// creating, updating, or removing DAX user-defined <see cref="Function"/> objects on
/// <c>Model.Functions</c> (full-replace-by-name, orphans removed), using the public typed TOM API — no
/// reflection, no TMSL. Functions are model-level (unlike tables/measures/calculation groups), so this
/// template never touches a <see cref="Table"/>.
/// </summary>
/// <param name="definition">The external function-library definition to apply.</param>
/// <remarks>
/// Requires database compatibility level &gt;= 1702 (enforced up front with a template-specific
/// exception; TOM itself throws a <see cref="CompatibilityViolationException"/> at
/// <c>Model.Functions.Add</c> below that level, verified empirically). Idempotency uses the
/// <see cref="Attributes.SqlbiTemplate"/> = <see cref="Attributes.SqlbiTemplateFunctions"/> annotation:
/// re-applying the template replaces its own prior output and removes functions it previously created
/// that are no longer present in <see cref="FunctionLibraryTemplateDefinition.Functions"/>.
/// </remarks>
public class FunctionLibraryTemplate(FunctionLibraryTemplateDefinition definition)
{
    /// <summary>
    /// Minimum TOM compatibility level required by the DAX user-defined-function object model. Below this
    /// level, TOM throws a <see cref="CompatibilityViolationException"/> as soon as a <see cref="Function"/>
    /// is added to a model (verified empirically), so this is enforced explicitly up front with a
    /// template-specific exception.
    /// </summary>
    private const int MinimumCompatibilityLevel = 1702;

    /// <summary>The external definition this instance applies.</summary>
    public FunctionLibraryTemplateDefinition Definition { get; } = definition;

    /// <summary>
    /// Validates <see cref="Definition"/> in full, then creates, updates, or removes the DAX functions it
    /// describes on <paramref name="model"/>.
    /// </summary>
    /// <param name="model">The model the function library is attached to.</param>
    /// <param name="isEnabled">
    /// When <see langword="false"/>, every function previously created by this template is removed and
    /// the method returns without validating or creating anything.
    /// </param>
    /// <param name="cancellationToken">Token observed once per function while applying the template.</param>
    /// <remarks>
    /// Validation runs entirely before any mutation of <paramref name="model"/>, so an invalid definition
    /// never leaves a partially-applied library behind (the "phantom table" lesson from the Holidays
    /// Group B1 fix applies equally here).
    /// </remarks>
    public void ApplyTemplate(TabularModel model, bool isEnabled, CancellationToken cancellationToken = default)
    {
        List<Function> existingFromThisTemplate =
            model.Functions
                .Where(f => f.Annotations.Any(a => a.Name == Attributes.SqlbiTemplate && a.Value == Attributes.SqlbiTemplateFunctions))
                .ToList();

        if (!isEnabled)
        {
            foreach (var function in existingFromThisTemplate)
            {
                model.Functions.Remove(function);
            }
            return;
        }

        if (model.Database.CompatibilityLevel < MinimumCompatibilityLevel)
        {
            throw new InvalidConfigurationException(
                $"Function library requires compatibility level >= {MinimumCompatibilityLevel} (current: {model.Database.CompatibilityLevel})");
        }

        // --- Validate the entire definition before mutating the model ---

        HashSet<string> seenNames = new(StringComparer.Ordinal);
        foreach (var fnDef in Definition.Functions)
        {
            if (string.IsNullOrWhiteSpace(fnDef.Name))
            {
                throw new InvalidConfigurationException("Undefined Name for a function in the function library configuration");
            }
            if (!seenNames.Add(fnDef.Name))
            {
                throw new InvalidConfigurationException($"Duplicate function name '{fnDef.Name}' in the function library configuration");
            }

            ValidateFunctionDefinition(fnDef);
        }

        // --- Mutate the model only after validation has fully succeeded ---

        HashSet<string> currentNames = new(StringComparer.Ordinal);

        foreach (var fnDef in Definition.Functions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentNames.Add(fnDef.Name);

            Function? fn = model.Functions.Find(fnDef.Name);
            bool isNew = fn is null;
            fn ??= new Function { Name = fnDef.Name };

            fn.Expression = BuildExpression(fnDef);
            fn.Description = fnDef.Description;
            fn.IsHidden = fnDef.IsHidden;

            if (isNew)
            {
                // The compatibility pre-check above already requires >= 1702, well above the >= 1540
                // LineageTag threshold used elsewhere in this library, so no separate guard is needed here.
                fn.LineageTag = Guid.NewGuid().ToString();
            }

            fn.Annotations.UpsertAnnotations(
                new Dictionary<string, string> { [Attributes.SqlbiTemplate] = Attributes.SqlbiTemplateFunctions },
                cancellationToken);

            if (isNew)
            {
                model.Functions.Add(fn);
            }
        }

        // --- Orphan cleanup: remove functions this template created previously but no longer defines ---

        var orphans = existingFromThisTemplate.Where(f => !currentNames.Contains(f.Name)).ToArray();
        foreach (var orphan in orphans)
        {
            model.Functions.Remove(orphan);
        }
    }

    private static void ValidateFunctionDefinition(FunctionDefinition fnDef)
    {
        bool hasRawExpression = !string.IsNullOrEmpty(fnDef.RawExpression);
        bool hasBody = !string.IsNullOrEmpty(fnDef.GetBody());

        if (hasRawExpression == hasBody)
        {
            throw new InvalidConfigurationException(
                $"Function '{fnDef.Name}' must define exactly one of RawExpression or Body/MultiLineBody, not {(hasRawExpression ? "both" : "neither")}");
        }

        if (hasRawExpression)
        {
            // Parameters/Body are ignored when RawExpression is set; no further structural validation applies.
            return;
        }

        bool previousParameterHasDefault = false;
        foreach (var parameter in fnDef.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                throw new InvalidConfigurationException($"Undefined Name for a parameter of function '{fnDef.Name}'");
            }

            bool isReferenceType = IsReferenceType(parameter.Type);

            if (string.Equals(parameter.Type, "SCALAR", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(parameter.Subtype))
            {
                throw new InvalidConfigurationException($"Parameter '{parameter.Name}' of function '{fnDef.Name}' has Type SCALAR and requires Subtype");
            }

            if (isReferenceType && parameter.PassingMode is not null)
            {
                throw new InvalidConfigurationException($"Parameter '{parameter.Name}' of function '{fnDef.Name}' has a reference Type ('{parameter.Type}') and cannot specify PassingMode");
            }

            if (parameter.PassingMode is not null && parameter.Type is null)
            {
                throw new InvalidConfigurationException($"Parameter '{parameter.Name}' of function '{fnDef.Name}' has a PassingMode but no Type; PassingMode requires an explicit Type");
            }

            bool hasDefault = parameter.DefaultExpression is not null;
            if (previousParameterHasDefault && !hasDefault)
            {
                throw new InvalidConfigurationException($"Parameter '{parameter.Name}' of function '{fnDef.Name}' must have a DefaultExpression because a preceding parameter has one (optional parameters must trail mandatory ones)");
            }
            previousParameterHasDefault = hasDefault;
        }
    }

    private static bool IsReferenceType(string? type) =>
        type is "ANYREF" or "MEASUREREF" or "COLUMNREF" or "TABLEREF" or "CALENDARREF";

    private static string BuildExpression(FunctionDefinition fnDef)
    {
        if (!string.IsNullOrEmpty(fnDef.RawExpression))
        {
            return fnDef.RawExpression;
        }

        string parameterList = string.Join(", ", fnDef.Parameters.Select(RenderParameter));
        string parameters = parameterList.Length == 0 ? string.Empty : $" {parameterList} ";
        return $"({parameters}) => {fnDef.GetBody()}";
    }

    private static string RenderParameter(ParameterDefinition parameter)
    {
        string result = parameter.Name;

        if (parameter.Type is not null)
        {
            string renderedType = string.Equals(parameter.Type, "SCALAR", StringComparison.Ordinal)
                ? $"SCALAR {parameter.Subtype}"
                : parameter.Type;
            result += $": {renderedType}";

            if (!IsReferenceType(parameter.Type) && parameter.PassingMode is not null)
            {
                result += $" {parameter.PassingMode}";
            }
        }

        if (parameter.DefaultExpression is not null)
        {
            result += $" = {parameter.DefaultExpression}";
        }

        return result;
    }
}