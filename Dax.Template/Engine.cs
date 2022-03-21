using Dax.Template.Enums;
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

            ApplyConfigurationDefaults();
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

        public void ApplyTemplates(TabularModel model, CancellationToken? cancellationToken)
        {
            (string className, Action<ITemplates.TemplateEntry, CancellationToken?> action)[] classes = new (string, Action<ITemplates.TemplateEntry, CancellationToken?>)[]
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
                    cancellationToken?.ThrowIfCancellationRequested();
                    var (className, action) = classes.First(c => c.className == template.Class);
                    action(template, cancellationToken);
                });
            }

            void ApplyHolidaysDefinitionTable(ITemplates.TemplateEntry templateEntry, CancellationToken? cancellationToken)
            {
                Table tableHolidaysDefinition = model.Tables.Find(templateEntry.Table);
                if (!templateEntry.IsEnabled)
                {
                    if (Configuration.HolidaysReference != null)
                        Configuration.HolidaysReference.IsEnabled = false;

                    if (tableHolidaysDefinition != null)
                        model.Tables.Remove(tableHolidaysDefinition);

                    return;
                }
                if (tableHolidaysDefinition == null)
                {
                    tableHolidaysDefinition = new Table { Name = templateEntry.Table };
                    if (model.Database.CompatibilityLevel >= 1540)
                        tableHolidaysDefinition.LineageTag = Guid.NewGuid().ToString();
                    model.Tables.Add(tableHolidaysDefinition);
                }
                CalculatedTableTemplateBase template;
                if (string.IsNullOrWhiteSpace(templateEntry.Template))
                {
                    throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
                }
                template = new HolidaysDefinitionTable(_package.ReadDefinition<HolidaysDefinitionTable.HolidaysDefinitions>(templateEntry.Template));
                template.ApplyTemplate(tableHolidaysDefinition, cancellationToken, templateEntry.IsHidden);
                tableHolidaysDefinition.RequestRefresh(RefreshType.Full);
            }
            void ApplyHolidaysTable(ITemplates.TemplateEntry templateEntry, CancellationToken? cancellationToken)
            {
                Table tableHolidays = model.Tables.Find(templateEntry.Table);
                if (!templateEntry.IsEnabled)
                {
                    if (Configuration.HolidaysReference != null)
                        Configuration.HolidaysReference.IsEnabled = false;

                    if (tableHolidays != null)
                        model.Tables.Remove(tableHolidays);

                    return;
                }
                if (tableHolidays == null)
                {
                    tableHolidays = new Table { Name = templateEntry.Table };
                    if (model.Database.CompatibilityLevel >= 1540)
                        tableHolidays.LineageTag = Guid.NewGuid().ToString();
                    model.Tables.Add(tableHolidays);
                }
                CalculatedTableTemplateBase template;
                template = new HolidaysTable(Configuration);
                template.ApplyTemplate(tableHolidays, cancellationToken, templateEntry.IsHidden);
                tableHolidays.RequestRefresh(RefreshType.Full);
            }
            void ApplyCustomDateTable(ITemplates.TemplateEntry templateEntry, CancellationToken? cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(templateEntry.Template))
                {
                    throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
                }
                if (string.IsNullOrWhiteSpace(templateEntry.Table))
                {
                    throw new InvalidConfigurationException($"Undefined Table property in class {templateEntry.Class} configuration");
                }
                if (!templateEntry.IsEnabled)
                {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(templateEntry.ReferenceTable))
                {
                    ReferenceCalculatedTable hiddenDateTemplate = CreateDateTable(
                        templateEntry.ReferenceTable,
                        templateEntry.Template,
                        model,
                        hideTable: true,
                        isoFormat: Configuration.IsoFormat,
                        cancellationToken);
                }
                bool translationsEnabled = !string.IsNullOrWhiteSpace(Configuration.IsoTranslation);
                ReferenceCalculatedTable visibleDateTemplate = CreateDateTable(
                    templateEntry.Table, 
                    templateEntry.Template, 
                    model, 
                    hideTable: templateEntry.IsHidden, 
                    isoFormat: Configuration.IsoFormat,
                    cancellationToken,
                    referenceTable: templateEntry.ReferenceTable, 
                    applyTranslations: translationsEnabled);

            }
            void ApplyMeasuresTemplate(ITemplates.TemplateEntry templateEntry, CancellationToken? cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(templateEntry.Template))
                {
                    throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
                }
                var measuresTemplateDefinition = _package.ReadDefinition<MeasuresTemplateDefinition>(templateEntry.Template);
                var template = new MeasuresTemplate(Configuration, measuresTemplateDefinition, templateEntry.Properties);
                template.ApplyTemplate(model, templateEntry.IsEnabled, cancellationToken);
            }
        }

        private Translations.Definitions ReadTranslations(CancellationToken? cancellationToken)
        {
            Translations.Definitions translations = new();
            foreach (var localizationFile in Configuration.LocalizationFiles!)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                Translations.Definitions definitions = _package.ReadDefinition<Translations.Definitions>(localizationFile);
                translations.Translations = translations.Translations.Union(definitions.Translations).ToArray();
            }
            return translations;
        }

        private ReferenceCalculatedTable CreateDateTable(
            string dateTableName,
            // TODO: if existing table has a different name, we should handle the replacement
            // string? existingDateTableName,
            string templateFilename,
            TabularModel model, 
            bool hideTable,
            string? isoFormat,
            CancellationToken? cancellationToken,
            string? referenceTable = null, 
            bool applyTranslations = false)
        {
            Table tableDate = model.Tables.Find(dateTableName);
            if (tableDate == null)
            {
                tableDate = new Table { Name = dateTableName };
                if (model.Database.CompatibilityLevel >= 1540)
                    tableDate.LineageTag = Guid.NewGuid().ToString();
                model.Tables.Add(tableDate);
            }
            Translations? translations = null;
            if (applyTranslations)
            {
                translations = new(ReadTranslations(cancellationToken));
                translations.DefaultIso = Configuration.IsoTranslation;
            }
            ReferenceCalculatedTable template;

            template = new CustomDateTable(Configuration, _package.ReadDefinition<CustomDateTemplateDefinition>(templateFilename), model)
            {
                Translation = translations,
                HiddenTable = referenceTable,
                IsoFormat = isoFormat
            };

            template.ApplyTemplate(tableDate, cancellationToken, hideTable);

            tableDate.RequestRefresh(RefreshType.Full);

            return template;
        }

        private void ApplyConfigurationDefaults()
        {
            //
            // ITemplates
            //
            Configuration.Templates ??= Array.Empty<ITemplates.TemplateEntry>();
            //
            // ILocalization
            //
            Configuration.LocalizationFiles ??= Array.Empty<string>();
            //
            // IScanConfig
            //
            Configuration.OnlyTablesColumns ??= Array.Empty<string>();
            Configuration.ExceptTablesColumns ??= Array.Empty<string>();
            // Add template tables to excluded tables
            var templateTables = from item in Configuration.Templates
                                 where !string.IsNullOrWhiteSpace(item.Table)
                                 select item.Table;
            Configuration.ExceptTablesColumns = Configuration.ExceptTablesColumns.Union(templateTables).Distinct().ToArray();
            //
            // IHolidaysConfig
            //
            Configuration.WorkingDays ??= "{ 2, 3, 4, 5, 6 }";
            Configuration.InLieuOfPrefix ??= "(in lieu of ";
            Configuration.InLieuOfSuffix ??= ")";
            //
            // IMeasureTemplateConfig
            //
            Configuration.AutoNaming ??= AutoNamingEnum.Suffix;
            Configuration.AutoNamingSeparator ??= " ";
            Configuration.TargetMeasures ??= Array.Empty<IMeasureTemplateConfig.TargetMeasure>();
        }
    }
}

