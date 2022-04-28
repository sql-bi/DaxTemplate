using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Syntax;
using Column = Dax.Template.Model.Column;
using Hierarchy = Dax.Template.Model.Hierarchy;
using Level = Dax.Template.Model.Level;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using Dax.Template.Extensions;
using Dax.Template.Constants;

namespace Dax.Template.Tables.Dates
{
    public class SimpleDateTemplateConfig : TemplateConfiguration
    {
        public string QuarterPrefix { get; set; } = @"Q";
        public string FiscalYearPrefix { get; set; } = @"FY";
        public string FiscalQuarterPrefix { get; set; } = @"FQ";
    }
    public class SimpleDateTable : BaseDateTemplate<SimpleDateTemplateConfig>
    {
        readonly DaxStep __RenameCalendar;

        //// TODO: this could be localized (as other column names)
        const string DATE_COLUMN_NAME = "Date";

        public SimpleDateTable(SimpleDateTemplateConfig config, TabularModel? model ) : base( config )
        {
            Annotations.Add(Attributes.SQLBI_TEMPLATE_ATTRIBUTE, Attributes.SQLBI_TEMPLATE_DATES);
            Annotations.Add(Attributes.SQLBI_TEMPLATETABLE_ATTRIBUTE, Attributes.SQLBI_TEMPLATETABLE_DATE);

            string quarterFormatPrefix = string.Concat(from c in Config.QuarterPrefix select @"\" + c);
            string fiscalYearFormatPrefix = string.Concat(from c in Config.FiscalYearPrefix select @"\" + c);
            string fiscalQuarterFormatPrefix = string.Concat(from c in Config.FiscalQuarterPrefix select @"\" + c);

            DaxStep __Calendar = new() { 
                Name = "__Calendar", 
                Expression = GenerateCalendarExpression(model),
                IgnoreAutoDependency = true,
            };

            // TODO: Restore [Date] without empty table identifier
            __RenameCalendar = new DaxStep
            {
                Name = "__RenameCalendar",
                Expression = $@"
    SELECTCOLUMNS ( 
        __Calendar,
        ""{DATE_COLUMN_NAME}"", ''[Date]
    )",
                IgnoreAutoDependency = true,
                Dependencies = new IDependencies<DaxBase>[] { __Calendar }
            };
            DaxStep[] steps = new DaxStep[] { __Calendar, __RenameCalendar };

            Model.DateColumn Date = new()
            {
                Name = DATE_COLUMN_NAME,
                Expression = string.Empty, // Does not generate the column, use only the metadata
                DataType = DataType.DateTime,
                FormatString = "m/dd/yyyy",
                IgnoreAutoDependency = true,
                Dependencies = new IDependencies<DaxBase>[] { __RenameCalendar }
            };

            // TODO consider possible rename/localization in base table expression
            Var __Date = new VarRow { 
                Name = "__Date", 
                Expression = $"[{DATE_COLUMN_NAME}]",
                IgnoreAutoDependency = true, 
                Dependencies = new IDependencies<DaxBase>[] { Date } 
            };

            Var[] variables = {
                __Date,
                new VarGlobal { Name = "__FirstFiscalMonth", Expression = "7" },
                new VarGlobal { Name = "__FirstDayOfWeek", Expression = "0" },
                new VarRow { Name = "__Yr", Expression = "YEAR ( __Date )" },
                new VarRow { Name = "__Mn", Expression = "MONTH ( __Date )" },
                new VarRow { Name = "__Qr", Expression = "QUARTER ( __Date )" },
                new VarRow { Name = "__MnQ", Expression = "__Mn - 3 * (__Qr - 1)" },
                new VarRow { Name = "__Wd", Expression = "WEEKDAY ( __Date, 1 ) - 1" },
                new VarRow { Name = "__Fyr", Expression = "__Yr + 1 * ( __FirstFiscalMonth > 1 && __Mn >= __FirstFiscalMonth )" },
                new VarRow { Name = "__Fqr", Expression = $"FORMAT ( EOMONTH ( __Date, 1 - __FirstFiscalMonth ), \"{fiscalQuarterFormatPrefix}Q\" )" },
            };

            Column[] columns = {
                Date,
                new Column {
                    Name = "Year",
                    Expression = "DATE(__Yr, 12, 31)",
                    DataType = DataType.DateTime,
                    FormatString = "yyyy",
                },
                new Column {
                    Name = "Year Quarter Date",
                    Expression = "EOMONTH( __Date, 3 - __MnQ )",
                    DataType = DataType.DateTime,
                    FormatString = "m/dd/yyyy",
                    IsHidden = true,
                },
                new Column {
                    Name = "Year Quarter",
                    Expression = $"FORMAT( __Date, \"{quarterFormatPrefix}Q-YYYY\")",  // TODO Add FORMAT argument for localization
                    DataType = DataType.String,
                },
                new Column {
                    Name = "Quarter",
                    Expression = $"FORMAT( __Date, \"{quarterFormatPrefix}Q\" )",
                    DataType = DataType.String,
                },
                new Column {
                    // Use this version for end-of-month
                    // Name = "Year Month", Expression = @"EOMONTH( _Date, 0 )", DataType = DataType.DateTime, Dependencies = new IDependencies<DaxBase>[]  { _Date } };
                    // Use this version for beginning-of-month
                    Name = "Year Month",
                    Expression = @"EOMONTH( __Date, -1 ) + 1",
                    DataType = DataType.DateTime,
                    FormatString = "mmm yyyy",
                },
                new Column {
                    Name = "Month",
                    Expression = "DATE(1900, MONTH( __Date ), 1 )",
                    DataType = DataType.DateTime,
                    FormatString = "mmm",
                },
                new Column {
                    Name = "Day of Week",
                    Expression = "DATE(1900, 1, 7 + __Wd + (7 * (__Wd < __FirstDayOfWeek)))",
                    DataType = DataType.DateTime,
                    FormatString = "ddd",
                },
                new Column {
                    Name = "Fiscal Year",
                    Expression = "DATE(__Fyr + (__FirstFiscalMonth = 1), __FirstFiscalMonth, 1) - 1",
                    DataType = DataType.DateTime,
                    FormatString = $"{fiscalYearFormatPrefix} yyyy",
                    DisplayFolder = "Fiscal"
                },
                new Column {
                    Name = "Fiscal Year Quarter",
                    Expression = @"__Fqr & ""-"" & __Fyr",
                    DataType = DataType.String,
                    FormatString = $"{fiscalQuarterFormatPrefix} yyyy",
                    DisplayFolder = "Fiscal"
                },
                new Column {
                    Name = "Fiscal Year Quarter Date",
                    Expression = "EOMONTH( __Date, 3 - __MnQ )",
                    DataType = DataType.DateTime,
                    FormatString = "m/dd/yyyy",
                    IsHidden = true,
                    DisplayFolder = "Fiscal"
                },
                new Column {
                    Name = "Fiscal Quarter",
                    Expression = @"__Fqr",
                    DataType = DataType.String,
                    DisplayFolder = "Fiscal"
                },
                new Column {
                    Name = "TestColumnDependency",
                    Expression = @"[Fiscal Quarter]",
                    DataType = DataType.String,
                    DisplayFolder = "Fiscal"
                } 
            };
            columns.First(c => c.Name == "Year Quarter").SortByColumn = columns.First(c => c.Name == "Year Quarter Date");
            columns.First(c => c.Name == "Fiscal Year Quarter").SortByColumn = columns.First(c => c.Name == "Fiscal Year Quarter Date");
            Columns.AddRange(columns);

            Hierarchy calendarHierarchy = new()
            {
                Name = "Calendar",
                Levels = new Level[] {
                    new Level { Name = "Year", Column = columns.First(c => c.Name == "Year") },
                    new Level { Name = "Month", Column = columns.First(c => c.Name == "Year Month") },
                    new Level { Name = "Date", Column = Date },
                }
            };
            Hierarchy fiscalHierarchy = new()
            {
                Name = "Fiscal",
                Levels = new Level[] {
                    new Level { Name = "Year", Column = columns.First(c => c.Name == "Fiscal Year") },
                    new Level { Name = "Month", Column = columns.First(c => c.Name == "Year Month") },
                    new Level { Name = "Date", Column = Date },
                },
                DisplayFolder = "Fiscal"
            };

            Hierarchy[] hierarchies = { calendarHierarchy, fiscalHierarchy };
            Hierarchies.AddRange(hierarchies);

            // Set dependencies for all the items
            IEnumerable<IDaxName>? stepsVariablesColumns = ((IDaxName[])variables).Union(steps).Union(columns);
            stepsVariablesColumns.AddDependenciesFromExpression();
        }
    }
}
