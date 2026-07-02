using Dax.Template.Enums;
using System.Text.Json.Serialization;

namespace Dax.Template.Interfaces;

public interface IScanConfig
{
    public string[]? OnlyTablesColumns { get; set; }
    public string[]? ExceptTablesColumns { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoScanEnum? AutoScan { get; set; }
}