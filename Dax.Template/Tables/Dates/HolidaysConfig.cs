namespace Dax.Template.Tables.Dates
{
    public class HolidaysConfig
    {
        public string? TableName { get; set; }
        public string? DateColumnName { get; set; }
        public string? HolidayColumnName { get; set; }
        public static bool HasHolidays( HolidaysConfig? holidaysConfig)
        {
            return (holidaysConfig?.TableName != null) && (holidaysConfig?.DateColumnName != null) && (holidaysConfig.HolidayColumnName != null);
        }
    }
}
