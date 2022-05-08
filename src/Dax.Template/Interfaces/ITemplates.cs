using System;
using System.Collections.Generic;
using Json.Schema.Generation;

namespace Dax.Template.Interfaces
{
    public interface ITemplates 
    {
        public class TemplateEntry
        {
            [Required]
            [Description("Template class name: CustomDateTable, HolidaysDefinition, HolidaysTable, or MeasuresTemplate")]
            public string? Class { get; set; }
            
            [Description("Name of the table created by the template (used only by templates that create calculated tables).")]
            [Nullable(true)]
            public string? Table { get; set; }
            
            [Description("Template configuration. It can be an external JSON file or a reference to a corresponding section (without the .JSON extension name) in the self-contained template file.")]
            [Nullable(true)]
            public string? Template { get; set; }

            [Description("If specified, creates a hidden table with the ReferenceTable name that contains the complete definition of the calculated table, whereas the table generated only has a reference to that hidden table.")]
            [Nullable(true)]
            public string? ReferenceTable { get; set; }

            [Description("Array of strings with the files used for the localization.")]
            public string[] LocalizationFiles { get; set; } = Array.Empty<string>();
            
            [Description("Array of criterias used to identify the measures to process. It is relevant only for measure templates.")]
            public IMeasureTemplateConfig.TargetMeasure[] TargetMeasures { get; set; } = Array.Empty<IMeasureTemplateConfig.TargetMeasure>();
            
            [Description("Flag true/false to specify whether the table created should be hidden. It is relevant only for table templates.")]
            public bool IsHidden { get; set; } = false;
            
            [Description("Flag true/false to specify whether the template is enabled or not. If a template is not enabled, it is ignored. Usually, this flag is used internally to disable templates that are not required for other configuration settings.")]
            public bool IsEnabled { get; set; } = true;
            
            [Description("List of properties that are used internally by specific templates. For example, the MeasureTemplate uses DisplayFolderRule and DisplayFolderRuleSingleInstanceMeasures.")]
            public Dictionary<string, object> Properties { get; set; } = new();
        }

        public TemplateEntry[]? Templates { get; set; }
    }
}
