using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Interfaces;
using Dax.Template.Exceptions;
using System.Text.RegularExpressions;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using TabularMeasure = Microsoft.AnalysisServices.Tabular.Measure;

namespace Dax.Template.Measures
{
    public class MeasureTemplateBase: Model.Measure
    {
        class MultipleMatchesException : Exception
        {
            public string[] Matches { get; init; }
            public MultipleMatchesException( string[] matches ) : base()
            {
                Matches = matches;
            }
        }

        protected readonly MeasuresTemplate Template;

        public MeasureTemplateBase(MeasuresTemplate template) : base()
        {
            Template = template;
        }

        public override string? Expression
        {
            get => (ReferenceMeasure != null) 
                ? GetDaxExpression(ReferenceMeasure.Model, ReferenceMeasure.Name)
                : throw new TemplateException($"ReferenceExpression not defined in {Name} template measure");
        }

        public TabularMeasure? ReferenceMeasure { get; set; }

        /// <summary>
        /// Template to generate the DAX code for Expression
        /// </summary>
        public string? TemplateExpression { get; set; }

        private static readonly Regex regexFindPlaceholders = new(@"@_(?<entity>.*?)-(?<attribute>.*?)(-(?<value>.*?))?_@", RegexOptions.Compiled);
        private static readonly Regex regexGetMeasure = new(@"@@GETMEASURE[ \r\n\t]*\((?<templateName>[^\)]*?)\)", RegexOptions.Compiled);

        private static string? GetGroupValue( Match match, string groupName)
        {
            return (match.Success && match.Groups.ContainsKey(groupName))
                        ? match.Groups[groupName].Value : null;
        }

        public const string ENTITY_SINGLE_COLUMN = "C";
        public const string ENTITY_COLUMNS_LIST = "CL";
        public const string ENTITY_SINGLE_TABLE = "T";
        public const string ENTITY_COLUMNS_TABLE = "CT";
        internal static Measure? FindMeasure(TabularModel model, string measureName)
        {
            return
                (from t in model.Tables
                 from m in t.Measures
                 select m).FirstOrDefault(m => m.Name == measureName);
        }

        public virtual Measure ApplyTemplate(TabularModel model, Table targetTable, bool overrideExistingMeasure = true)
        {
            var measure = FindMeasure(model, Name);
            if (measure == null)
            {
                measure = new TabularMeasure { Name = Name };
                targetTable.Measures.Add(measure);
            }
            measure.FormatString = FormatString ?? ReferenceMeasure?.FormatString;
            measure.IsHidden = IsHidden;
            measure.DisplayFolder = DisplayFolder;
            measure.Description = Description;
            measure.Expression = GetDaxExpression(model, ReferenceMeasure?.Name);
            ApplyAnnotations(measure);

            return measure;

            void ApplyAnnotations(TabularMeasure measure)
            {
                if (Annotations == null) return;
                foreach (var annotation in Annotations)
                {
                    var annotationName = annotation.Key;
                    var annotationValue = annotation.Value.ToString();

                    Annotation? tabularAnnotation = measure.Annotations.FirstOrDefault(a => a.Name == annotationName);
                    if (tabularAnnotation == null)
                    {
                        tabularAnnotation = new Annotation { Name = annotationName, Value = annotationValue };
                        measure.Annotations.Add(tabularAnnotation);
                    }
                    else
                    {
                        tabularAnnotation.Value = annotationValue;
                    }
                }
            }
        }
        public string GetDaxExpression(TabularModel model)
        {
            return GetDaxExpression(model, originalMeasureName: null);
        }
        public virtual string GetDaxExpression(TabularModel model, string? originalMeasureName)
        {
            if (TemplateExpression == null)
            {
                throw new TemplateException($"TemplateExpression not defined in {Name} template measure");
            }
            string result = TemplateExpression;
            var placeholders = regexFindPlaceholders.Matches(result);

            foreach( Match match in placeholders )
            {
                string? entity = GetGroupValue(match,"entity");
                string? attribute = GetGroupValue(match,"attribute");
                string? value = GetGroupValue(match,"value");
                if (attribute == null)
                {
                    throw new InvalidMacroReferenceException(match.Value, TemplateExpression);
                }

                string? replace;
                try
                {
                    replace = entity switch
                    {
                        ENTITY_SINGLE_COLUMN => FindSingleColumn(model, attribute, value),
                        ENTITY_SINGLE_TABLE => FindSingleTable(model, attribute, value),
                        ENTITY_COLUMNS_LIST => FindColumnsList(model, attribute, value),
                        ENTITY_COLUMNS_TABLE => FindTablesList(model, attribute, value),
                        _ => throw new InvalidMacroReferenceException(match.Value, TemplateExpression),
                    };
                }
                catch (MultipleMatchesException ex)
                {
                    throw new InvalidMacroReferenceException(match.Value, ex.Matches, TemplateExpression);
                }

                if (string.IsNullOrWhiteSpace(replace))
                {
                    throw new InvalidMacroReferenceException(match.Value, TemplateExpression);
                }
                result = result.Replace(match.Value, replace);
            }

            if (originalMeasureName != null)
            {
                result = regexGetMeasure.Replace(result, match =>
                {
                    string? templateName = GetGroupValue(match, "templateName")?.Trim();
                    string replaceMeasureName =
                        string.IsNullOrWhiteSpace(templateName)
                        ? originalMeasureName
                        : Template.GetTargetMeasureName(templateName, originalMeasureName);
                    return $"[{replaceMeasureName}]";
                });
            }
            else
            {
                if (regexGetMeasure.IsMatch(result))
                {
                    throw new InvalidMacroReferenceException(
                        regexGetMeasure.Match(result).Value, 
                        TemplateExpression,
                        additionalMessage: "Missing original measure, check IsSingleInstance property.");
                }
            }

            return result;
        }

        internal static IEnumerable<Table> GetTablesFromAnnotations(TabularModel model, string attribute, string? value)
        {
            return (from t in model.Tables
                    where !t.IsHidden
                    from a in t.Annotations
                    where a.Name == attribute
                          && (value == null || a.Value == value)
                    select t).Distinct();
        }

        internal static IEnumerable<Column> GetColumnsFromAnnotations(TabularModel model, string attribute, string? value)
        {
            return (from t in model.Tables
                    where !t.IsHidden
                    from c in t.Columns
                    from a in c.Annotations
                    where a.Name == attribute
                          && (value == null || a.Value == value)
                    select c).Distinct();
        }

        private static string? FindTablesList(TabularModel model, string attribute, string? value)
        {
            var tables = GetTablesFromAnnotations(model, attribute, value);
            string result = string.Join(", ", tables.Select(t => $"'{t.Name}'"));
            return result;
        }


        private static string? FindColumnsList(TabularModel model, string attribute, string? value)
        {
            var columns = GetColumnsFromAnnotations(model, attribute, value);
            string result = string.Join(", ", columns.Select(c => $"'{c.Table.Name}'[{c.Name}]"));
            return result;
        }

        private static string? FindSingleTable(TabularModel model, string attribute, string? value)
        {
            var tables = GetTablesFromAnnotations(model, attribute, value);
            if (tables.Count() != 1)
            {
                throw new MultipleMatchesException(tables.Select(t => $"'{t.Name}'").ToArray());
            }
            return $"'{tables.First().Name}'";
        }

        private static string? FindSingleColumn(TabularModel model, string attribute, string? value)
        {
            var columns = GetColumnsFromAnnotations(model, attribute, value);
            if (columns.Count() != 1)
            {
                throw new MultipleMatchesException(columns.Select(c => $"'{c.Table.Name}'[{c.Name}]").ToArray());
            }
            return $"'{columns.First().Table.Name}'[{columns.First().Name}]";
        }
    }
}
