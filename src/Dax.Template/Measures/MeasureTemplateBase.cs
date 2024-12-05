using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Interfaces;
using Dax.Template.Exceptions;
using System.Text.RegularExpressions;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using TabularMeasure = Microsoft.AnalysisServices.Tabular.Measure;
using System.Threading;
using Dax.Template.Extensions;

namespace Dax.Template.Measures
{
    public class MeasureTemplateBase: Model.Measure
    {
        class MultipleMatchesException : TemplateException
        {
            public string[] Matches { get; init; }
            public MultipleMatchesException( string[] matches ) : base()
            {
                Matches = matches;
            }
        }
        class AttributeNotFoundException : TemplateException
        {
            public string Attribute { get; init; }
            public string? Value { get; init; }
            public AttributeNotFoundException(string attribute, string? value) : base()
            {
                Attribute = attribute;
                Value = value;
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

        /// <summary>
        /// Default variables settings accessible to measures
        /// </summary>
        public Dictionary<string, string>? DefaultVariables { get; set; }

        private static readonly Regex regexFindPlaceholders = new(@"@_(?<entity>.*?)-(?<attribute>.*?)(-(?<value>.*?))?_@", RegexOptions.Compiled);
        private static readonly Regex regexGetMeasure = new(@"@@GETMEASURE[ \r\n\t]*\((?<templateName>[^\)]*?)\)", RegexOptions.Compiled);
        private static readonly Regex regexGetDefaultVariable = new(@"@@GETDEFAULTVARIABLE[ \r\n\t]*\((?<setting>[^\)]*)\)", RegexOptions.Compiled);
        private static readonly Regex regexGetYearEndFromFirstMonthVariable = new(@"@@GETYEARENDFROMFIRSTMONTHVARIABLE[ \r\n\t]*\((?<setting>[^\)]*)\)", RegexOptions.Compiled);

        private static string? GetGroupValue( Match match, string groupName)
        {
            return (match.Success && match.Groups.ContainsKey(groupName))
                        ? match.Groups[groupName].Value : null;
        }

        public const string ENTITY_SINGLE_COLUMN = "C";
        public const string ENTITY_COLUMNS_LIST = "CL";
        public const string ENTITY_SINGLE_TABLE = "T";
        public const string ENTITY_COLUMNS_TABLE = "CT";
        internal static TabularMeasure? FindMeasure(TabularModel model, string measureName)
        {
            foreach (var table in model.Tables)
            {
                var measure = table.Measures.Find(measureName);
                if (measure != null)
                    return measure;
            }

            return null;
        }

        string GetDefaultVariable(string expression)
        {
            Match matchGetDefaultVariable = regexGetDefaultVariable.Match(expression);
            if (matchGetDefaultVariable.Success)
            {
                var settingName = matchGetDefaultVariable.Groups.ContainsKey("setting") ? matchGetDefaultVariable.Groups["setting"].Value?.Trim() : null;
                if (settingName == null)
                {
                    throw new TemplateException($"Expression {regexGetDefaultVariable} not resolved");
                }
                string? replace = null;
                DefaultVariables?.TryGetValue(settingName, out replace);
                if (replace == null)
                {
                    throw new TemplateException($"Default variable not available for expression {regexGetDefaultVariable}");
                }
                expression = regexGetDefaultVariable.Replace(expression, replace);
            }

            return expression;
        }

        string GetYearEndFromFirstMonthVariable(string expression)
        {
            Match matchGetYearEnd = regexGetYearEndFromFirstMonthVariable.Match(expression);
            if (matchGetYearEnd.Success)
            {
                var settingName = matchGetYearEnd.Groups.ContainsKey("setting") ? matchGetYearEnd.Groups["setting"].Value?.Trim() : null;
                if (settingName == null)
                {
                    throw new TemplateException($"Expression {regexGetYearEndFromFirstMonthVariable} not resolved");
                }
                string? firstMonth = null;
                DefaultVariables?.TryGetValue(settingName, out firstMonth);
                if (!int.TryParse(firstMonth, out int firstMonthNumber))
                {
                    throw new TemplateException($"Invalid number argument in {regexGetYearEndFromFirstMonthVariable} expression");
                }
                string replace = firstMonthNumber switch
                {
                    1 => "\"12-31\"",
                    2 => "\"1-31\"",
                    3 => "\"2-28\"",
                    4 => "\"3-31\"",
                    5 => "\"4-30\"",
                    6 => "\"5-31\"",
                    7 => "\"6-30\"",
                    8 => "\"7-31\"",
                    9 => "\"8-31\"",
                    10 => "\"9-30\"",
                    11 => "\"10-31\"",
                    12 => "\"11-30\"",
                    _ => throw new TemplateException($"Invalid month number in {regexGetYearEndFromFirstMonthVariable} expression")
                };
                expression = regexGetYearEndFromFirstMonthVariable.Replace(expression, replace);
            }

            return expression;
        }

        public virtual TabularMeasure ApplyTemplate(TabularModel model, Table targetTable, bool overrideExistingMeasure = true, CancellationToken cancellationToken = default)
        {
            var measure = FindMeasure(model, Name);
            if (measure == null)
            {
                measure = new TabularMeasure { Name = Name };
                targetTable.Measures.Add(measure);
            }
            else if ((measure.Parent as Table)?.Name != targetTable.Name)
            {
                var clonedMeasure = measure.Clone();
                (measure.Parent as Table)?.Measures.Remove(measure);
                measure = clonedMeasure;
                targetTable.Measures.Add(measure);
            }
            measure.Name = Name; // Force rename in case of different char casing (e.g. 'Amount' renamed to 'amount')
            measure.FormatString = FormatString ?? ReferenceMeasure?.FormatString;
            measure.IsHidden = IsHidden;
            measure.DisplayFolder = DisplayFolder;
            measure.Description = Description;
            measure.Expression = GetDaxExpression(model, ReferenceMeasure?.Name);
            ApplyAnnotations(measure, cancellationToken);

            return measure;

            void ApplyAnnotations(TabularMeasure measure, CancellationToken cancellationToken = default)
            {
                if (Annotations == null) return;
                foreach (var annotation in Annotations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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
                catch (AttributeNotFoundException ex)
                {
                    throw new InvalidMacroReferenceException($"{ex.Attribute} : {ex.Value}", TemplateExpression);
                }

                if (string.IsNullOrWhiteSpace(replace))
                {
                    throw new InvalidMacroReferenceException(match.Value, TemplateExpression);
                }
                result = result.Replace(match.Value, replace);
            }

            result = GetDefaultVariable(result);
            result = GetYearEndFromFirstMonthVariable(result);

            if (originalMeasureName != null)
            {
                result = regexGetMeasure.Replace(result, match =>
                {
                    string? templateName = GetGroupValue(match, "templateName")?.Trim();
                    string replaceMeasureName =
                        string.IsNullOrWhiteSpace(templateName)
                        ? originalMeasureName
                        : Template.GetTargetMeasureName(templateName, originalMeasureName);
                    return $"[{replaceMeasureName.Replace("]", "]]")}]";
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

            return result.ToASEol()!;
        }

        internal static IEnumerable<Table> GetTablesFromAnnotations(TabularModel model, string attribute, string? value)
        {
            return (from t in model.Tables
                    where !t.IsHidden
                    from a in t.Annotations
                    where a.Name == attribute
                          && (value == null || a.Value.Split(",").Any(s => s.Trim() == value))
                    select t).Distinct();
        }

        internal static IEnumerable<Column> GetColumnsFromAnnotations(TabularModel model, string attribute, string? value)
        {
            return (from t in model.Tables
                    where !t.IsHidden
                    from c in t.Columns
                    from a in c.Annotations
                    where a.Name == attribute
                          && (value == null || a.Value.Split(",").Any(s => s.Trim() == value) )
                    select c).Distinct();
        }

        private static string? FindTablesList(TabularModel model, string attribute, string? value)
        {
            var tables = GetTablesFromAnnotations(model, attribute, value);
            string result = string.Join(", ", tables.Select(t => $"'{t.Name.GetDaxTableName()}'"));
            return result;
        }

        private static string? FindColumnsList(TabularModel model, string attribute, string? value)
        {
            var columns = GetColumnsFromAnnotations(model, attribute, value);
            string result = string.Join(", ", columns.Select(c => $"'{c.Table.Name.GetDaxTableName()}'[{c.Name.GetDaxColumnName()}]"));
            return result;
        }

        private static string? FindSingleTable(TabularModel model, string attribute, string? value)
        {
            var tables = GetTablesFromAnnotations(model, attribute, value);
            if (tables.Count() > 1)
            {
                throw new MultipleMatchesException(tables.Select(t => $"'{t.Name}'").ToArray());
            }
            else if (!tables.Any())
            {
                throw new AttributeNotFoundException(attribute, value);
            }
            return $"'{tables.First().Name.GetDaxTableName()}'";
        }

        private static string? FindSingleColumn(TabularModel model, string attribute, string? value)
        {
            var columns = GetColumnsFromAnnotations(model, attribute, value);
            if (columns.Count() > 1)
            {
                throw new MultipleMatchesException(columns.Select(c => $"'{c.Table.Name}'[{c.Name}]").ToArray());
            }
            else if (!columns.Any())
            {
                throw new AttributeNotFoundException(attribute, value);
            }
            return $"'{columns.First().Table.Name.GetDaxTableName()}'[{columns.First().Name}]";
        }
    }
}
