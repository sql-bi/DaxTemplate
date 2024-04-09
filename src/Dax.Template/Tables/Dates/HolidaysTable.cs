using Microsoft.AnalysisServices.Tabular;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using Dax.Template.Syntax;
using Dax.Template.Constants;
using Column = Dax.Template.Model.Column;
using Dax.Template.Interfaces;
using System.Threading;

namespace Dax.Template.Tables.Dates
{
    public class HolidaysTable : BaseDateTemplate<IHolidaysConfig> // CalculatedTableTemplateBase
    {
        private readonly DaxStep __HolidaysTable;
        public HolidaysTable(IHolidaysConfig config): base(config)
        {
            Annotations.Add(Attributes.SQLBI_TEMPLATE_ATTRIBUTE, Attributes.SQLBI_TEMPLATE_HOLIDAYS);
            Annotations.Add(Attributes.SQLBI_TEMPLATETABLE_ATTRIBUTE, Attributes.SQLBI_TEMPLATETABLE_HOLIDAYS);
            __HolidaysTable = new()
            {
                Name = "__HolidayParameters",
                Expression = $@"
-- 
--    Configuration
-- 
VAR __FirstYear = @@GETMINYEAR()
VAR __LastYear = @@GETMAXYEAR()
VAR __IsoCountryHolidays = ""{config.IsoCountry}""
VAR __WorkingDays = {config.WorkingDays}
VAR __InLieuOfPrefix = ""{config.InLieuOfPrefix}""
VAR __InLieuOfSuffix = ""{config.InLieuOfSuffix}""
----------------------------------------
VAR __FilterIsoConfig =
    FILTER(
        '{config.HolidaysDefinitionTable}',
        IF(
            CONTAINS('{config.HolidaysDefinitionTable}', '{config.HolidaysDefinitionTable}'[ISO Country], __IsoCountryHolidays)
                || __IsoCountryHolidays = """",
            '{config.HolidaysDefinitionTable}'[ISO Country] = __IsoCountryHolidays,
            ERROR(""IsoCountryHolidays set to an unsupported country code"")
        )
    )
VAR __ConfigGeneration =
    GENERATE(
        GENERATESERIES(__FirstYear - 1, __LastYear + 1, 1),
        __FilterIsoConfig
    )
VAR __GeneratedRawWithDuplicatesUnfiltered =
    GENERATE(
        __ConfigGeneration,
        VAR __HolidayYear = ''[Value]
        VAR __EasterDate =
            -- Code adapted from original VB version from https://www.assa.org.au/edm 
            VAR _EasterYear = __HolidayYear
            VAR _FirstDig =
                INT ( _EasterYear / 100 )
            VAR _Remain19 =
                MOD ( _EasterYear, 19 ) 
            -- Calculate PFM date
            VAR _temp1 =
                MOD (
                    INT ( ( _FirstDig - 15 ) / 2 )
                        + 202
                        - 11 * _Remain19
                        + SWITCH (
                            TRUE,
                            _FirstDig IN {{ 21, 24, 25, 27, 28, 29, 30, 31, 32, 34, 35, 38 }},
                            0
                        ),
                    30
                )
            VAR _tA =
                _temp1 + 21
                    + IF ( _temp1 = 29 || ( _temp1 = 28 && _Remain19 > 10 ), -1 ) // 
            -- Find the next Sunday
            VAR _tB =
                MOD ( _tA - 19, 7 )
            VAR _tCpre =
                MOD ( 40 - _FirstDig, 4 )
            VAR _tC =
                _tCpre
                    + IF ( _tCpre = 3, 1 )
                    + IF ( _tCpre > 1, 1 )
            VAR _temp2 =
                MOD ( _EasterYear, 100 )
            VAR _tD =
                MOD ( _temp2 + INT ( _temp2 / 4 ), 7 )
            VAR _tE =
                MOD ( 20 - _tB - _tC - _tD, 7 )
                    + 1
            VAR _d = _tA + _tE 
            -- Return the date
            VAR _EasterDay =
                IF ( _d > 31, _d - 31, _d )
            VAR _EasterMonth =
                IF ( _d > 31, 4, 3 )
            RETURN
                DATE ( _EasterYear, _EasterMonth, _EasterDay ) 
            -- End of code adapted from original VB version from https://www.assa.org.au/edm
        VAR __SwedishMidSummer = 
            -- Compute the Midsummer day in Swedish - it is the Saturday between 20 and 26 June
            -- This calculation is valid only for years after 1953 
            -- https://sv.wikipedia.org/wiki/Midsommar_i_Sverige
            VAR _June20 = 
                DATE ( __HolidayYear, 6, 20 )
            RETURN
                DATE ( __HolidayYear, 6, 20 + (7 - WEEKDAY ( _June20, 1 ) ) )
            -- End of SwedishMidSummer calculation
        VAR __MarchEquinoxDay =
            INT ( 20.8431 + 0.242194 * ( __HolidayYear - 1980 ) )
                - INT ( ( ( __HolidayYear - 1980 ) / 4 ) )
        VAR __MarchEquinox = DATE ( __HolidayYear, 3, __MarchEquinoxDay )
        VAR __SeptemberEquinoxDay =
            INT ( 23.2488 + 0.242194 * ( __HolidayYear - 1980 ) )
                - INT ( ( __HolidayYear - 1980 ) / 4 )
        VAR __SeptemberEquinox = DATE ( __HolidayYear, 9, __SeptemberEquinoxDay )
        VAR __HolidayDate = 
//  Workaround for a SWITCH regression that fails on service from 2022-12-10 - revert to SWITCH and remove IF once the bug is fixed
//            SWITCH (
//                TRUE,
                IF ( '{config.HolidaysDefinitionTable}'[DayNumber] <> 0
                    && '{config.HolidaysDefinitionTable}'[WeekDayNumber] <> 0, ERROR ( ""Wrong configuration in {config.HolidaysDefinitionTable}"" ),
                IF ( '{config.HolidaysDefinitionTable}'[DayNumber] <> 0
                    && '{config.HolidaysDefinitionTable}'[MonthNumber] <= 12, DATE ( __HolidayYear, '{config.HolidaysDefinitionTable}'[MonthNumber], '{config.HolidaysDefinitionTable}'[DayNumber] ),
                IF ( '{config.HolidaysDefinitionTable}'[MonthNumber] = 99, -- Easter offset
                    __EasterDate + '{config.HolidaysDefinitionTable}'[DayNumber],
                IF ( '{config.HolidaysDefinitionTable}'[MonthNumber] = 98, -- Swedish Midsummer Day
                    __SwedishMidSummer + '{config.HolidaysDefinitionTable}'[DayNumber],
                IF ( '{config.HolidaysDefinitionTable}'[MonthNumber] = 97, -- September Equinox
                    __SeptemberEquinox + '{config.HolidaysDefinitionTable}'[DayNumber],
                IF ( '{config.HolidaysDefinitionTable}'[MonthNumber] = 96, -- March Equinox
                    __MarchEquinox + '{config.HolidaysDefinitionTable}'[DayNumber],
                IF ( '{config.HolidaysDefinitionTable}'[WeekDayNumber] IN {{ 0, 1, 2, 3, 4, 5, 6 }}
                    && '{config.HolidaysDefinitionTable}'[DayNumber] = 0
                    && '{config.HolidaysDefinitionTable}'[OffsetWeek] <> 0
                    && '{config.HolidaysDefinitionTable}'[MonthNumber] IN {{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }},
                    VAR _ReferenceDate =
                        DATE ( __HolidayYear, 1
                            + MOD ( '{config.HolidaysDefinitionTable}'[MonthNumber] - 1 + IF ( '{config.HolidaysDefinitionTable}'[OffsetWeek] < 0, 1 ), 12 ), 1 )
                            - IF ( '{config.HolidaysDefinitionTable}'[OffsetWeek] < 0, 1 )
                    VAR _ReferenceWeekDayNumber =
                        WEEKDAY ( _ReferenceDate, 1 ) - 1
                    VAR _Offset =
                        '{config.HolidaysDefinitionTable}'[WeekDayNumber] - _ReferenceWeekDayNumber
                            + 7 * '{config.HolidaysDefinitionTable}'[OffsetWeek]
                            + IF (
                                '{config.HolidaysDefinitionTable}'[OffsetWeek] > 0,
                                IF ( '{config.HolidaysDefinitionTable}'[WeekDayNumber] >= _ReferenceWeekDayNumber, -7 ),
                                IF ( _ReferenceWeekDayNumber >= '{config.HolidaysDefinitionTable}'[WeekDayNumber], 7)
                            )
                RETURN
                    _ReferenceDate + _Offset + '{config.HolidaysDefinitionTable}'[OffsetDays],
                ERROR ( ""Wrong configuration in {config.HolidaysDefinitionTable}"" )
            ) ) ) ) ) ) )
//            )
        VAR __HolidayDay = WEEKDAY ( __HolidayDate, 1 ) - 1
        VAR __SubstituteHolidayOffset = 
//            SWITCH (
//                TRUE,
                IF ( '{config.HolidaysDefinitionTable}'[SubstituteHoliday] = -1,
                    SWITCH ( 
                        __HolidayDay, 
                        0, 1,       -- If it falls on a Sunday then it is observed on Monday
                        6, -1,      -- If it falls on a Saturday then it is observed on Friday
                        0
                    ),
                IF ( '{config.HolidaysDefinitionTable}'[SubstituteHoliday] > 0
                    && NOT CONTAINS ( __WorkingDays, ''[Value], __HolidayDay ),
                    VAR _NextWorkingDay =
                        MINX (
                            FILTER ( __WorkingDays, ''[Value] > __HolidayDay ),
                            ''[Value]
                        )
                    VAR _SubstituteDay =
                        IF (
                            ISBLANK ( _NextWorkingDay ),
                            MINX ( __WorkingDays, ''[Value] ) + 7,
                            _NextWorkingDay
                        )
                    RETURN
                        _SubstituteDay - __HolidayDay
                            + ( '{config.HolidaysDefinitionTable}'[SubstituteHoliday] - 1 )
            ) )
//            )
        RETURN ROW ( 
            ""@HolidayDate"", DATE ( YEAR ( __HolidayDate ), MONTH ( __HolidayDate ), DAY ( __HolidayDate ) ),
            ""@SubstituteHolidayOffset"", __SubstituteHolidayOffset
        )
    )
VAR __GeneratedRawWithDuplicates =
	FILTER (
        __GeneratedRawWithDuplicatesUnfiltered,
        ( '{config.HolidaysDefinitionTable}'[FirstYear] = 0 || '{config.HolidaysDefinitionTable}'[FirstYear] <= ''[Value] )
            && ( '{config.HolidaysDefinitionTable}'[LastYear] = 0 || '{config.HolidaysDefinitionTable}'[LastYear] >= ''[Value] )
    )
VAR __RawDatesUnique = 
    DISTINCT ( 
        SELECTCOLUMNS ( 
            __GeneratedRawWithDuplicates,
            ""@HolidayDateUnique"", [@HolidayDate]
        )
    )
VAR __GeneratedRaw = 
    GENERATE (
        __RawDatesUnique,
        VAR _FilterDate = [@HolidayDateUnique]
        RETURN 
            TOPN (
                1,
                FILTER ( 
                    __GeneratedRawWithDuplicates,
                    [@HolidayDate] = _FilterDate
                ),
                '{config.HolidaysDefinitionTable}'[ConflictPriority],
                ASC,
                '{config.HolidaysDefinitionTable}'[HolidayName], 
                ASC
            )
    )  
VAR __GeneratedSubstitutesOffset =
    SELECTCOLUMNS(
        FILTER ( __GeneratedRawWithDuplicates, '{config.HolidaysDefinitionTable}'[SubstituteHoliday] <> 0 ),
        ""Value"", ''[Value],
        ""ISO Country"", '{config.HolidaysDefinitionTable}'[ISO Country],
        ""MonthNumber"", '{config.HolidaysDefinitionTable}'[MonthNumber],
        ""DayNumber"", '{config.HolidaysDefinitionTable}'[DayNumber],
        ""WeekDayNumber"", '{config.HolidaysDefinitionTable}'[WeekDayNumber],
        ""OffsetWeek"", '{config.HolidaysDefinitionTable}'[OffsetWeek],
        ""HolidayName"", '{config.HolidaysDefinitionTable}'[HolidayName],
        ""SubstituteHoliday"", '{config.HolidaysDefinitionTable}'[SubstituteHoliday],
        ""ConflictPriority"", '{config.HolidaysDefinitionTable}'[ConflictPriority],
        ""@HolidayDate"", [@HolidayDate],
        ""@SubstituteHolidayOffset"",
            VAR _CurrentHolidayDate = [@HolidayDate]
            VAR _CurrentHolidayName = '{config.HolidaysDefinitionTable}'[HolidayName]
            VAR _OriginalSubstituteDate = [@HolidayDate] + [@SubstituteHolidayOffset]
            VAR _OtherHolidays = 
                FILTER ( 
                    __GeneratedRawWithDuplicates, 
                    [@HolidayDate] <> _CurrentHolidayDate
                    || '{config.HolidaysDefinitionTable}'[HolidayName] <> _CurrentHolidayName
                )
            VAR _ConflictDay0 = 
                CONTAINS ( 
                    _OtherHolidays,
                    [@HolidayDate], _OriginalSubstituteDate
                )
            VAR _ConflictDay1 = 
                _ConflictDay0 
                && CONTAINS ( 
                    _OtherHolidays,
                    [@HolidayDate], _OriginalSubstituteDate + 1
                )
            VAR _ConflictDay2 = 
                _ConflictDay1 
                && CONTAINS ( 
                    _OtherHolidays,
                    [@HolidayDate], _OriginalSubstituteDate + 2
                )
            VAR _SubstituteOffsetStep1 = [@SubstituteHolidayOffset] + _ConflictDay0 + _ConflictDay1 + _ConflictDay2
            VAR _HolidayDateStep1 = _CurrentHolidayDate + _SubstituteOffsetStep1
            VAR _HolidayDayStep1 =
                WEEKDAY ( _HolidayDateStep1, 1 ) - 1
            VAR _SubstituteHolidayOffsetNonWorkingDays =
                IF (
                    NOT CONTAINS ( __WorkingDays, ''[Value], _HolidayDayStep1 ),
                    VAR _NextWorkingDayStep2 =
                        MINX (
                            FILTER ( __WorkingDays, ''[Value] > _HolidayDayStep1 ),
                            ''[Value]
                        )
                    VAR _SubstituteDay =
                        IF (
                            ISBLANK ( _NextWorkingDayStep2 ),
                            MINX ( __WorkingDays, ''[Value] ) + 7,
                            _NextWorkingDayStep2
                        )
                    RETURN _SubstituteDay - _HolidayDateStep1
                )
            VAR _SubstituteOffsetStep2 = _SubstituteOffsetStep1 + _SubstituteHolidayOffsetNonWorkingDays
            VAR _SubstituteDateStep2 = _OriginalSubstituteDate + _SubstituteOffsetStep2
            VAR _ConflictDayStep2_0 = 
                CONTAINS ( 
                    _OtherHolidays,
                    [@HolidayDate], _SubstituteDateStep2
                )
            VAR _ConflictDayStep2_1 = 
                _ConflictDayStep2_0
                && CONTAINS ( 
                    _OtherHolidays,
                    [@HolidayDate], _SubstituteDateStep2 + 1
                )
            VAR _ConflictDayStep2_2 = 
                _ConflictDayStep2_1 
                && CONTAINS ( 
                    _OtherHolidays,
                    [@HolidayDate], _SubstituteDateStep2 + 2
                )
            VAR _FinalSubstituteHolidayOffset = 
                _SubstituteOffsetStep2 + _ConflictDayStep2_0 + _ConflictDayStep2_1 + _ConflictDayStep2_2
            RETURN
                _FinalSubstituteHolidayOffset
        )
VAR __GeneratedSubstitutesExpanded =
    ADDCOLUMNS (
        __GeneratedSubstitutesOffset,
        ""@ReplacementHolidayDate"", [@HolidayDate] + [@SubstituteHolidayOffset]
    )
VAR __GeneratedSubstitutesUnique =
    DISTINCT ( 
        SELECTCOLUMNS ( 
            __GeneratedSubstitutesExpanded,
            ""@UniqueReplacementHolidayDate"", [@ReplacementHolidayDate]
        )
    )
VAR __GeneratedSubstitutes =
    GENERATE (
        __GeneratedSubstitutesUnique,
        TOPN (
            1,
            FILTER ( 
                __GeneratedSubstitutesExpanded,
                [@UniqueReplacementHolidayDate] = [@ReplacementHolidayDate]
            ),
            [ConflictPriority],
            ASC,
            [HolidayName], 
            ASC
        )
    )  
VAR __Generated =
    UNION (
        SELECTCOLUMNS (
            __GeneratedRaw,
            ""Holiday Date"", [@HolidayDate],
            ""Holiday Name"", '{config.HolidaysDefinitionTable}'[HolidayName]
        ),
        SELECTCOLUMNS (
            FILTER ( __GeneratedSubstitutes, [@SubstituteHolidayOffset] <> 0 ), 
            ""Holiday Date"", [@HolidayDate] + [@SubstituteHolidayOffset],
            ""Holiday Name"", __InLieuOfPrefix & [HolidayName]
                & __InLieuOfSuffix
        )
    )
VAR __GeneratedValidDates =
    FILTER ( __Generated, [Holiday Date] > 2 )
RETURN
    __GeneratedValidDates"
            };

            Column[] columns = {
                new Column {
                    Name = "Holiday Date",
                    DataType = DataType.DateTime
                },
                new Column {
                    Name = "Holiday Name",
                    DataType = DataType.String
                }
            };
            Columns.AddRange(columns);
        }

        public override string? GetDaxTableExpression(TabularModel? model, CancellationToken? cancellationToken)
        {
            return ProcessDaxExpression(__HolidaysTable.Expression, string.Empty, cancellationToken, model);
        }
    }
}

