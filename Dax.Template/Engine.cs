using Dax.Template.Exceptions;
using Dax.Template.Extensions;
using Dax.Template.Interfaces;
using Dax.Template.Measures;
using Dax.Template.Tables;
using Dax.Template.Tables.Dates;
using Microsoft.AnalysisServices.Tabular;
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;

namespace Dax.Template
{
    public class Engine
    {
        private readonly Package _package;

        public Engine(Package package)
        {
            _package = package;
        }

        public TemplateConfiguration Configuration => _package.Configuration;

        public static Model.ModelChanges GetModelChanges(TabularModel model, CancellationToken cancellationToken = default)
        {
            object? txManager = model.GetPropertyValue("TxManager");
            object? currentSavePoint = txManager?.GetPropertyValue("CurrentSavepoint");
            object? allBodies = currentSavePoint?.GetPropertyValue("AllBodies");
            Model.ModelChanges modelChanges = new();
            if (allBodies != null)
            {
                var collection = (IEnumerable)allBodies;
                foreach (var item in collection)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var owner = item?.GetPropertyValue("Owner");
                    Table? lastParent = item?.GetPropertyValue("LastParent", false) as Table;
                    Table? parent = lastParent ?? owner?.GetPropertyValue("Parent", false) as Table;
                    switch (owner)
                    {
                        case Table table: modelChanges.AddTable(table, table.IsRemoved); break;
                        case Measure measure: modelChanges.AddMeasure(measure, parent, measure.IsRemoved); break;
                        case Column column: modelChanges.AddColumn(column, parent, column.IsRemoved); break;
                        case Hierarchy hierarchy: modelChanges.AddHierarchy(hierarchy, parent, hierarchy.IsRemoved); break;
                    }
                }
            }
            modelChanges.SimplifyRemovedObjects(cancellationToken);
            return modelChanges;
        }

        public void ApplyTemplates(TabularModel model, CancellationToken cancellationToken = default)
        {
            (string className, Action<ITemplates.TemplateEntry> action)[] classes =
                new (string, Action<ITemplates.TemplateEntry>)[]
            {
                ( nameof(HolidaysDefinitionTable), ApplyHolidaysDefinitionTable ),
                ( nameof(HolidaysTable), ApplyHolidaysTable ),
                ( nameof(CustomDateTable), ApplyCustomDateTable ),
                ( nameof(MeasuresTemplate), ApplyMeasuresTemplate )
            };

            if (Configuration.Templates != null)
            {
                Configuration.Templates.ToList().ForEach(template =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (className, action) = classes.First(c => c.className == template.Class);
                    action(template);
                });
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
                if (string.IsNullOrWhiteSpace(templateEntry.Template))
                {
                    throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
                }
                template = new HolidaysDefinitionTable(_package.ReadDefinition<HolidaysDefinitionTable.HolidaysDefinitions>(templateEntry.Template));
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
                ReferenceCalculatedTable? hiddenDateTemplate = null;
                if (string.IsNullOrWhiteSpace(templateEntry.Template))
                {
                    throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
                }
                if (string.IsNullOrWhiteSpace(templateEntry.Table))
                {
                    throw new InvalidConfigurationException($"Undefined Table property in class {templateEntry.Class} configuration");
                }
                if (!string.IsNullOrWhiteSpace(templateEntry.ReferenceTable))
                {
                    hiddenDateTemplate = CreateDateTable(
                        templateEntry.ReferenceTable,
                        templateEntry.Template,
                        model,
                        hideTable: true,
                        isoFormat: Configuration.IsoFormat);
                }
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
                if (string.IsNullOrWhiteSpace(templateEntry.Template))
                {
                    throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
                }
                var measuresTemplateDefinition = _package.ReadDefinition<MeasuresTemplateDefinition>(templateEntry.Template);
                var template = new MeasuresTemplate(Configuration, measuresTemplateDefinition, templateEntry.Properties);
                template.ApplyTemplate(model);
            }
        }

        private Translations.Definitions ReadTranslations()
        {
            Translations.Definitions translations = new();
            foreach (var localizationFile in Configuration.LocalizationFiles)
            {
                Translations.Definitions definitions = _package.ReadDefinition<Translations.Definitions>(localizationFile);
                translations.Translations = translations.Translations.Union(definitions.Translations).ToArray();
            }
            return translations;
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

            template = new CustomDateTable(Configuration, _package.ReadDefinition<CustomDateTemplateDefinition>(templateFilename), model)
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

