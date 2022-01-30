using Dax.Template.Enums;
namespace Dax.Template.Interfaces
{
    public interface IMeasureTemplateConfig : IScanConfig
    {
        public class TargetMeasure
        {
            public string? Name { get; set; }
        }
        public AutoNamingEnum AutoNaming { get; set; }
        public string AutoNamingSeparator { get; set; }
        // public IScanConfig DateColumns { get; set; } = new();
        public TargetMeasure[] TargetMeasures { get; set; }
        public string? TableSingleInstanceMeasures { get; set; }
    }
}