using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using TabularMeasure = Microsoft.AnalysisServices.Tabular.Measure;

namespace Dax.Template.Model
{
    public class Measure : EntityBase, Syntax.IDaxComment
    {
        public virtual string? Expression { get; set; }
        string DaxReference { get { return $"[{Name}]"; } }
        public string[]? Comments { get; set; } = Array.Empty<string>();
        public string? DisplayFolder { get; set; }
        public string? FormatString { get; set; }
        public bool IsHidden { get; set; } = false;
        public IEnumerable<KeyValuePair<string, string>>? Annotations { get; set; }
        public override void Reset()
        {
            // Implement reset of references to Tabular entities
        }
    }
}
