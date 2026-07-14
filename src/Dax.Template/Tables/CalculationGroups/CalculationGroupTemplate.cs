using Dax.Template.Constants;
using Dax.Template.Exceptions;
using Dax.Template.Extensions;
using Microsoft.AnalysisServices.Tabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dax.Template.Tables.CalculationGroups;

/// <summary>
/// Applies a <see cref="CalculationGroupTemplateDefinition"/> to a TOM <see cref="Table"/> by creating or
/// updating a <see cref="CalculationGroup"/>: its ownership annotation, backing string column and
/// partition, calculation items (full-replace-by-name, orphans removed), and the two selection expressions
/// (<see cref="CalculationGroup.MultipleOrEmptySelectionExpression"/> / <see cref="CalculationGroup.NoSelectionExpression"/>),
/// using the public typed TOM API — no reflection, no TMSL. This is a generic calculation-group generator,
/// independent of the <c>Measures</c>/<c>Syntax</c>/time-intelligence-macro machinery elsewhere in this
/// library: the JSON definition alone determines the calculation items and DAX.
/// </summary>
/// <param name="definition">The external calculation-group definition to apply.</param>
public class CalculationGroupTemplate(CalculationGroupTemplateDefinition definition)
{
    /// <summary>The external definition this instance applies.</summary>
    public CalculationGroupTemplateDefinition Definition { get; } = definition;

    /// <summary>
    /// Validates <see cref="Definition"/> in full, then creates or updates the calculation group on
    /// <paramref name="targetTable"/>.
    /// </summary>
    /// <param name="targetTable">
    /// The table the calculation group is attached to. May be a brand-new <see cref="Table"/> not yet added
    /// to a model, or an existing table already attached to one (see remarks).
    /// </param>
    /// <param name="isHidden">Applied to <see cref="Table.IsHidden"/> once validation succeeds.</param>
    /// <param name="cancellationToken">Token observed once per calculation item while applying the template.</param>
    /// <remarks>
    /// <para>
    /// Validation runs entirely before any mutation of <paramref name="targetTable"/>, so an invalid
    /// definition never leaves a partially-built table behind (the "phantom table" lesson from the
    /// Holidays Group B1 fix applies equally here).
    /// </para>
    /// <para>
    /// Because <c>Engine.ApplyTemplates</c>'s CalculationGroupTemplate dispatch may call this method with a
    /// brand-new <see cref="Table"/> that has not yet been added to a model (to keep the no-phantom
    /// guarantee for newly-created tables), <see cref="Table.Model"/> can be <see langword="null"/> here.
    /// Rather than throwing in that case, the backing column's <see cref="MetadataObject.LineageTag"/> is
    /// simply left unset; the caller stamps the table's own <c>LineageTag</c>, and backfills the backing
    /// column's <see cref="MetadataObject.LineageTag"/> if still unset, once it attaches the table to a
    /// model.
    /// </para>
    /// </remarks>
    public void ApplyTemplate(Table targetTable, bool isHidden, CancellationToken cancellationToken = default)
    {
        // --- Validate the entire definition before mutating targetTable ---

        if (Definition.CalculationItems.Length == 0)
        {
            throw new InvalidConfigurationException($"Calculation group table '{targetTable.Name}' has no CalculationItems defined");
        }

        if (string.IsNullOrWhiteSpace(Definition.ColumnName))
        {
            throw new InvalidConfigurationException($"Undefined ColumnName in calculation group '{targetTable.Name}' configuration");
        }

        int[] effectiveOrdinals = new int[Definition.CalculationItems.Length];
        for (int i = 0; i < Definition.CalculationItems.Length; i++)
        {
            var item = Definition.CalculationItems[i];

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                throw new InvalidConfigurationException($"Undefined Name for a calculation item in calculation group '{targetTable.Name}' configuration");
            }
            if (string.IsNullOrEmpty(item.GetExpression()))
            {
                throw new InvalidConfigurationException($"Undefined Expression for calculation item '{item.Name}' in calculation group '{targetTable.Name}' configuration");
            }

            // Effective ordinal rule: an item's own Ordinal wins when set; otherwise its array index is
            // used. The final set of effective ordinals (mixing explicit and implicit values) must be
            // unique, checked below.
            effectiveOrdinals[i] = item.Ordinal ?? i;
        }

        var duplicateGroup = effectiveOrdinals
            .Select((ordinal, index) => (ordinal, name: Definition.CalculationItems[index].Name))
            .GroupBy(x => x.ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateGroup != null)
        {
            string itemNames = string.Join(", ", duplicateGroup.Select(x => $"'{x.name}'"));
            throw new InvalidConfigurationException($"Duplicate effective Ordinal {duplicateGroup.Key} among calculation items {itemNames} in calculation group '{targetTable.Name}' configuration");
        }

        // --- Mutate targetTable only after validation has fully succeeded ---

        targetTable.IsHidden = isHidden;
        targetTable.Annotations.UpsertAnnotations(
            new Dictionary<string, string> { [Attributes.SqlbiTemplate] = Attributes.SqlbiTemplateTableCalculationGroup },
            cancellationToken);

        CalculationGroup calculationGroup = targetTable.CalculationGroup ??= new CalculationGroup();
        calculationGroup.Precedence = Definition.Precedence;
        calculationGroup.Description = Definition.Description;
        calculationGroup.MultipleOrEmptySelectionExpression = BuildSelectionExpression(
            Definition.MultipleOrEmptySelectionExpression,
            Definition.MultipleOrEmptySelectionFormatStringExpression);
        calculationGroup.NoSelectionExpression = BuildSelectionExpression(
            Definition.NoSelectionExpression,
            Definition.NoSelectionFormatStringExpression);

        EnsureBackingColumn(targetTable);
        EnsurePartition(targetTable);
        ReconcileCalculationItems(calculationGroup, effectiveOrdinals, cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="CalculationGroupExpression"/> for a selection expression, or <see
    /// langword="null"/> when <paramref name="expression"/> is empty so a previously-set expression is
    /// cleared on re-apply. Requires database compatibility level &gt;= 1605, enforced by TOM itself at
    /// assignment time (a <see cref="CompatibilityViolationException"/>, verified empirically).
    /// </summary>
    private static CalculationGroupExpression? BuildSelectionExpression(string? expression, string? formatStringExpression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return null;
        }

        return new CalculationGroupExpression
        {
            Expression = expression,
            FormatStringDefinition = string.IsNullOrEmpty(formatStringExpression)
                ? null
                : new FormatStringDefinition { Expression = formatStringExpression }
        };
    }

    private void EnsureBackingColumn(Table targetTable)
    {
        Column? existingColumn = targetTable.Columns.Find(Definition.ColumnName);
        if (existingColumn is not null)
        {
            if (existingColumn is not DataColumn { DataType: DataType.String })
            {
                throw new InvalidConfigurationException(
                    $"Column '{Definition.ColumnName}' already exists on table '{targetTable.Name}' and is not the String DataColumn expected as the calculation group's backing column");
            }

            return;
        }

        DataColumn column = new()
        {
            Name = Definition.ColumnName,
            DataType = DataType.String,
            SourceColumn = Definition.ColumnName
        };

        // targetTable.Model is null for a brand-new table not yet added to a model (see remarks on
        // ApplyTemplate); skip the LineageTag rather than throwing in that case.
        if (targetTable.Model?.Database is { } database && database.CompatibilityLevel >= 1540)
        {
            column.LineageTag = Guid.NewGuid().ToString();
        }

        targetTable.Columns.Add(column);
    }

    private static void EnsurePartition(Table targetTable)
    {
        if (targetTable.Partitions.Count > 0)
        {
            return;
        }

        targetTable.Partitions.Add(new Partition
        {
            Name = targetTable.Name,
            Source = new CalculationGroupSource()
        });
    }

    private void ReconcileCalculationItems(CalculationGroup calculationGroup, int[] effectiveOrdinals, CancellationToken cancellationToken)
    {
        HashSet<string> currentNames = new(StringComparer.Ordinal);

        for (int i = 0; i < Definition.CalculationItems.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var itemDefinition = Definition.CalculationItems[i];
            currentNames.Add(itemDefinition.Name);

            CalculationItem? calculationItem = calculationGroup.CalculationItems.Find(itemDefinition.Name);
            bool isNew = calculationItem is null;
            calculationItem ??= new CalculationItem { Name = itemDefinition.Name };

            calculationItem.Expression = itemDefinition.GetExpression();
            calculationItem.Ordinal = effectiveOrdinals[i];
            calculationItem.Description = itemDefinition.Description;
            calculationItem.FormatStringDefinition = string.IsNullOrEmpty(itemDefinition.FormatStringExpression)
                ? null
                : new FormatStringDefinition { Expression = itemDefinition.FormatStringExpression };

            if (isNew)
            {
                calculationGroup.CalculationItems.Add(calculationItem);
            }
        }

        var orphans = calculationGroup.CalculationItems.Where(ci => !currentNames.Contains(ci.Name)).ToArray();
        foreach (var orphan in orphans)
        {
            calculationGroup.CalculationItems.Remove(orphan);
        }
    }
}