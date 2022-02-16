using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dax.Template.Interfaces;
using Dax.Template.Tables.Dates;
using Dax.Template.Enums;
using System.IO;

namespace Dax.Template.Tables
{

    public class TemplateConfiguration: IScanConfig, IDateTemplateConfig, IMeasureTemplateConfig, IHolidaysConfig, ICustomTableConfig, ITemplates, ILocalization
    {
        public string? TemplateUri { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }

        // ITemplates implementation
        public ITemplates.TemplateEntry[]? Templates { get; set; }

        // ILocalization implementation
        public string? IsoTranslation { get; set; }
        public string? IsoFormat { get; set; }
        public string[]? LocalizationFiles { get; set; }

        // IScanConfig implementation
        public string[]? OnlyTablesColumns { get; set; }
        public string[]? ExceptTablesColumns { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AutoScanEnum? AutoScan { get; set; }

        // IDateTemplateConfig implementation
        public int? FirstYearMin { get; set; }
        public int? FirstYearMax { get; set; }
        public int? LastYearMin { get; set; }
        public int? LastYearMax { get; set; }
        public int? FirstYear { get; set; }
        public int? LastYear { get; set; }

        // ICustomTableConfig implementation
        public Dictionary<string, string> DefaultVariables { get; set; } = new();

        // IHolidaysConfig implementation
        public string? IsoCountry { get; set; }
        public string? InLieuOfPrefix { get; set; }
        public string? InLieuOfSuffix { get; set; }
        public string? HolidaysDefinitionTable { get; set; }
        public string? WorkingDays { get; set; }

        public HolidaysConfig? HolidaysReference { get; set; }

        // IMeasureTemplateConfig implementation
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AutoNamingEnum? AutoNaming { get; set; }
        public string? AutoNamingSeparator { get; set; }
        public IMeasureTemplateConfig.TargetMeasure[]? TargetMeasures { get; set; }
        public string? TableSingleInstanceMeasures { get; set; }

        public string? DisplayFolderRule { get; set; }
    }

    public static class TemplateConfigurationExtensions
    {
        public static string ToTemplateUri(this FileInfo file)
        {
            var uriBuilder = new UriBuilder(file.FullName)
            {
                Scheme = Uri.UriSchemeFile
            };
            
            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}
