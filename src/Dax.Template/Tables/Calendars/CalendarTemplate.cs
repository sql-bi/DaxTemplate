using Dax.Template.Exceptions;
using Microsoft.AnalysisServices.Tabular;
using System;
using System.Threading;

namespace Dax.Template.Tables.Calendars;

/// <summary>
/// Applies a <see cref="CalendarTemplateDefinition"/> to a TOM <see cref="Table"/> by creating,
/// updating, or removing a <see cref="Calendar"/> and its <see cref="CalendarColumnGroup"/> bindings,
/// using the public typed TOM Calendar API (<see cref="TimeUnitColumnAssociation"/> /
/// <see cref="TimeRelatedColumnGroup"/>) — no reflection, no TMSL.
/// </summary>
/// <param name="definition">The external calendar definition to apply.</param>
public class CalendarTemplate(CalendarTemplateDefinition definition)
{
    /// <summary>
    /// Minimum TOM compatibility level required by the Calendar object model. Below this level, TOM
    /// throws a <see cref="CompatibilityViolationException"/> as soon as a <see cref="Calendar"/> is
    /// added to a table (verified empirically: it does not wait until <c>Model.Validate()</c>), so this
    /// is enforced explicitly up front with a template-specific exception.
    /// </summary>
    private const int MinimumCompatibilityLevel = 1701;

    /// <summary>The external definition this instance applies.</summary>
    public CalendarTemplateDefinition Definition { get; } = definition;

    /// <summary>
    /// Creates, updates, or removes the <see cref="Calendar"/> named
    /// <see cref="CalendarTemplateDefinition.Name"/> on <paramref name="targetTable"/>.
    /// </summary>
    /// <param name="targetTable">The table the calendar is attached to.</param>
    /// <param name="isEnabled">
    /// When <see langword="false"/>, any previously-applied calendar of the same name is removed and
    /// <see langword="null"/> is returned without creating anything.
    /// </param>
    /// <param name="cancellationToken">Token observed once per column group while applying the template.</param>
    /// <returns>The created or updated <see cref="Calendar"/>, or <see langword="null"/> when disabled.</returns>
    public Calendar? ApplyTemplate(Table targetTable, bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Definition.Name))
        {
            throw new InvalidConfigurationException("Undefined Name in Calendar template configuration");
        }

        Calendar? existingCalendar = targetTable.Calendars.Find(Definition.Name);

        if (!isEnabled)
        {
            if (existingCalendar != null)
                targetTable.Calendars.Remove(existingCalendar);

            return null;
        }

        if (targetTable.Model?.Database is not { } database)
        {
            throw new InvalidConfigurationException($"Calendar target table '{targetTable.Name}' is not attached to a model with a database");
        }

        if (database.CompatibilityLevel < MinimumCompatibilityLevel)
        {
            throw new InvalidConfigurationException(
                $"Calendar '{Definition.Name}' requires a database compatibility level of at least {MinimumCompatibilityLevel} (current: {database.CompatibilityLevel})");
        }

        Calendar calendar = existingCalendar ?? new Calendar { LineageTag = Guid.NewGuid().ToString() };
        calendar.Name = Definition.Name;
        calendar.Description = Definition.Description;

        if (existingCalendar is null)
        {
            targetTable.Calendars.Add(calendar);
        }
        else
        {
            calendar.CalendarColumnGroups.Clear();
        }

        foreach (var columnGroupDefinition in Definition.ColumnGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            calendar.CalendarColumnGroups.Add(BuildColumnGroup(targetTable, columnGroupDefinition));
        }

        return calendar;
    }

    private static CalendarColumnGroup BuildColumnGroup(Table targetTable, CalendarTemplateDefinition.CalendarColumnGroupDefinition columnGroupDefinition) =>
        columnGroupDefinition.Type switch
        {
            "TimeUnit" => BuildTimeUnitColumnGroup(targetTable, columnGroupDefinition),
            "TimeRelated" => BuildTimeRelatedColumnGroup(targetTable, columnGroupDefinition),
            _ => throw new InvalidConfigurationException($"Unknown calendar column group Type '{columnGroupDefinition.Type}'")
        };

    private static TimeUnitColumnAssociation BuildTimeUnitColumnGroup(Table targetTable, CalendarTemplateDefinition.CalendarColumnGroupDefinition columnGroupDefinition)
    {
        TimeUnitColumnAssociation timeUnitColumnAssociation = new(
            columnGroupDefinition.TimeUnit ?? throw new InvalidConfigurationException("Undefined TimeUnit in a TimeUnit calendar column group configuration"))
        {
            PrimaryColumn = ResolveColumn(targetTable, columnGroupDefinition.PrimaryColumn)
        };

        if (columnGroupDefinition.AssociatedColumns != null)
        {
            foreach (var columnName in columnGroupDefinition.AssociatedColumns)
            {
                timeUnitColumnAssociation.AssociatedColumns.Add(ResolveColumn(targetTable, columnName));
            }
        }

        return timeUnitColumnAssociation;
    }

    private static TimeRelatedColumnGroup BuildTimeRelatedColumnGroup(Table targetTable, CalendarTemplateDefinition.CalendarColumnGroupDefinition columnGroupDefinition)
    {
        TimeRelatedColumnGroup timeRelatedColumnGroup = new();

        if (columnGroupDefinition.Columns != null)
        {
            foreach (var columnName in columnGroupDefinition.Columns)
            {
                timeRelatedColumnGroup.Columns.Add(ResolveColumn(targetTable, columnName));
            }
        }

        return timeRelatedColumnGroup;
    }

    private static Column ResolveColumn(Table targetTable, string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new InvalidConfigurationException("Undefined column name in calendar column group configuration");
        }

        return targetTable.Columns.Find(columnName)
            ?? throw new TemplateException($"Calendar column '{columnName}' not found on table '{targetTable.Name}' (verify the column exists in the target date table before binding a calendar to it)");
    }
}