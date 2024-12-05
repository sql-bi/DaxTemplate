using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Exceptions;
using System.Text.RegularExpressions;
using Dax.Template.Extensions;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using TabularMeasure = Microsoft.AnalysisServices.Tabular.Measure;
using Dax.Template.Interfaces;
using Dax.Template.Enums;
using Dax.Template.Constants;
using System.Threading;

// TODO: implement logic to match targetable based on annotations

namespace Dax.Template.Measures
{
    /// <summary>
    /// This class creates a measures template based on the external definition in a JSON file.
    /// </summary>
    public class MeasuresTemplateDefinition
    {
        public class MeasureTemplate 
        {
            public string? Name { get; init; }
            public string? FormatString { get; set; }
            public bool IsHidden { get; set; } = false;
            public bool IsSingleInstance { get; set; } = false;
            public string? DisplayFolder { get; set; }
            public string? Description { get; set; }
            public Dictionary<string, string> Annotations { get; set; } = new();
            public string? Comment { get; set; }
            public string[]? MultiLineComment { get; set; }
            public string? Expression { get; set; }
            public string[]? MultiLineExpression { get; set; }
            public string? GetExpression()
            {
                return (string.IsNullOrEmpty(Expression) && MultiLineExpression != null)
                    ? string.Join("", MultiLineExpression.Select(line => $"\r\n{line}"))
                    : Expression;
            }
            public string[]? GetComments()
            {
                return (MultiLineComment != null && MultiLineComment.Length > 0)
                    ? MultiLineComment
                    : (!string.IsNullOrWhiteSpace(Comment) ? new string[] { Comment } : null);
            }
        }
        public Dictionary<string, string> TargetTable { get; set; } = new();
        public Dictionary<string, string> TemplateAnnotations { get; set; } = new();
        public MeasureTemplate[] MeasureTemplates { get; set;} = Array.Empty<MeasureTemplate>();
    }

    public class MeasuresTemplate
    {
        const string PROPERTY_DISPLAYFOLDERRULE = "DisplayFolderRule";
        const string PROPERTY_DISPLAYFOLDERRULESINGLEINSTANCEMEASURES = "DisplayFolderRuleSingleInstanceMeasures";

        public IMeasureTemplateConfig Config { get; init; }
        public MeasuresTemplateDefinition Template { get; init; }
        public Dictionary<string, object> Properties { get; init; }
        public MeasuresTemplate(IMeasureTemplateConfig config, MeasuresTemplateDefinition measuresTemplateDefinition, Dictionary<string, object> properties)
        {
            Config = config;
            Template = measuresTemplateDefinition;
            Properties = properties;
        }

        public string? DisplayFolderRule
        {
            get => Properties.GetValueOrDefault(PROPERTY_DISPLAYFOLDERRULE)?.ToString();
        }
        public string? DisplayFolderRuleSingleInstanceMeasures
        {
            get => Properties.GetValueOrDefault(PROPERTY_DISPLAYFOLDERRULESINGLEINSTANCEMEASURES)?.ToString();
        }

        /// <summary>
        /// Returns the target measures for the template
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Measure> GetTargetMeasures(TabularModel model, CancellationToken cancellationToken = default)
        {
            if (Config.TargetMeasures == null || Config.TargetMeasures.Length == 0)
            {
                return
                    from t in model.Tables
                    from m in t.Measures
                    where !m.Annotations.Any(a => a.Name == Attributes.SQLBI_TEMPLATE_ATTRIBUTE)
                    select m;
            }

            IEnumerable<Measure> result = Array.Empty<Measure>();
            foreach(var tm in Config.TargetMeasures)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                result = result.Union(
                    from t in model.Tables
                    from m in t.Measures
                    where m.Name == tm.Name    // TODO - modify the matching algorithm to manage wildcards and/or attributes
                       && !m.Annotations.Any(a => a.Name == Attributes.SQLBI_TEMPLATE_ATTRIBUTE)
                    select m
                );
            }
            return result.Distinct();
        }

        private static readonly Regex regexGetMinDates = new(@"@@GETMINDATE[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);
        private static readonly Regex regexGetMaxDates = new(@"@@GETMAXDATE[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);
        protected string? ReplaceMacros( string? expression, TabularModel model )
        {
            if (expression == null) return expression;
            var matchGetMinDates = regexGetMinDates.Match(expression);
            var matchGetMaxDates = regexGetMaxDates.Match(expression);
            if (matchGetMinDates.Success || matchGetMaxDates.Success)
            {
                var scanColumns = model.GetScanColumns(Config);
                if (scanColumns == null)
                {
                    if (Config.AutoScan == AutoScanEnum.Disabled)
                    {
                        if (matchGetMinDates.Success)
                        {
                            expression = regexGetMinDates.Replace(expression, "TODAY()");
                        }
                        if (matchGetMaxDates.Success)
                        {
                            expression = regexGetMaxDates.Replace(expression, "TODAY()");
                        }
                        return expression;
                    }
                    else
                    {
                        throw new InvalidMacroReferenceException(matchGetMinDates.Value ?? matchGetMaxDates.Value, expression, "Invalid configuration for scan columns.");
                    }
                }
                if (matchGetMinDates.Success)
                {
                    var listMin = string.Join(", ", scanColumns.Select(col => $"MIN ( '{col.Table.Name.GetDaxTableName()}'[{col.Name.GetDaxColumnName()}] )"));
                    string replace = listMin.IsNullOrEmpty() ? "TODAY()" : $"MINX ( {{ {listMin} }}, ''[Value] )";
                    expression = regexGetMinDates.Replace(expression, replace);
                }
                if (matchGetMaxDates.Success)
                {
                    var listMax = string.Join(", ", scanColumns.Select(col => $"MAX ( '{col.Table.Name.GetDaxTableName()}'[{col.Name.GetDaxColumnName()}] )"));
                    string replace = listMax.IsNullOrEmpty() ? "TODAY()" : $"MAXX ( {{ {listMax} }}, ''[Value] )";
                    expression = regexGetMaxDates.Replace(expression, replace);
                }
            }
            return expression;
        }
        protected internal virtual string GetTargetMeasureName(string templateName, string referenceMeasureName)
        {
            string prefix = (Config.AutoNaming == AutoNamingEnum.Prefix) ? $"{templateName}{Config.AutoNamingSeparator}" : string.Empty;
            string suffix = (Config.AutoNaming == AutoNamingEnum.Suffix) ? $"{Config.AutoNamingSeparator}{templateName}" : string.Empty;
            return $"{prefix}{referenceMeasureName}{suffix}";
        }

        public void ApplyTemplate(TabularModel model, bool isEnabled, bool overrideExistingMeasures = true, CancellationToken cancellationToken = default)
        {
            // Retrieves the existing measures created by a previous execution of the same template type
            string? SqlbiTemplateValue = GetSqlbiTemplateValue();
            List<Measure> existingMeasuresFromSameTemplate =
                (from t in model.Tables
                 from m in t.Measures
                 where m.Annotations.Any(a =>
                    a.Name == Attributes.SQLBI_TEMPLATE_ATTRIBUTE
                    && (string.IsNullOrEmpty(SqlbiTemplateValue) || a.Value == SqlbiTemplateValue))
                 select m).ToList();

            if (!isEnabled)
            {
                existingMeasuresFromSameTemplate.ForEach((measure) => measure.Table.Measures.Remove(measure.Name));
                return;
            }

            List<Measure> appliedMeasures = new();
            Table targetTable = GetTargetTable(model, cancellationToken);

            Table targetTableSingleInstanceMeasures = (!string.IsNullOrEmpty(Config.TableSingleInstanceMeasures))
                ? FindTable(model, Config.TableSingleInstanceMeasures) ?? targetTable : targetTable;

            var targetMeasures = GetTargetMeasures(model, cancellationToken).ToList();
            var singleInstanceMeasures = Template.MeasureTemplates.Where(mt => mt.IsSingleInstance);
            var templateMeasures = Template.MeasureTemplates.Where(mt => !mt.IsSingleInstance);

            // Create the individual measures of the template (not applied to single measures)
            foreach (var template in singleInstanceMeasures)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                ApplyMeasureTemplate(template, targetTableSingleInstanceMeasures, referenceMeasure: null, cancellationToken);
            }

            // Apply the templates to each target measure
            foreach (var target in targetMeasures)
            {
                foreach (var template in templateMeasures)
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    ApplyMeasureTemplate(template, targetTable, referenceMeasure: target, cancellationToken);
                }
            }

            // Remove the existing measures created by previous executions of the same template type
            if (overrideExistingMeasures)
            {
                // Remove measures with the same SQLBI_Template attribute that have not been overwritten
                existingMeasuresFromSameTemplate.RemoveAll(m => appliedMeasures.Any(am => am.Name.Equals(m.Name)));
                foreach (var removeMeasure in existingMeasuresFromSameTemplate)
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    removeMeasure.Table.Measures.Remove(removeMeasure);
                }
            }

            void ApplyMeasureTemplate(MeasuresTemplateDefinition.MeasureTemplate template, Table targetTable, Measure? referenceMeasure, CancellationToken cancellationToken = default)
            {
                if (template.Name == null)
                {
                    throw new TemplateException("Undefined measure template name");
                }
                var x = template.Annotations.Union(Template.TemplateAnnotations);
                MeasureTemplateBase measureTemplate = new(this)
                {
                    Name = (referenceMeasure != null) ? GetTargetMeasureName(template.Name, referenceMeasure.Name) : template.Name,
                    FormatString = template.FormatString,
                    IsHidden = template.IsHidden,
                    DisplayFolder = GetDisplayFolder( referenceMeasure, template.DisplayFolder, template.Name),
                    Description = template.Description,
                    Annotations = template.Annotations.Union(Template.TemplateAnnotations),
                    Comments = template.GetComments(),
                    TemplateExpression = ReplaceMacros(template.GetExpression(), model),
                    ReferenceMeasure = referenceMeasure,
                    DefaultVariables = Config.DefaultVariables
                };
                var modelMeasure = measureTemplate.ApplyTemplate(model, referenceMeasure?.Parent as Table ?? targetTable, cancellationToken, overrideExistingMeasures);
                appliedMeasures.Add(modelMeasure);
            }
        }

        private static readonly Regex regexMeasureName = new(@"@_MEASURE_@", RegexOptions.Compiled);
        private static readonly Regex regexTemplateName = new(@"@_TEMPLATE_@", RegexOptions.Compiled);
        private static readonly Regex regexMeasureFolder = new(@"@_MEASUREFOLDER_@", RegexOptions.Compiled);
        private static readonly Regex regexTemplateFolder = new(@"@_TEMPLATEFOLDER_@", RegexOptions.Compiled);
        protected virtual string? GetDisplayFolder(TabularMeasure? measure, string? templateDisplayFolder, string? templateName)
        {
            string? folderRule = 
                (measure != null) 
                ? DisplayFolderRule 
                : DisplayFolderRuleSingleInstanceMeasures ?? DisplayFolderRule;
            if (string.IsNullOrWhiteSpace(folderRule))
            {
                return templateDisplayFolder;
            }
            else
            {
                string displayFolder = regexMeasureName.Replace(folderRule, measure?.Name ?? string.Empty);
                displayFolder = regexTemplateName.Replace(displayFolder, templateName ?? string.Empty);
                displayFolder = regexMeasureFolder.Replace(displayFolder, measure?.DisplayFolder ?? string.Empty);
                displayFolder = regexTemplateFolder.Replace(displayFolder, templateDisplayFolder ?? string.Empty);
                displayFolder = displayFolder.Replace(@"\\", @"\");
                return displayFolder;
            }
        }

        /// <summary>
        /// Retrieve the SQLBI template name in execution
        /// </summary>
        /// <returns>Value of SQLBI_Template used by the current template</returns>
        private string GetSqlbiTemplateValue()
        {
            return Template.TemplateAnnotations.FirstOrDefault(a => a.Key == Attributes.SQLBI_TEMPLATE_ATTRIBUTE).Value;
        }

        private static Table? FindTable(TabularModel model, string tableName)
        {
            return
                (from t in model.Tables
                 select t).FirstOrDefault(t => t.Name == tableName);
        }
        private Table GetTargetTable(TabularModel model, CancellationToken cancellationToken = default)
        {
            Table? targetTable = null;
            var targetTableName = Template.TargetTable.FirstOrDefault(tt => tt.Key == "Name").Value;
            if (targetTableName != null)
            {
                targetTable = FindTable(model, targetTableName);
            }
            if (targetTable == null)
            {
                foreach (var tt in Template.TargetTable)
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    var tables = MeasureTemplateBase.GetTablesFromAnnotations(model, tt.Key, tt.Value);
                    if (!tables.Any())
                    {
                        throw new TemplateException($"No target tables found for attribute {tt.Key}={tt.Value}");
                    }
                    if (tables.Count() > 1)
                    {
                        string tableList = string.Join(", ", tables.Select(t => $"'{t.Name}'"));
                        throw new TemplateException($"Multiple tables found in TargetTable for attribute {tt.Key}={tt.Value} : {tableList}");
                    }
                    if (targetTable != null)
                    {
                        string tableList = string.Join(", ", tables.Select(t => $"'{t.Name}'"));
                        throw new TemplateException($"Additional tables found in TargetTable for attribute {tt.Key}={tt.Value} : {tableList}");
                    }
                    targetTable = tables.First();
                }
            }
            if (targetTable == null)
            {
                string attributeList = string.Join(", ", Template.TargetTable.Select(tt => $"{tt.Key}={tt.Value}"));
                throw new TemplateException($"Target tables not found: {attributeList}");
            }

            return targetTable;
        }
    }
}
