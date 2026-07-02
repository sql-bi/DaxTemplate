using Dax.Template.Constants;
using Dax.Template.Syntax;
using Microsoft.AnalysisServices.Tabular;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using Column = Dax.Template.Model.Column;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;

namespace Dax.Template.Tables.Dates;

public class HolidaysDefinitionTable : CalculatedTableTemplateBase
{
    public enum SubstituteEnum
    {
        NoSubstituteHoliday = 0,
        SubstituteHolidayWithNextWorkingDay = 1,
        /// <summary>
        /// Use 2 before 1 only, e.g. Christmas = 2, Boxing Day = 1
        /// </summary>
        SubstituteHolidayWithNextNextWorkingDay = 2,
        /// <summary>
        /// If it falls on a Saturday then it is observed on Friday, 
        /// If it falls on a Sunday then it is observed on Monday
        /// </summary>
        FridayIfSaturdayOrMondayIfSunday = -1
    }
    public class HolidayLine
    {
        /// <summary>
        /// ISO country code (filter holidays based on country)
        /// </summary>
        public string? IsoCountry { get; set; }
        /// <summary>
        /// Number of month - use 99 for relative dates using Easter as a reference
        /// </summary>
        public int MonthNumber { get; set; }
        /// <summary>
        /// Absolute day (ignore WeekDayNumber, otherwise use 0)
        /// </summary>
        public int DayNumber { get; set; }
        /// <summary>
        /// 0 = Sunday, 1 = Monday, ... , 6 = Saturday
        /// </summary>
        public int WeekDayNumber { get; set; }
        /// <summary>
        /// 1 = first, 2 = second, ... -1 = last, -2 = second-last, ...
        /// </summary>
        public int OffsetWeek { get; set; }
        /// <summary>
        /// days to add after offsetWeek and WeekDayNumber have been applied
        /// </summary>
        public int OffsetDays { get; set; }
        /// <summary>
        /// Holiday name 
        /// </summary>
        public string? HolidayName { get; set; }
        /// <summary>
        /// Define logic to move an holiday to another day in case
        /// the date is already a non-working day (e.g. "in lieu of...")
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubstituteEnum SubstituteHoliday { get; set; } = SubstituteEnum.NoSubstituteHoliday;
        /// <summary>
        /// Priority in case of two or more holidays in the same date - lower number --> higher priority
        /// For example: marking Easter relative days with 150 and other holidays with 100 means that other holidays take   
        ///              precedence over Easter-related days; use 50 for Easter related holidays to invert such a priority
        /// </summary>
        public int ConflictPriority { get; set; }
        /// <summary>
        /// First year for the holiday, 0 if it is not defined
        /// </summary>
        public int FirstYear { get; set; }
        /// <summary>
        /// Last year for the holiday, 0 if it is not defined
        /// </summary>
        public int LastYear { get; set; }

        internal string GetTableLine() =>
            $"{{ \"{IsoCountry}\", {MonthNumber}, {DayNumber}, {WeekDayNumber}, {OffsetWeek}, {OffsetDays}, \"{HolidayName}\", {(int)SubstituteHoliday}, {ConflictPriority}, {FirstYear}, {LastYear} }}";
    }
    public class HolidaysDefinitions
    {
        public HolidayLine[] Holidays { get; set; } = [];
    }

    private readonly DaxStep __HolidaysDefinition;
    public HolidaysDefinitionTable(HolidaysDefinitions holidaysDefinitions)
    {
        string padding = new(' ', 8);
        Annotations.Add(Attributes.SQLBI_TEMPLATE_ATTRIBUTE, Attributes.SQLBI_TEMPLATE_HOLIDAYS);
        Annotations.Add(Attributes.SQLBI_TEMPLATETABLE_ATTRIBUTE, Attributes.SQLBI_TEMPLATETABLE_HOLIDAYSDEFINITION);
        __HolidaysDefinition = new()
        {
            Name = "__HolidayParameters",
            Expression = $@"
DATATABLE (
    ""ISO Country"", STRING,        -- ISO country code(to enable filter based on country)
    ""MonthNumber"", INTEGER,       -- Number of month - use 99,98,97,96 for relative dates using an offset over special references:
                                  --     99 = Easter (DayNumber 1 = Easter Monday, DayNumber -2 = Easter Friday)
                                  --     98 = Swedish Midsummer Day 
                                  --     97 = September Equinox
                                  --     96 = March Equinox
    ""DayNumber"", INTEGER,         -- Absolute day(ignore WeekDayNumber, otherwise use 0)
    ""WeekDayNumber"", INTEGER,     -- 0 = Sunday, 1 = Monday, ... , 7 = Saturday
    ""OffsetWeek"", INTEGER,        -- 1 = first, 2 = second, ... -1 = last, -2 = second - last, ...
    ""OffsetDays"", INTEGER,        -- days to add after offsetWeek and WeekDayNumber have been applied
    ""HolidayName"", STRING,        -- Holiday name
    ""SubstituteHoliday"", INTEGER, -- 0 = no substituteHoliday, 1 = substitute holiday with next working day, 2 = substitute holiday with next working day
                                  -- (use 2 before 1 only, e.g.Christmas = 2, Boxing Day = 1)
                                  -- -1 = if it falls on a Saturday then it is observed on Friday, if it falls on a Sunday then it is observed on Monday
    ""ConflictPriority"", INTEGER,  -- Priority in case of two or more holidays in the same date - lower number-- > higher priority
                                  -- For example: marking Easter relative days with 150 and other holidays with 100 means that other holidays take
                                  --              precedence over Easter - related days; use 50 for Easter related holidays to invert such a priority
    ""FirstYear"", INTEGER,         -- First year for the holiday, 0 if it is not defined
    ""LastYear"", INTEGER,          -- Last year for the holiday, 0 if it is not defined
    {{
        {string.Join($",\r\n{padding}", holidaysDefinitions.Holidays.Select(h => h.GetTableLine()))}
    }}
)"
        };

        Column[] columns = [
            new() {
                    Name = "ISO Country",
                    DataType = DataType.String
                },
                new() {
                    Name = "MonthNumber",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "DayNumber",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "WeekDayNumber",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "OffsetWeek",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "OffsetDays",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "HolidayName",
                    DataType = DataType.String
                },
                new() {
                    Name = "SubstituteHoliday",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "ConflictPriority",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "FirstYear",
                    Expression = "''[FirstYear]",
                    DataType = DataType.Int64
                },
                new() {
                    Name = "LastYear",
                    DataType = DataType.Int64
                }
        ];
        Columns.AddRange(columns);
    }

    public override string? GetDaxTableExpression(TabularModel? model, CancellationToken cancellationToken = default)
    {
        return __HolidaysDefinition.Expression;
    }
}