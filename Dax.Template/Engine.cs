using System;
using System.IO;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Measures;
using Dax.Template.Tables;
using Dax.Template.Tables.Dates;
using Dax.Template.Interfaces;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using System.Text.Json;
using TabularJsonSerializer = Microsoft.AnalysisServices.Tabular.JsonSerializer;
using SystemJsonSerializer = System.Text.Json.JsonSerializer;

namespace Dax.Template
{
    public class Engine
    {
        public TemplateConfiguration Configuration { get; init; }
        public string PathTemplates { get; set; } = string.Empty;

        public Engine(TemplateConfiguration configuration)
        {
            Configuration = configuration;
        }

        private string GetFullPath(string filename)
        {
            return Path.Combine(PathTemplates, filename);
        }

        public void ApplyTemplates(TabularModel model)
        {
            (string className, Action<ITemplates.TemplateEntry> action)[] classes =
                new (string, Action<ITemplates.TemplateEntry>)[]
            {
                ( nameof(HolidaysDefinitionTable), ApplyHolidaysDefinitionTable ),
                ( nameof(HolidaysTable), ApplyHolidaysTable ),
                ( nameof(CustomDateTable), ApplyCustomDateTable ),
                ( nameof(MeasuresTemplate), ApplyMeasuresTemplate )
            };

            Configuration.Templates.ToList().ForEach(template =>
            {
                var (className, action) = classes.First(c => c.className == template.Class);
                action(template);
            });

            T ReadDefinition<T>(string filename)
            {
                string json = File.ReadAllText(GetFullPath(filename));
                if (SystemJsonSerializer.Deserialize(
                        json,
                        typeof(T))
                    is not T holidaysDefinition)
                {
                    throw new Exception("Invalid configuration");
                }
                return holidaysDefinition;
            }

            void ApplyHolidaysDefinitionTable(ITemplates.TemplateEntry templateEntry)
            {
                Table tableHolidaysDefinition = model.Tables.Find(templateEntry.Table);
                if (tableHolidaysDefinition == null)
                {
                    tableHolidaysDefinition = new Table { Name = templateEntry.Table };
                    model.Tables.Add(tableHolidaysDefinition);
                }
                CalculatedTableTemplateBase template;
                template = new HolidaysDefinitionTable(ReadDefinition<HolidaysDefinitionTable.HolidaysDefinitions>(templateEntry.Template));
                template.ApplyTemplate(tableHolidaysDefinition, templateEntry.IsHidden);
                tableHolidaysDefinition.RequestRefresh(RefreshType.Full);
            }
            void ApplyHolidaysTable(ITemplates.TemplateEntry templateEntry)
            {
                Table tableHolidays = model.Tables.Find(templateEntry.Table);
                if (tableHolidays == null)
                {
                    tableHolidays = new Table { Name = templateEntry.Table };
                    model.Tables.Add(tableHolidays);
                }
                CalculatedTableTemplateBase template;
                template = new HolidaysTable(Configuration);
                template.ApplyTemplate(tableHolidays, templateEntry.IsHidden);
                tableHolidays.RequestRefresh(RefreshType.Full);
            }
            void ApplyCustomDateTable(ITemplates.TemplateEntry templateEntry)
            {
                bool translationsEnabled = !string.IsNullOrWhiteSpace(Configuration.IsoTranslation);
                bool createReferenceTable = !string.IsNullOrWhiteSpace(templateEntry.ReferenceTable);
                ReferenceCalculatedTable? hiddenDateTemplate = createReferenceTable ? CreateDateTable(
                    templateEntry.ReferenceTable, 
                    templateEntry.Template, 
                    model, 
                    hideTable: true, 
                    isoFormat: Configuration.IsoFormat) : null;
                ReferenceCalculatedTable visibleDateTemplate = CreateDateTable(
                    templateEntry.Table, 
                    templateEntry.Template, 
                    model, 
                    hideTable: templateEntry.IsHidden, 
                    isoFormat: Configuration.IsoFormat, 
                    referenceTable: templateEntry.ReferenceTable, 
                    applyTranslations: translationsEnabled);

            }
            void ApplyMeasuresTemplate(ITemplates.TemplateEntry templateEntry)
            {
                var measuresTemplateDefinition = ReadDefinition<MeasuresTemplateDefinition>(templateEntry.Template);
                var template = new MeasuresTemplate(Configuration, measuresTemplateDefinition, templateEntry.Properties);
                template.ApplyTemplate(model);
            }
        }
        private Translations.Definitions ReadTranslations()
        {
            Translations.Definitions translations = new();
            foreach (var localizationFile in Configuration.LocalizationFiles)
            {
                string translationsJsonFilename = GetFullPath(localizationFile);
                string translationsJson = File.ReadAllText(translationsJsonFilename);
                if (SystemJsonSerializer.Deserialize(translationsJson, typeof(Translations.Definitions)) is not Translations.Definitions definitions) throw new Exception("Invalid translations");
                translations.Translations = translations.Translations.Union(definitions.Translations).ToArray();
            }
            return translations;
        }
        private T ReadTemplateDefinition<T>(string templateFilename) where T : CustomTemplateDefinition
        {
            string json = File.ReadAllText(GetFullPath(templateFilename));
            if (SystemJsonSerializer.Deserialize(
                    json,
                    typeof(T))
                is not T templateDefinition)
            {
                throw new Exception("Invalid configuration");
            }
            return templateDefinition;
        }
        private ReferenceCalculatedTable CreateDateTable(
            string dateTableName, 
            string templateFilename,
            TabularModel model, 
            bool hideTable,
            string? isoFormat,
            string? referenceTable = null, 
            bool applyTranslations = false)
        {
            Table tableDate = model.Tables.Find(dateTableName);
            if (tableDate == null)
            {
                tableDate = new Table { Name = dateTableName };
                model.Tables.Add(tableDate);
            }
            Translations? translations = null;
            if (applyTranslations)
            {
                translations = new(ReadTranslations());
                translations.DefaultIso = Configuration.IsoTranslation;
            }
            ReferenceCalculatedTable template;

            template = new CustomDateTable(Configuration, ReadTemplateDefinition<CustomDateTemplateDefinition>(templateFilename), model)
            {
                Translation = translations,
                HiddenTable = referenceTable,
                IsoFormat = isoFormat
            };

            template.ApplyTemplate(tableDate, hideTable);

            tableDate.RequestRefresh(RefreshType.Full);

            return template;
        }
    }
}

