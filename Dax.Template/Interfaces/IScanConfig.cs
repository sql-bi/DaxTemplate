using System.Text.Json.Serialization;
using Dax.Template.Enums;

namespace Dax.Template.Interfaces
{
    public interface IScanConfig
    {
        public string[] OnlyTablesColumns { get; set; }
        public string[] ExceptTablesColumns { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AutoScanEnum? AutoScan { get; set; }
    }
}
