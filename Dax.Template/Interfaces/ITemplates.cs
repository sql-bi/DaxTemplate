using System;
using System.Collections.Generic;

namespace Dax.Template.Interfaces
{
    public interface ITemplates 
    {
        public class TemplateEntry
        {
            public string? Class { get; set; }
            public string? Table { get; set; }
            public string? Template { get; set; }
            public string? ReferenceTable { get; set; }
            public string[] LocalizationFiles { get; set; } = Array.Empty<string>();
            public IMeasureTemplateConfig.TargetMeasure[] TargetMeasures { get; set; } = Array.Empty<IMeasureTemplateConfig.TargetMeasure>();
            public bool IsHidden { get; set; } = false;
            public bool IsEnabled { get; set; } = true;
            public Dictionary<string, object> Properties { get; set; } = new();
        }

        public TemplateEntry[]? Templates { get; set; }
    }
}
