using System.Collections.Generic;
using System.Linq;

namespace Dax.Template;

public class CustomTemplateDefinition
{
    public class DaxExpression
    {
        public string? Name { get; set; }
        public string? Expression { get; set; }
        public string[]? MultiLineExpression { get; set; }
        public string? Comment { get; set; }
        public string[]? MultiLineComment { get; set; }
        public string[]? GetComments()
        {
            return (MultiLineComment != null && MultiLineComment.Length > 0)
                ? MultiLineComment
                : (!string.IsNullOrWhiteSpace(Comment) ? [Comment] : null);
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
        public bool IsConfigurable { get; set; }
    }
    public class RowVariable : DaxExpression
    {
    }
    public class Column : DaxExpression
    {
        public string? DataType { get; set; }
        public string? FormatString { get; set; }
        public bool IsHidden { get; set; }
        public bool IsTemporary { get; set; }
        public bool RequiresHolidays { get; set; }
        public string? SortByColumn { get; set; }
        public string? DisplayFolder { get; set; }
        public string? DataCategory { get; set; }
        public string? Description { get; set; }
        public string? Step { get; set; }
        public string? AttributeType { get; set; }
        public string[]? AttributeTypes { get; set; }
        public Dictionary<string, object> Annotations { get; set; } = new();
    }
    public class HierarchyLevel
    {
        public string? Name { get; set; }
        public string? Column { get; set; }
        public string? Description { get; set; }
    }
    public class Hierarchy
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public HierarchyLevel[] Levels { get; set; } = [];
    }
    public string[] FormatPrefixes { get; set; } = [];
    public Step[] Steps { get; set; } = [];
    public GlobalVariable[] GlobalVariables { get; set; } = [];
    public RowVariable[] RowVariables { get; set; } = [];
    public Column[] Columns { get; set; } = [];
    public Hierarchy[] Hierarchies { get; set; } = [];
    public Dictionary<string, string> Annotations { get; set; } = new();
    /// <summary>
    /// Define the calendar type for time intelligence calculations
    /// </summary>
    // public string? CalendarType { get; set; }
}