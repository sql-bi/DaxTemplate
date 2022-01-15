using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using AttributeType = Microsoft.AnalysisServices.AttributeType;

namespace Dax.Template.Model
{
    public class Column : EntityBase, Syntax.IDependencies<Syntax.DaxBase>, Syntax.IDaxName, Syntax.IDaxComment
    {
        bool Syntax.IDependencies<Syntax.DaxBase>.AddLevel { get; init; } = true;
        public bool IgnoreAutoDependency { get; init; } = false;
        public string? Expression { get; set; } 
        public DataType DataType { get; init; } 
        public string? FormatString { get; set; } 
        public string? DisplayFolder { get; set; } 
        public bool IsHidden { get; set; } = false;
        public bool IsTemporary { get; set; } = false;
        public bool IsKey { get; set; } = false;
        public Syntax.IDependencies<Syntax.DaxBase>[]? Dependencies { get; set; } 
        string Syntax.IDaxName.DaxName { get { return $"[{Name}]"; } }
        public string[]? Comments { get; set; } 
        public Column? SortByColumn { get; set; }
        internal TabularColumn? TabularColumn { get; set; }
        public Dictionary<string, object> Annotations { get; set; } = new();
        /// <summary>
        /// This attribute is applied as a SQLBI_AttributeType annotation
        /// </summary>
        public AttributeType? AttributeType { get; set; }
        public override void Reset()
        {
            SortByColumn = null;
            TabularColumn = null;
        }
        public string GetDebugInfo() { return $"Column {Name} : {Expression}"; }
    }
}
