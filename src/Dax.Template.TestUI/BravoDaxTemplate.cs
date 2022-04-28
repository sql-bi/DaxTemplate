using Dax.Template;
using Dax.Template.Enums;
using Dax.Template.Exceptions;
using Dax.Template.Model;
using Microsoft.AnalysisServices.AdomdClient;
using TOM = Microsoft.AnalysisServices.Tabular;
using System.Text.Json.Serialization;

namespace Dax.Template.TestUI.Bravo
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
            Weekly445 = 445,
            Weekly454 = 454,
            Weekly544 = 544
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
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public WeeklyTypeEnum? WeeklyType { get; set; }

        }

        public DaxTemplateConfig(string templatePath)
            => TemplatePath = templatePath;

        /// <summary>
        /// This property is for internal use, it must not be shown in Bravo UI
        /// </summary>
        public string TemplatePath { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
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

        /// <summary>
        /// Apply template or just preview result for a template with a specific configuration
        /// </summary>
        /// <param name="config">Template and configuration</param>
        /// <param name="model">Model on which the template is applied</param>
        /// <param name="connectionString">Connection string for Adomd used to preview changes</param>
        /// <param name="commitChanges">TRUE to commit changes</param>
        /// <param name="previewRows">Number of rows to include in data preview</param>
        /// <returns>Changes applied to the model</returns>
        /// <exception cref="TemplateException">Errors executing template</exception>
        public static ModelChanges? ApplyTemplate(DaxTemplateConfig config, TOM.Model model, string connectionString, bool commitChanges, int previewRows = 5) 
        {
            var package = Package.LoadFromFile(config.TemplatePath);

            CopyConfiguration();
            Engine templateEngine = new(package);
            templateEngine.ApplyTemplates(model, null);
            var modelChanges = Engine.GetModelChanges(model);
            
            if (commitChanges)
            {
                model.SaveChanges();
            }
            else
            {
                // Only for preview data
                AdomdConnection connection = new(connectionString);
                modelChanges.PopulatePreview(connection, model, previewRows);
            }
            return modelChanges;

            void CopyConfiguration()
            {
                package.Configuration.IsoCountry = config.IsoCountry ?? package.Configuration.IsoCountry;
                package.Configuration.IsoFormat = config.IsoFormat ?? package.Configuration.IsoFormat;
                package.Configuration.IsoTranslation = config.IsoTranslation ?? package.Configuration.IsoTranslation;
                package.Configuration.AutoScan = config.AutoScan ?? package.Configuration.AutoScan;
                package.Configuration.AutoNaming = config.AutoNaming ?? package.Configuration.AutoNaming;

                SetIntVariable(nameof(config.Defaults.FirstFiscalMonth), config.Defaults.FirstFiscalMonth);
                SetIntVariable(nameof(config.Defaults.FirstDayOfWeek), (int?)config.Defaults.FirstDayOfWeek);
                SetIntVariable(nameof(config.Defaults.MonthsInYear), config.Defaults.MonthsInYear);
                SetStringVariable(nameof(config.Defaults.WorkingDayType), config.Defaults.WorkingDayType);
                SetStringVariable(nameof(config.Defaults.NonWorkingDayType), config.Defaults.NonWorkingDayType);
                SetIntVariable(nameof(config.Defaults.TypeStartFiscalYear), (int?)config.Defaults.TypeStartFiscalYear);
                SetStringVariable(nameof(config.Defaults.QuarterWeekType), (int?)config.Defaults.QuarterWeekType);
                SetStringVariable(nameof(config.Defaults.WeeklyType), config.Defaults.WeeklyType);

                if (config.FirstYear != null)
                {
                    package.Configuration.FirstYear = config.FirstYear;
                    package.Configuration.FirstYearMin = config.FirstYear;
                    package.Configuration.FirstYearMax = config.FirstYear;
                }
                if (config.LastYear != null)
                {
                    package.Configuration.LastYear = config.LastYear;
                    package.Configuration.LastYearMin = config.LastYear;
                    package.Configuration.LastYearMax = config.LastYear;
                }
                if (config.OnlyTablesColumns?.Length > 0)
                {
                    package.Configuration.OnlyTablesColumns = config.OnlyTablesColumns.ToArray();
                }
                if (config.ExceptTablesColumns?.Length > 0)
                {
                    package.Configuration.ExceptTablesColumns = config.ExceptTablesColumns.ToArray();
                }
                if (config.TargetMeasures?.Length > 0)
                {
                    var targetMeasures =
                        from measureName in config.TargetMeasures
                        select new Dax.Template.Interfaces.IMeasureTemplateConfig.TargetMeasure() { Name = measureName };
                    package.Configuration.TargetMeasures = targetMeasures.ToArray();
                }
            }

            void SetStringVariable<T>(string parameterName, T? value)
            {
                SetVariable(parameterName, value, "\"");
            }
            void SetIntVariable<T>(string parameterName, T? value)
            {
                SetVariable(parameterName, value, "");
            }
            void SetVariable<T>(string parameterName, T? value, string quote)
            {
                if ((value == null) || (package == null)) return;
                string key = $"__{parameterName}";
                if (!package.Configuration.DefaultVariables.ContainsKey(key))
                {
                    throw new TemplateException($"Invalid {key} variable.");
                }
                string? variableValue = value.ToString();
                if (variableValue == null)
                {
                    throw new TemplateException($"Null value for {key} variable.");
                }
                package.Configuration.DefaultVariables[key] = $"{quote}{variableValue}{quote}";
            }
        }

        /// <summary>
        /// Retrieve the templates available scanning all the files in the provided path
        /// </summary>
        /// <param name="path">Path to scan for template configuration files</param>
        /// <returns>Array of template configurations</returns>
        /// <exception cref="TemplateException">Error retrieving template configuration</exception>
        public static DaxTemplateConfig[] GetTemplates(string path)
        {
            List<DaxTemplateConfig> daxTemplateConfigs = new ();
            var templateFiles = Directory.EnumerateFiles(path, TEMPLATEJSON_WILDCARD );
            foreach( var templatePath in templateFiles)
            {
                var package = Package.LoadFromFile(templatePath);
                if (package?.Configuration == null)
                {
                    throw new TemplateException($"Configuration {templatePath} not loaded.");
                }
                DaxTemplateConfig templateConfig = new(templatePath) {
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
                templateConfig.Defaults.WorkingDayType = GetQuotedStringParameter(nameof(templateConfig.Defaults.WorkingDayType));
                templateConfig.Defaults.NonWorkingDayType = GetQuotedStringParameter(nameof(templateConfig.Defaults.NonWorkingDayType));
                templateConfig.Defaults.TypeStartFiscalYear = (DaxTemplateConfig.TypeStartFiscalYear?)GetIntParameter(nameof(templateConfig.Defaults.TypeStartFiscalYear));
                if (Enum.TryParse(GetQuotedStringParameter(nameof(templateConfig.Defaults.QuarterWeekType)), out DaxTemplateConfig.QuarterWeekTypeEnum qwtValue))
                {
                    templateConfig.Defaults.QuarterWeekType = qwtValue;
                }
                if (Enum.TryParse(GetQuotedStringParameter(nameof(templateConfig.Defaults.WeeklyType)), out DaxTemplateConfig.WeeklyTypeEnum wtValue))
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

                string? GetQuotedStringParameter(string? parameterName)
                {
                    var value = GetStringParameter(parameterName);
                    if (string.IsNullOrEmpty(value)) return null;
                    if ((value[0] == '"') && (value[^1] == '"'))
                    {
                        value = value[1..^1];
                    } 
                    return value;
                }
            }
            return daxTemplateConfigs.ToArray();
        }
    }
}
