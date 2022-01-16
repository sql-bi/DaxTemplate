using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Exceptions;
using System.Text.RegularExpressions;
using Dax.Template.Extensions;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using Dax.Template.Interfaces;
using Dax.Template.Enums;

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
        const string SQLBI_TEMPLATE_ATTRIBUTE = "SQLBI_Template";

        public IMeasureTemplateConfig Config { get; init; } 
        public MeasuresTemplateDefinition Template { get; init; } 
        public MeasuresTemplate(IMeasureTemplateConfig config, MeasuresTemplateDefinition measuresTemplateDefinition)
        {
            Config = config;
            Template = measuresTemplateDefinition;
        }

        /// <summary>
        /// Returns the target measures for the template
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Measure> GetTargetMeasures(TabularModel model)
        {
            if (Config.TargetMeasures == null || Config.TargetMeasures.Length == 0)
            {
                return
                    from t in model.Tables
                    from m in t.Measures
                    select m;
            }

            IEnumerable<Measure> result = Array.Empty<Measure>();
            foreach(var tm in Config.TargetMeasures)
            {
                result = result.Union(
                    from t in model.Tables
                    from m in t.Measures
                    where m.Name == tm.Name    // TODO - modify the matching algorithm to manage wildcards and/or attributes
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
                    throw new InvalidMacroReferenceException(matchGetMinDates.Value ?? matchGetMaxDates.Value, expression, "Invalid configuration for scan columns.");
                }
                if (matchGetMinDates.Success)
                {
                    var listMin = string.Join(", ", scanColumns.Select(col => $"MIN ( '{col.Table.Name}'[{col.Name}] )"));
                    string replace = $"MINX ( {{ {listMin} }}, ''[Value] )";
                    expression = regexGetMinDates.Replace(expression, replace);
                }
                if (matchGetMaxDates.Success)
                {
                    var listMax = string.Join(", ", scanColumns.Select(col => $"MAX ( '{col.Table.Name}'[{col.Name}] )"));
                    string replace = $"MAXX ( {{ {listMax} }}, ''[Value] )";
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
        public void ApplyTemplate(TabularModel model, bool overrideExistingMeasures = true)
        {
            var targetMeasures = GetTargetMeasures(model).ToList();
            var singleInstanceMeasures = Template.MeasureTemplates.Where(mt => mt.IsSingleInstance);
            var templateMeasures = Template.MeasureTemplates.Where(mt => !mt.IsSingleInstance);

            // Retrieves the existing measures created by a previous execution of the same template type
            string? SqlbiTemplateValue = GetSqlbiTemplateValue();
            List<Measure> existingMeasuresFromSameTemplate =
                (from t in model.Tables
                 from m in t.Measures
                 where m.Annotations.Any(a =>
                    a.Name == SQLBI_TEMPLATE_ATTRIBUTE
                    && (string.IsNullOrEmpty(SqlbiTemplateValue) || a.Value == SqlbiTemplateValue))
                 select m).ToList();

            List<Measure> appliedMeasures = new();
            Table targetTable = GetTargetTable(model);
            
            // Create the individual measures of the template (not applied to single measures)
            foreach (var template in singleInstanceMeasures)
            {
                ApplyMeasureTemplate(template, targetTable, referenceMeasure: null);
            }

            // Apply the templates to each target measure
            foreach (var target in targetMeasures)
            {
                foreach (var template in templateMeasures)
                {
                    ApplyMeasureTemplate(template, targetTable, referenceMeasure: target);
                }
            }

            // Remove the existing measures created by previous executions of the same template type
            if (overrideExistingMeasures)
            {
                // Remove measures with the same SQLBI_Template attribute that have not been overwritten
                existingMeasuresFromSameTemplate.RemoveAll(m => appliedMeasures.Any(am => am.Name.Equals(m.Name)));
                foreach (var removeMeasure in existingMeasuresFromSameTemplate)
                {
                    removeMeasure.Table.Measures.Remove(removeMeasure);
                }
            }

            void ApplyMeasureTemplate(MeasuresTemplateDefinition.MeasureTemplate template, Table targetTable, Measure? referenceMeasure)
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
                    DisplayFolder = template.DisplayFolder,
                    Description = template.Description,
                    Annotations = template.Annotations.Union(Template.TemplateAnnotations),
                    Comments = template.GetComments(),
                    TemplateExpression = ReplaceMacros(template.GetExpression(), model),
                    ReferenceMeasure = referenceMeasure
                };
                var modelMeasure = measureTemplate.ApplyTemplate(model, referenceMeasure?.Parent as Table ?? targetTable, overrideExistingMeasures);
                appliedMeasures.Add(modelMeasure);
            }
        }

        /// <summary>
        /// Retrieve the SQLBI template name in execution
        /// </summary>
        /// <returns>Value of SQLBI_Template used by the current template</returns>
        private string GetSqlbiTemplateValue()
        {
            return Template.TemplateAnnotations.FirstOrDefault(a => a.Key == SQLBI_TEMPLATE_ATTRIBUTE).Value;
        }

        private static Table? FindTable(TabularModel model, string tableName)
        {
            return
                (from t in model.Tables
                 select t).FirstOrDefault(t => t.Name == tableName);
        }
        private Table GetTargetTable(TabularModel model)
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
