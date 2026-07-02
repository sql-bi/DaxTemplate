using Microsoft.AnalysisServices.Tabular;
using System.Collections.Generic;
using AttributeType = Microsoft.AnalysisServices.AttributeType;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;

namespace Dax.Template.Model;

public class Column : EntityBase, Syntax.IDependencies<Syntax.DaxBase>, Syntax.IDaxName, Syntax.IDaxComment
{
    bool Syntax.IDependencies<Syntax.DaxBase>.AddLevel { get; init; } = true;
    public bool IgnoreAutoDependency { get; init; }
    public string? Expression { get; set; }
    public DataType DataType { get; init; }
    public string? DataCategory { get; set; }
    public string? FormatString { get; set; }
    public string? DisplayFolder { get; set; }
    public bool IsHidden { get; set; }
    public bool IsTemporary { get; set; }
    public bool IsKey { get; set; }
    public Syntax.IDependencies<Syntax.DaxBase>[]? Dependencies { get; set; }
    string Syntax.IDaxName.DaxName => $"[{Name}]";
    public string[]? Comments { get; set; }
    public Column? SortByColumn { get; set; }
    internal TabularColumn? TabularColumn { get; set; }
    public Dictionary<string, object> Annotations { get; set; } = [];
    /// <summary>
    /// This attribute is applied as a SQLBI_AttributeTypes annotation
    /// </summary>
    public AttributeType[]? AttributeType { get; set; }

    public override void Reset()
    {
        SortByColumn = null;
        TabularColumn = null;
    }
    public string GetDebugInfo() => $"Column {Name} : {Expression}";
}