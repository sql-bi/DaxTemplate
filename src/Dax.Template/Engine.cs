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

namespace Dax.Template;

public class Engine
{
    private readonly Package _package;

    public Engine(Package package)
    {
        _package = package;

        ApplyConfigurationDefaults();
    }

    public TemplateConfiguration Configuration => _package.Configuration;

    /// <summary>
    /// Names of Microsoft.AnalysisServices.Tabular (TOM) internal members read via reflection (see
    /// <see cref="Extensions.ReflectionHelper"/>) because TOM exposes no public API for the transaction
    /// log. Every name here is TOM-internal and version-fragile: re-verify each one against the installed
    /// Microsoft.AnalysisServices.Tabular package after any TOM upgrade, since a rename would only surface
    /// at runtime (an <see cref="ArgumentOutOfRangeException"/> from <see
    /// cref="Extensions.ReflectionHelper.GetPropertyValue"/>), not at compile time.
    /// </summary>
    private static class TomInternalMembers
    {
        /// <summary>TOM's internal transaction manager, reached off <see cref="TabularModel"/>.</summary>
        internal const string TxManager = "TxManager";

        /// <summary>The transaction manager's current savepoint (internal transaction log entry).</summary>
        internal const string CurrentSavepoint = "CurrentSavepoint";

        /// <summary>The savepoint's collection of changed TOM object bodies.</summary>
        internal const string AllBodies = "AllBodies";

        /// <summary>The TOM object (<see cref="Table"/>/<see cref="Measure"/>/<see cref="Column"/>/<see cref="Hierarchy"/>) that owns a changed body.</summary>
        internal const string Owner = "Owner";

        /// <summary>The owner's last-known parent table before the change, when tracked.</summary>
        internal const string LastParent = "LastParent";

        /// <summary>The owner's current parent table.</summary>
        internal const string Parent = "Parent";
    }

    /// <summary>
    /// Reaches into TOM's internal transaction log (<see cref="TomInternalMembers.TxManager"/> -&gt;
    /// <see cref="TomInternalMembers.CurrentSavepoint"/> -&gt; <see cref="TomInternalMembers.AllBodies"/>)
    /// via reflection and returns the collection of changed object bodies, or <see langword="null"/> if
    /// any hop in the chain is unavailable.
    /// </summary>
    private static IEnumerable? GetChangedBodies(TabularModel model)
    {
        object? txManager = model.GetPropertyValue(TomInternalMembers.TxManager);
        object? currentSavePoint = txManager?.GetPropertyValue(TomInternalMembers.CurrentSavepoint);
        object? allBodies = currentSavePoint?.GetPropertyValue(TomInternalMembers.AllBodies);
        return allBodies == null ? null : (IEnumerable)allBodies;
    }

    /// <summary>
    /// Diffs the changes made to <paramref name="model"/> since its last commit by reflecting into TOM's
    /// internal transaction log (see <see cref="GetChangedBodies"/> and <see cref="TomInternalMembers"/>),
    /// returning the set of added, modified, and removed tables, measures, columns, and hierarchies.
    /// </summary>
    /// <remarks>
    /// The reflected member names are TOM-internal, undocumented, and therefore version-fragile: verify
    /// them after any Microsoft.AnalysisServices.Tabular package upgrade. Also note a behavioral quirk:
    /// this method only inspects the transaction log when <c>model.HasLocalChanges</c> is <see
    /// langword="true"/>, which TOM only sets on a model connected to a server. Calling this after
    /// applying templates to a disconnected/offline model (as built for the offline test harness) returns
    /// an empty <see cref="Model.ModelChanges"/> even though the in-memory model was visibly changed; it
    /// is only meaningful against a server-connected model.
    /// </remarks>
    public static Model.ModelChanges GetModelChanges(TabularModel model, CancellationToken cancellationToken = default)
    {
        Model.ModelChanges modelChanges = new();

        if (model.HasLocalChanges)
        {
            IEnumerable? allBodies = GetChangedBodies(model);

            if (allBodies != null)
            {
                foreach (var item in allBodies)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var owner = item?.GetPropertyValue(TomInternalMembers.Owner);
                    Table? lastParent = item?.GetPropertyValue(TomInternalMembers.LastParent, false) as Table;
                    Table? parent = lastParent ?? owner?.GetPropertyValue(TomInternalMembers.Parent, false) as Table;
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
        }

        return modelChanges;
    }

    public void ApplyTemplates(TabularModel model, CancellationToken cancellationToken = default)
    {
        (string className, Action<ITemplates.TemplateEntry, CancellationToken> action)[] classes =
        [
            ( nameof(HolidaysDefinitionTable), ApplyHolidaysDefinitionTable ),
            ( nameof(HolidaysTable), ApplyHolidaysTable ),
            ( nameof(CustomDateTable), ApplyCustomDateTable ),
            ( nameof(MeasuresTemplate), ApplyMeasuresTemplate )
        ];

        if (Configuration.Templates != null)
        {
            foreach (var template in Configuration.Templates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (_, action) = classes.First(c => c.className == template.Class);
                action(template, cancellationToken);
            }

            RemoveOrphanTranslations();
        }

        void RemoveOrphanTranslations()
        {
            foreach (var culture in model.Cultures)
            {
                var orphanTranslations = culture.ObjectTranslations.Where((t) => t.Object.IsRemoved).ToArray();
                foreach (var translation in orphanTranslations)
                {
                    culture.ObjectTranslations.Remove(translation);
                }
            }

            var orphanRelationships = model.Relationships.Where(
                (r) =>
                    r.FromTable.IsRemoved
                    || r.ToTable.IsRemoved
                    || (r is SingleColumnRelationship scr && (scr.FromColumn.IsRemoved || scr.ToColumn.IsRemoved))
                ).ToArray();

            foreach (var relationship in orphanRelationships)
            {
                model.Relationships.Remove(relationship);
            }
        }

        void ApplyHolidaysDefinitionTable(ITemplates.TemplateEntry templateEntry, CancellationToken cancellationToken = default)
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
            if (string.IsNullOrWhiteSpace(templateEntry.Template))
            {
                throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
            }
            if (tableHolidaysDefinition == null)
            {
                tableHolidaysDefinition = new Table { Name = templateEntry.Table };
                if (model.Database.CompatibilityLevel >= 1540)
                    tableHolidaysDefinition.LineageTag = Guid.NewGuid().ToString();
                model.Tables.Add(tableHolidaysDefinition);
            }
            CalculatedTableTemplateBase template;
            template = new HolidaysDefinitionTable(_package.ReadDefinition<HolidaysDefinitionTable.HolidaysDefinitions>(templateEntry.Template));
            template.ApplyTemplate(tableHolidaysDefinition, templateEntry.IsHidden, cancellationToken);
            RequestTableRefresh(tableHolidaysDefinition);
        }
        void ApplyHolidaysTable(ITemplates.TemplateEntry templateEntry, CancellationToken cancellationToken = default)
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
            template.ApplyTemplate(tableHolidays, templateEntry.IsHidden, cancellationToken);
            RequestTableRefresh(tableHolidays);
        }
        void ApplyCustomDateTable(ITemplates.TemplateEntry templateEntry, CancellationToken cancellationToken = default)
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
                CreateDateTable(
                    templateEntry.ReferenceTable,
                    templateEntry.Template,
                    model,
                    hideTable: true,
                    isoFormat: Configuration.IsoFormat,
                    cancellationToken: cancellationToken);
            }
            bool translationsEnabled = !string.IsNullOrWhiteSpace(Configuration.IsoTranslation);
            CreateDateTable(
                templateEntry.Table,
                templateEntry.Template,
                model,
                hideTable: templateEntry.IsHidden,
                isoFormat: Configuration.IsoFormat,
                referenceTable: templateEntry.ReferenceTable,
                applyTranslations: translationsEnabled,
                cancellationToken);

        }
        void ApplyMeasuresTemplate(ITemplates.TemplateEntry templateEntry, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(templateEntry.Template))
            {
                throw new InvalidConfigurationException($"Undefined Template in class {templateEntry.Class} configuration");
            }
            var measuresTemplateDefinition = _package.ReadDefinition<MeasuresTemplateDefinition>(templateEntry.Template);
            var template = new MeasuresTemplate(Configuration, measuresTemplateDefinition, templateEntry.Properties);
            template.ApplyTemplate(model, isEnabled: templateEntry.IsEnabled, cancellationToken: cancellationToken);
        }
    }

    private Translations.Definitions ReadTranslations(CancellationToken cancellationToken = default)
    {
        Translations.Definitions translations = new();
        foreach (var localizationFile in Configuration.LocalizationFiles!)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        string? referenceTable = null,
        bool applyTranslations = false,
        CancellationToken cancellationToken = default)
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

        template = new CustomDateTable(Configuration, _package.ReadDefinition<CustomDateTemplateDefinition>(templateFilename), model, referenceTable)
        {
            Translation = translations,
            IsoFormat = isoFormat
        };

        template.ApplyTemplate(tableDate, hideTable, cancellationToken);

        RequestTableRefresh(tableDate);

        return template;
    }

    /// <summary>
    /// Requests a full refresh of <paramref name="table"/> only when its model is connected to a server.
    /// A disconnected (in-memory) model is read-only for refresh purposes and throws if asked to refresh,
    /// so this guard keeps server deployments unchanged while allowing offline metadata generation and tests.
    /// </summary>
    private static void RequestTableRefresh(Table table)
    {
        if (table.Model?.Server != null)
            table.RequestRefresh(RefreshType.Full);
    }

    private void ApplyConfigurationDefaults()
    {
        //
        // ITemplates
        //
        Configuration.Templates ??= [];
        //
        // ILocalization
        //
        Configuration.LocalizationFiles ??= [];
        //
        // IScanConfig
        //
        Configuration.OnlyTablesColumns ??= [];
        Configuration.ExceptTablesColumns ??= [];
        // Add template tables to excluded tables
        var templateTables = from item in Configuration.Templates
                             where !string.IsNullOrWhiteSpace(item.Table)
                             select item.Table;
        // Add also reference table if present
        templateTables = templateTables.Union(
            from item in Configuration.Templates
            where !string.IsNullOrWhiteSpace(item.ReferenceTable)
            select item.ReferenceTable
            );
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
        Configuration.AutoNaming ??= AutoNaming.Suffix;
        Configuration.AutoNamingSeparator ??= " ";
        Configuration.TargetMeasures ??= [];
    }
}