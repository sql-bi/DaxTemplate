using System;
using System.Linq;
using System.Collections.Generic;
using Json.Schema.Generation;

namespace Dax.Template
{
    public class CustomTemplateDefinition
    {
        public class DaxExpression
        {
            [Description("Name of the DAX expression, corresponding to the name of the step/variable/column.")]
            public string? Name { get; set; }

            [Description("DAX expression defined in a single line. If defined, Expression has precedence over MultiLineExpression.")]
            public string? Expression { get; set; }

            [Description("Array of strings that define a comment in multiple lines. If Expression is defined, then MultiLineExpression is ignored.")]
            public string[]? MultiLineExpression { get; set; }

            [Description("Single line comment. If defined, Comment has precedence over MultiLineComment.")]
            public string? Comment { get; set; }

            [Description("Array of strings that define a DAX expression in a multiple lines. If Comment is defined, then MultiLineComment is ignored.")]
            public string[]? MultiLineComment { get; set; }

            public string[]? GetComments()
            {
                return (MultiLineComment != null && MultiLineComment.Length > 0)
                    ? MultiLineComment
                    : (!string.IsNullOrWhiteSpace(Comment) ? new string[] { Comment } : null);
            }
            public string? GetExpression(string? padding = null)
            {
                return (string.IsNullOrEmpty(Expression) && MultiLineExpression != null)
                    ? string.Join("", MultiLineExpression.Select(line => $"\r\n{padding}{line}"))
                    : Expression;
            }
        }
        public class Step : DaxExpression
        {
        }
        public abstract class Variable : DaxExpression
        {
        }
        public class GlobalVariable : DaxExpression
        {
            [Description("This additional property extends the DaxExpression object for a global variable specifying whether the variable should be configurable (true) or not (false). The global variables with IsConfigurable set to true are included in an initial section of the table expression, so that they are easier to change by manually editing the DAX expression that defines the calculated table")]
            public bool IsConfigurable { get; set; } = false;
        }
        public class RowVariable : DaxExpression
        {
        }
        public class Column : DaxExpression
        {
            [Description("Data type of the column: String, Int64, Double, DateTime, Decimal, Boolean")]
            public string? DataType { get; set; }

            [Description("Format String of the column.")]
            [Nullable(true)]
            public string? FormatString { get; set; }

            [Description("true if the column is hidden.")] 
            public bool IsHidden { get; set; } = false;
            
            [Description("true if the column is temporary. Temporary column should follow the naming convention starting with the @ symbol.")]
            public bool IsTemporary { get; set; } = false;

            [Description("true if the column requires an Holidays table. If the Holidays table is not enabled, the column is not included in the generated table.")]
            public bool RequiresHolidays { get; set; } = false;

            [Description("Name of the column to use in the Sort By property. For example, a Month column usually has MonthNumber in the SortByColumn property.")]
            public string? SortByColumn { get; set; }

            [Description("Name of the display folder.")]
            public string? DisplayFolder { get; set; }

            [Description("Data category using standard definitions: Years, Quarters, QuarterOfYear, Months, MonthOfYear, MonthOfQuarter, Weeks, WeekOfQuarter, WeekOfYear, DayOfWeek, DayOfMonty, DayOfQuarter, DayOfYear, PaddedDateTableDates")]
            public string? DataCategory { get; set; }

            [Description("Description of the expression. Can be used in comments for steps and variables.")]
            public string? Description { get; set; }

            [Description("Specifies a column defined in the specified step.")]
            public string? Step { get; set; }

            [Description("Specify a single attribute type assigned to the column. When specified, it creates a list with a single attribute in AttributeTypes, ignoring the AttributeTypes definition.")]
            public string? AttributeType { get; set; }

            [Description("Specify a list of attribute types assigned to the column. It is ignored if AttributeType is defined.")]
            public string[]? AttributeTypes { get; set; }

            [Description("List of annotations added to the column. The annotations might be required by measure templates to identify measures, columns, and tables referenced by the template.")]
            public Dictionary<string, object> Annotations { get; set; } = new();
        }
        public class HierarchyLevel
        {
            [Description("Name of the hierarchy level.")]
            public string? Name { get; set; }
            
            [Description("Corresponding column name of the hierarchy level. The column name is the simple name, it is not a fully qualified name.")]
            public string? Column { get; set; }

            [Description("Description of the hierarchy level.")] 
            public string? Description { get; set; }
        }
        public class Hierarchy
        {
            [Description("Name of the hierarchy.")]
            public string? Name { get; set; }

            [Description("Description of the hierarchy.")]
            public string? Description { get; set; }

            [Description("Array of objects defining the levels of the hierarchy. Each level has the following properties.")]
            public HierarchyLevel[] Levels { get; set; } = Array.Empty<HierarchyLevel>();
        }

        [Description("Array of strings used as prefix/suffix in formatted name of attribute values. The purpose of this definition is to create a list of names that can be translated in localized versions.")]
        public string[] FormatPrefixes { get; set; } = Array.Empty<string>();

        [Description("Array of DaxExpression objects defining explicit table steps required by other expressions. For example, the Date table usually creates a __Calendar step that defines the range of dates using the @@GETCALENDAR() placeholder.")]
        public Step[] Steps { get; set; } = Array.Empty<Step>();

        [Description("Array of objects derived from DaxExpression defining global variables that can be used by any following step. The global variables cannot have dependencies on other Steps of the template. Every global variable name must start with a double underscore prefix ( __ ).")]
        public GlobalVariable[] GlobalVariables { get; set; } = Array.Empty<GlobalVariable>();

        [Description("Array of DaxExpression objects that define local variables for each row of the generated table. The expression can reference other variables defined in RowVariables and GlobalVariables. The template engine automatically arrange the right definition order evaluating the dependencies. Every row variable name must start with a double underscore prefix ( __ ).")]
        public RowVariable[] RowVariables { get; set; } = Array.Empty<RowVariable>();

        [Description("Array of objects derived from DaxExpression defining the columns of the table generated by the template. As a naming convention, every column name should start with the @ prefix if the column is temporary to the calculation and must not be exposed in the final table.")]
        public Column[] Columns { get; set; } = Array.Empty<Column>();

        [Description("Array of objects defining user hierarchies of the table. Each hierarchy has the following properties.")]
        public Hierarchy[] Hierarchies { get; set; } = Array.Empty<Hierarchy>();

        [Description("List of annotations added to the table. The annotations might be required by measure templates to identify measures, columns, and tables referenced by the template.")]
        public Dictionary<string, string> Annotations { get; set; } = new();
        /// <summary>
        /// Define the calendar type for time intelligence calculations
        /// </summary>
        // public string? CalendarType { get; set; }
    }
}
