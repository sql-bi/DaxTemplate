using Dax.Template;
using Dax.Template.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TestDaxTemplates.Bravo
{
    public class DaxTemplateConfig
    {
        public enum WeeklyTypeEnum
        {
            Last,
            Nearest
        }

        public enum TypeStartFiscalYear
        {
            FirstDayOfFiscalYear = 0,
            LastDayOfFiscalYear = 1
        }
        public enum QuarterWeekTypeEnum
        {
            Weekly445,
            Weekly454,
            Weekly544
        }
        public enum DayOfWeekEnum
        {
            Sunday = 0,
            Monday = 1,
            Tuesday = 2,
            Wednesday = 3,
            Thursday = 4,
            Friday = 5,
            Saturday = 6
        }
        public class DefaultVariables
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? FirstFiscalMonth { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public DayOfWeekEnum? FirstDayOfWeek { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? MonthsInYear { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? WorkingDayType { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? NonWorkingDayType { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public TypeStartFiscalYear? TypeStartFiscalYear { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public QuarterWeekTypeEnum? QuarterWeekType { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public WeeklyTypeEnum? WeeklyType { get; set; }

        }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? IsoCountry { get; set; }
        public string? IsoTranslation { get; set; }
        public string? IsoFormat { get; set; }
        public string[]? OnlyTablesColumns { get; set; }
        public string[]? ExceptTablesColumns { get; set; }
        public int? FirstYear { get; set; }
        public int? LastYear { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AutoScanEnum? AutoScan { get; set; }
        public DefaultVariables Defaults { get; init; } = new ();
        public string? TableSingleInstanceMeasures { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AutoNamingEnum? AutoNaming { get; set; }
        public string[]? TargetMeasures { get; set; }
    }
    public class BravoDaxTemplate
    {
        const string TEMPLATE_EXTENSION = ".template";
        const string JSON_EXTENSION = ".json";
        const string TEMPLATEJSON_EXTENSION = TEMPLATE_EXTENSION + JSON_EXTENSION;
        const string TEMPLATEJSON_WILDCARD = "*" + TEMPLATEJSON_EXTENSION;

        public static DaxTemplateConfig[] GetTemplates(string path)
        {

            List<DaxTemplateConfig> daxTemplateConfigs = new ();
            var templateFiles = Directory.EnumerateFiles(path, TEMPLATEJSON_WILDCARD );
            foreach( var templatePath in templateFiles)
            {
                var package = Package.LoadPackage(templatePath);
                if (package?.Configuration == null)
                {
                    throw new Exception($"Configuration {templatePath} not loaded.");
                }
                DaxTemplateConfig templateConfig = new() {
                    Name = package.Configuration.Name,
                    Description = package.Configuration.Description,
                    IsoCountry = package.Configuration.IsoCountry,
                    IsoFormat = package.Configuration.IsoFormat,
                    IsoTranslation = package.Configuration.IsoTranslation,
                    AutoScan = package.Configuration.AutoScan,
                    AutoNaming = package.Configuration.AutoNaming
                };
                templateConfig.Defaults.FirstFiscalMonth = GetIntParameter(nameof(templateConfig.Defaults.FirstFiscalMonth));
                templateConfig.Defaults.FirstDayOfWeek = (DaxTemplateConfig.DayOfWeekEnum?)GetIntParameter(nameof(templateConfig.Defaults.FirstDayOfWeek));
                templateConfig.Defaults.MonthsInYear = GetIntParameter(nameof(templateConfig.Defaults.MonthsInYear));
                templateConfig.Defaults.WorkingDayType = GetStringParameter(nameof(templateConfig.Defaults.WorkingDayType));
                templateConfig.Defaults.NonWorkingDayType = GetStringParameter(nameof(templateConfig.Defaults.NonWorkingDayType));
                templateConfig.Defaults.TypeStartFiscalYear = (DaxTemplateConfig.TypeStartFiscalYear?)GetIntParameter(nameof(templateConfig.Defaults.TypeStartFiscalYear));
                if (Enum.TryParse(GetStringParameter(nameof(templateConfig.Defaults.QuarterWeekType)), out DaxTemplateConfig.QuarterWeekTypeEnum qwtValue))
                {
                    templateConfig.Defaults.QuarterWeekType = qwtValue;
                }
                if (Enum.TryParse(GetStringParameter(nameof(templateConfig.Defaults.WeeklyType)), out DaxTemplateConfig.WeeklyTypeEnum wtValue))
                {
                    templateConfig.Defaults.WeeklyType = wtValue;
                }
                daxTemplateConfigs.Add(templateConfig);

                int? GetIntParameter(string? parameterName)
                {
                    var value = GetStringParameter(parameterName);
                    if (value == null) return null;
                    if (int.TryParse(value, out var valueInt)) return valueInt;
                    return null;
                }

                string? GetStringParameter(string? parameterName)
                {
                    if (string.IsNullOrEmpty(parameterName)) return null;
                    if (package?.Configuration.DefaultVariables.TryGetValue($"__{parameterName}", out string? value) == true)
                    {
                        return value;
                    }
                    return null;
                }
            }
            return daxTemplateConfigs.ToArray();
        }
    }
}
