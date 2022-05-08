namespace Dax.Template.Tables.Dates
{
    using System.Text.Json.Serialization;
    using Json.Schema.Generation;

    public class HolidaysConfig
    {
        [JsonIgnore]
        [Description("Use true to generate the Date table using the Holidays table, or false to ignore holidays related information.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Name of the Holidays table.")]
        public string? TableName { get; set; }

        [Description("Name of the column of Date data type in the Holidays table.")]
        public string? DateColumnName { get; set; }

        [Description("Name of the column of type string containing the name of the holiday for each corresponding date in the Holidays table.")]
        public string? HolidayColumnName { get; set; }

        public static bool HasHolidays( HolidaysConfig? holidaysConfig)
        {
            return (holidaysConfig?.IsEnabled == true) && (holidaysConfig?.TableName != null) && (holidaysConfig?.DateColumnName != null) && (holidaysConfig.HolidayColumnName != null);
        }
    }
}
