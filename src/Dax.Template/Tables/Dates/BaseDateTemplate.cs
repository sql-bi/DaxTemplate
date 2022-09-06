using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Extensions;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using Dax.Template.Exceptions;
using System.Text.RegularExpressions;
using Dax.Template.Interfaces;
using Dax.Template.Constants;
using System.Threading;
using System.Globalization;

namespace Dax.Template.Tables.Dates
{
    public abstract class BaseDateTemplate<T> : CustomTableTemplate<T> where T : IDateTemplateConfig
    {
        protected const string DATACATEGORY_TIME = "Time";
        protected const string ANNOTATION_CALENDAR_TYPE = "SQLBI_CalendarType";

        public string[]? CalendarType { get; init; }

        public BaseDateTemplate(T config) : base(config) { }
        public BaseDateTemplate(T config, CustomTemplateDefinition template, TabularModel? model) : base(config, template, model) { }
        public BaseDateTemplate(T config, CustomTemplateDefinition template, Predicate<CustomTemplateDefinition.Column>? skipColumn, TabularModel? model) : base(config, template, skipColumn, model) { }

        protected override string? GetDefaultFormatString(Dax.Template.Model.Column column, Microsoft.AnalysisServices.Tabular.Model model)
        {
            string isoCulture = string.IsNullOrWhiteSpace(IsoFormat) ? model.Culture : IsoFormat;
            return (column.DataType == DataType.DateTime)
                ? new CultureInfo(isoCulture).DateTimeFormat.ShortDatePattern.Replace('M', 'm')
                : null;
        }

        protected override bool IsRelationshipToSaveAndRestore(SingleColumnRelationship relationship)
        {
            // Only preserve relationships on DateTime columns
            return relationship.FromColumn.DataType == DataType.DateTime
                   && relationship.ToColumn.DataType == DataType.DateTime;
        }

        public override void ApplyTemplate(Table dateTable, CancellationToken? cancellationToken)
        {
            foreach (var column in Columns.Where(c => c is Model.DateColumn))
            {
                column.IsKey = true;
            }

            if (CalendarType != null)
            {
                string calendarTypes = string.Join(", ", CalendarType);
                Annotations.Add(ANNOTATION_CALENDAR_TYPE, calendarTypes);
            }

            base.ApplyTemplate(dateTable, cancellationToken);

            // Mark as Date table (Date column already set as Key)
            dateTable.DataCategory = DATACATEGORY_TIME;
        }

        private static readonly Regex regexGetHolidayName = new(@"@@GETHOLIDAYNAME[ \r\n\t]*\(([^\)]*)\)", RegexOptions.Compiled);
        private static readonly Regex regexGetMinDates = new(@"@@GETMINDATE[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);
        private static readonly Regex regexGetMaxDates = new(@"@@GETMAXDATE[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);
        private static readonly Regex regexGetCalendar = new(@"@@GETCALENDAR[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);
        private static readonly Regex regexGetLastStep = new(@"@@GETLASTSTEP[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);
        private static readonly Regex regexGetMinYear = new(@"@@GETMINYEAR[ \r\n\t]*\((?<minYear>[^\)]*)?\)", RegexOptions.Compiled);
        private static readonly Regex regexGetMaxYear = new(@"@@GETMAXYEAR[ \r\n\t]*\((?<maxYear>[^\)]*)?\)", RegexOptions.Compiled);
        private static readonly Regex regexGetConfig = new(@"@@GETCONFIG[ \r\n\t]*\((?<setting>[^\)]*)\)", RegexOptions.Compiled);
        private static readonly Regex regexGetDefaultVariable = new(@"@@GETDEFAULTVARIABLE[ \r\n\t]*\((?<setting>[^\)]*)\)", RegexOptions.Compiled);
        private static readonly Regex regexGetYearEndFromFirstMonthVariable = new(@"@@GETYEARENDFROMFIRSTMONTHVARIABLE[ \r\n\t]*\((?<setting>[^\)]*)\)", RegexOptions.Compiled);

        /// <summary>
        /// Modify the expression replacing placeholders - by default, it replaces the calendar
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override string? ProcessDaxExpression(string? expression, string lastStep, CancellationToken? cancellationToken, TabularModel? model = null)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            expression = base.ProcessDaxExpression(expression, lastStep, cancellationToken, model);
            if (string.IsNullOrEmpty(expression)) return expression;

            expression = GetConfig(expression);
            expression = GetHolidayName(expression);
            expression = GetDefaultVariable(expression);
            expression = GetYearEndFromFirstMonthVariable(expression);
            expression = GetLastStep(expression, lastStep);
            if (model != null)
            {
                var scanColumns = model.GetScanColumns(Config, dataCategory: DATACATEGORY_TIME);
                expression = GetMinDates(expression, scanColumns);
                expression = GetMaxDates(expression, scanColumns);
                expression = GetCalendar(expression, model);
                expression = GetMinYear(expression, model);
                expression = GetMaxYear(expression, model);
            }
            return expression;

            string GetConfig(string expression)
            {
                Match matchGetConfig = regexGetConfig.Match(expression);
                if (matchGetConfig.Success)
                {
                    var settingName = matchGetConfig.Groups.ContainsKey("setting") ? matchGetConfig.Groups["setting"].Value?.Trim() : null;
                    if (settingName == null)
                    {
                        throw new TemplateException($"Expression {regexGetConfig} not resolved");
                    }
                    string? replace = Config.GetType().GetProperty(settingName)?.GetValue(Config, null)?.ToString();
                    if (replace == null)
                    {
                        throw new TemplateException($"Configuration not available for expression {regexGetConfig}");
                    }
                    expression = regexGetConfig.Replace(expression, replace);
                }

                return expression;
            }

            string GetDefaultVariable(string expression)
            {
                Match matchGetDefaultVariable = regexGetDefaultVariable.Match(expression);
                if (matchGetDefaultVariable.Success)
                {
                    var settingName = matchGetDefaultVariable.Groups.ContainsKey("setting") ? matchGetDefaultVariable.Groups["setting"].Value?.Trim() : null;
                    if (settingName == null)
                    {
                        throw new TemplateException($"Expression {regexGetDefaultVariable} not resolved");
                    }
                    string? replace = null;
                    Config.DefaultVariables.TryGetValue(settingName, out replace);
                    if (replace == null)
                    {
                        throw new TemplateException($"Default variable not available for expression {regexGetDefaultVariable}");
                    }
                    expression = regexGetDefaultVariable.Replace(expression, replace);
                }

                return expression;
            }

            string GetYearEndFromFirstMonthVariable(string expression)
            {
                Match matchGetYearEnd = regexGetYearEndFromFirstMonthVariable.Match(expression);
                if (matchGetYearEnd.Success)
                {
                    var settingName = matchGetYearEnd.Groups.ContainsKey("setting") ? matchGetYearEnd.Groups["setting"].Value?.Trim() : null;
                    if (settingName == null)
                    {
                        throw new TemplateException($"Expression {regexGetYearEndFromFirstMonthVariable} not resolved");
                    }
                    string? firstMonth = null;
                    Config.DefaultVariables.TryGetValue(settingName, out firstMonth);
                    if (!int.TryParse(firstMonth, out int firstMonthNumber))
                    {
                        throw new TemplateException($"Invalid number argument in {regexGetYearEndFromFirstMonthVariable} expression");
                    }
                    string replace = firstMonthNumber switch
                    {
                        1 => "\"12-31\"",
                        2 => "\"1-31\"",
                        3 => "\"2-28\"",
                        4 => "\"3-31\"",
                        5 => "\"4-30\"",
                        6 => "\"5-31\"",
                        7 => "\"6-30\"",
                        8 => "\"7-31\"",
                        9 => "\"8-31\"",
                        10 => "\"9-30\"",
                        11 => "\"10-31\"",
                        12 => "\"11-30\"",
                        _ => throw new TemplateException($"Invalid month number in {regexGetYearEndFromFirstMonthVariable} expression")
                    };
                    expression = regexGetYearEndFromFirstMonthVariable.Replace(expression, replace);
                }

                return expression;
            }

            string GetHolidayName(string expression)
            {
                if (regexGetHolidayName.Match(expression).Success)
                {
                    if (!string.IsNullOrEmpty(Config.HolidaysReference?.TableName)
                        && !string.IsNullOrEmpty(Config.HolidaysReference?.DateColumnName)
                        && !string.IsNullOrEmpty(Config.HolidaysReference?.HolidayColumnName)
                        )
                    {
                        string replace = $"LOOKUPVALUE ( '{Config.HolidaysReference.TableName}'[{Config.HolidaysReference.HolidayColumnName}], '{Config.HolidaysReference.TableName}'[{Config.HolidaysReference.DateColumnName}], $1 )";
                        expression = regexGetHolidayName.Replace(expression, replace);
                    }
                    else
                    {
                        throw new TemplateException($"Invalid reference: missing Holidays configuration: {expression}");
                    }
                }

                return expression;
            }

            static string GetLastStep(string expression, string lastStep)
            {
                if (!string.IsNullOrEmpty(lastStep))
                {
                    if (regexGetLastStep.Match(expression).Success)
                    {
                        expression = regexGetLastStep.Replace(expression, lastStep);
                    }
                }

                return expression;
            }

            static string GetMinDates(string expression, IEnumerable<TabularColumn>? scanColumns)
            {
                if (regexGetMinDates.Match(expression).Success)
                {
                    string replace = "TODAY()";
                    if (scanColumns != null)
                    {
                        // TODO: remove Table?.Name
                        var listMin = string.Join(", ", scanColumns.Select(col => $"MIN ( '{col.Table?.Name}'[{col.Name}] )"));
                        replace = listMin.IsNullOrEmpty() ? "TODAY()" : $"MINX ( {{ {listMin} }}, ''[Value] )";
                    }
                    expression = regexGetMinDates.Replace(expression, replace);
                }
                return expression;
            }

            static string GetMaxDates(string expression, IEnumerable<TabularColumn>? scanColumns)
            {
                if (regexGetMaxDates.Match(expression).Success)
                {
                    string replace = "TODAY()";
                    if (scanColumns != null)
                    {
                        // TODO: remove Table?.Name
                        var listMax = string.Join(", ", scanColumns.Select(col => $"MAX ( '{col.Table?.Name}'[{col.Name}] )"));
                        replace = listMax.IsNullOrEmpty() ? "TODAY()" : $"MAXX ( {{ {listMax} }}, ''[Value] )";
                    }
                    expression = regexGetMaxDates.Replace(expression, replace);
                }
                return expression;
            }

            string GetCalendar(string expression, TabularModel model)
            {
                if (regexGetCalendar.Match(expression).Success)
                {
                    string replace = GenerateCalendarExpression(model);
                    expression = regexGetCalendar.Replace(expression, replace);
                }

                return expression;
            }

            string GetMinYear(string expression, TabularModel model)
            {
                Match matchMinYear = regexGetMinYear.Match(expression);
                if (matchMinYear.Success)
                {
                    var existingVar = matchMinYear.Groups.ContainsKey("minYear") ? matchMinYear.Groups["minYear"].Value : null;
                    string? replace = GenerateMinYearExpression(model, existingVar);
                    if (replace == null)
                    {
                        throw new TemplateException($"Expression {regexGetMinYear} not resolved");
                    }
                    expression = regexGetMinYear.Replace(expression, replace);
                }

                return expression;
            }

            string GetMaxYear(string expression, TabularModel model)
            {
                Match matchGetMaxYear = regexGetMaxYear.Match(expression);
                if (matchGetMaxYear.Success)
                {
                    var existingVar = matchGetMaxYear.Groups.ContainsKey("maxYear") ? matchGetMaxYear.Groups["maxYear"].Value : null;
                    string? replace = GenerateMaxYearExpression(model);
                    if (replace == null)
                    {
                        throw new TemplateException($"Expression {regexGetMaxYear} not resolved");
                    }
                    expression = regexGetMaxYear.Replace(expression, replace);
                }

                return expression;
            }
        }
        protected string GenerateCalendarExpression(TabularModel? model)
        {
            if (model == null) return "CALENDARAUTO()";

            string? firstYear = GenerateMinYearExpression(model);
            string? lastYear = GenerateMaxYearExpression(model);

            string calendarExpression;
            if (firstYear != null && lastYear != null)
            {
                // Use CALENDAR
                calendarExpression = $@"
    VAR __FirstYear = {firstYear}
    VAR __LastYear = {lastYear}
    RETURN CALENDAR (
        DATE ( __FirstYear, 1, 1 ),
        DATE ( __LastYear, 12, 31 )
    )";
            }
            else
            {
                int? minYear = Config.FirstYearMin ?? Config.FirstYearMax;
                int? maxYear = Config.LastYearMin ?? Config.LastYearMax;
                // Use CALENDARAUTO and apply filter later
                calendarExpression = (minYear == null && maxYear == null) ? "CALENDARAUTO()" :
$@"
    VAR __FirstYear = {minYear}
    VAR __LastYear = {maxYear}
    RETURN FILTER (
        CALENDARAUTO(),
        {
            ((minYear != null) ? $"YEAR ( [Date] ) >= __FirstYear" : (maxYear != null) ? " && " : "")
        }{
            ((maxYear != null) ? $"YEAR ( [Date] ) <= __LastYear" : "")
        }
)";
            }
            return calendarExpression;
        }

        protected string? GenerateMinYearExpression(TabularModel model, string? firstYear = null)
        {
            IEnumerable<TabularColumn>? scanColumns = model.GetScanColumns(Config, dataCategory: DATACATEGORY_TIME);

            if (string.IsNullOrEmpty(firstYear) && scanColumns != null)
            {
                var listMin = string.Join(", ", scanColumns.Select(col => $"MIN ( '{col.Table.Name}'[{col.Name}] )"));
                firstYear = listMin.IsNullOrEmpty() ? "YEAR ( TODAY() )" : $"YEAR ( MINX ( {{ {listMin} }}, ''[Value] ) )";
            }
            firstYear =
                (string.IsNullOrEmpty(firstYear)) ?
                    ((Config.FirstYearMin != null) ? Config.FirstYearMin?.ToString() : Config.FirstYearMax?.ToString()) :
                    (Config.FirstYearMin != null && Config.FirstYearMax != null) ? $"MAX ( {Config.FirstYearMin}, MIN ( {Config.FirstYearMax}, {firstYear} ) )" :
                    (Config.FirstYearMin != null) ? $"MAX ( {Config.FirstYearMin}, {firstYear} )" :
                    (Config.FirstYearMax != null) ? $"MIN ( {Config.FirstYearMax}, {firstYear} )" :
                    firstYear;

            return firstYear;
        }

        protected string? GenerateMaxYearExpression(TabularModel model, string? lastYear = null)
        {
            IEnumerable<TabularColumn>? scanColumns = model.GetScanColumns(Config, dataCategory: DATACATEGORY_TIME);

            if (string.IsNullOrEmpty(lastYear) && scanColumns != null)
            {
                // TODO: remove Table?.Name
                var listMax = string.Join(", ", scanColumns.Select(col => $"MAX ( '{col.Table?.Name}'[{col.Name}] )"));
                lastYear = listMax.IsNullOrEmpty() ? "YEAR ( TODAY() )" : $" YEAR ( MAXX ( {{ {listMax} }}, ''[Value] ) )";
            }
            lastYear =
                (string.IsNullOrEmpty(lastYear)) ?
                    ((Config.LastYearMin != null) ? Config.LastYearMin?.ToString() : Config.LastYearMax?.ToString()) :
                    (Config.LastYearMin != null && Config.LastYearMax != null) ? $"MAX ( {Config.LastYearMin}, MIN ( {Config.LastYearMax}, {lastYear} ) )" :
                    (Config.LastYearMin != null) ? $"MAX ( {Config.LastYearMin}, {lastYear} ) " :
                    (Config.LastYearMax != null) ? $"MIN ( {Config.LastYearMax}, {lastYear} ) " :
                    lastYear;

            return lastYear;
        }
    }
}