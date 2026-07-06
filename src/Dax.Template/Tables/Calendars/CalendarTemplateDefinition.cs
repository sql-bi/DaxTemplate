using System.Text.Json.Serialization;
using TimeUnit = Microsoft.AnalysisServices.Tabular.TimeUnit;

namespace Dax.Template.Tables.Calendars;

/// <summary>
/// External JSON definition of a TOM <see cref="Microsoft.AnalysisServices.Tabular.Calendar"/> to attach
/// to a date table, read by <see cref="CalendarTemplate"/> via <see cref="Package.ReadDefinition{T}"/>.
/// </summary>
public class CalendarTemplateDefinition
{
    /// <summary>Name of the <see cref="Microsoft.AnalysisServices.Tabular.Calendar"/> to create or update.</summary>
    public string? Name { get; set; }

    /// <summary>Optional description applied to the <see cref="Microsoft.AnalysisServices.Tabular.Calendar"/>.</summary>
    public string? Description { get; set; }

    /// <summary>The calendar column groups (time-unit or time-related bindings) to attach to the calendar.</summary>
    public CalendarColumnGroupDefinition[] ColumnGroups { get; set; } = [];

    /// <summary>
    /// External JSON definition of a single <see cref="Microsoft.AnalysisServices.Tabular.CalendarColumnGroup"/>.
    /// The <see cref="Type"/> discriminator selects between a
    /// <see cref="Microsoft.AnalysisServices.Tabular.TimeUnitColumnAssociation"/> ("TimeUnit") and a
    /// <see cref="Microsoft.AnalysisServices.Tabular.TimeRelatedColumnGroup"/> ("TimeRelated").
    /// </summary>
    public class CalendarColumnGroupDefinition
    {
        /// <summary>Discriminator selecting the column group kind: <c>"TimeUnit"</c> or <c>"TimeRelated"</c>.</summary>
        public required string Type { get; set; }

        /// <summary>Time unit for a <c>"TimeUnit"</c> column group (e.g. <see cref="TimeUnit.Year"/>).</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TimeUnit? TimeUnit { get; set; }

        /// <summary>Name of the primary column for a <c>"TimeUnit"</c> column group.</summary>
        public string? PrimaryColumn { get; set; }

        /// <summary>Names of the associated columns for a <c>"TimeUnit"</c> column group.</summary>
        public string[]? AssociatedColumns { get; set; }

        /// <summary>Names of the columns for a <c>"TimeRelated"</c> column group.</summary>
        public string[]? Columns { get; set; }
    }
}