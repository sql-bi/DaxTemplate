namespace Dax.Template.Tables.Dates
{
    using System.Text.Json.Serialization;

    public class HolidaysConfig
    {
        [JsonIgnore]
        public bool IsEnabled { get; set; } = true;
        public string? TableName { get; set; }
        public string? DateColumnName { get; set; }
        public string? HolidayColumnName { get; set; }
        public static bool HasHolidays( HolidaysConfig? holidaysConfig)
        {
            return (holidaysConfig?.IsEnabled == true) && (holidaysConfig?.TableName != null) && (holidaysConfig?.DateColumnName != null) && (holidaysConfig.HolidayColumnName != null);
        }
    }
}
