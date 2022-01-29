using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Dax.Template.Syntax;
using Dax.Template.Exceptions;
using Dax.Template.Extensions;
using System.Text.RegularExpressions;
using Column = Dax.Template.Model.Column;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;

namespace Dax.Template.Tables
{
    public abstract class CalculatedTableTemplateBase : TableTemplateBase
    {
        protected override bool RemoveExistingPartitions(Table dateTable)
        {
            // If the table has partitions, remove it assuming it is a calculated one
            if (dateTable.Partitions.Count > 1)
            {
                throw new ExistingTableException($"Existing table {dateTable.Name} has more than one partition ({dateTable.Partitions.Count})");
            }
            bool removeExistingPartition = (dateTable.Partitions.Count == 1);
            if (removeExistingPartition)
            {
                var existingPartition = dateTable.Partitions[0];

                if (existingPartition.SourceType != PartitionSourceType.Calculated)
                {
                    throw new ExistingTableException($"Existing table {dateTable.Name} is not a calculated table (SourceType={existingPartition.SourceType})");
                }
                // Remove existing partition
                dateTable.Partitions.Remove(dateTable.Partitions[0]);
            }
            return removeExistingPartition;
        }

        protected override void AddPartitions(Table dateTable)
        {
            // Add the new partition
            dateTable.Partitions.Add(new Partition
            {
                Name = dateTable.Name,
                Mode = ModeType.Import,
                Source = new CalculatedPartitionSource
                {
                    Expression = GetDaxTableExpression(dateTable.Model)
                }
            });
        }

        public string? IsoFormat { get; set; } 

        private static readonly Regex regexGetIso = new(@"@@GETISO[ \r\n\t]*\([ \r\n\t]*\)", RegexOptions.Compiled);

        protected virtual string? ProcessDaxExpression(string? expression, string lastStep, Microsoft.AnalysisServices.Tabular.Model? model = null)
        {
            if (string.IsNullOrEmpty(expression)) return expression;

            if (regexGetIso.Match(expression).Success)
            {
                string replace = !string.IsNullOrEmpty(IsoFormat) ? $", \"{IsoFormat}\"" : "";
                expression = regexGetIso.Replace(expression, replace);
            }
            return expression;
        }

        private static IEnumerable<TBase> GetLevelElements<TBase>(IEnumerable<(IDependencies<DaxBase> item, int level)>? elements) where TBase : class, IDependencies<DaxBase>
        {
            return
                from element in elements
                where element.item is TBase
                select element.item as TBase;
        }

        static private readonly string CommentPrefix = $"{new string('-', 2)} ";
        static private readonly string CommentPrefixNewLine = $"\r\n{CommentPrefix} ";
        static private readonly string CommentLineSeparator = $"\r\n{new string('-', 40)}";

        private const string STEP_RESULT_NAME = "__Result";
        //protected static string GetComments(IDaxComment daxComment)
        //{ 
        //    return GetComments(daxComment,string.Empty);
        //}

        protected static string GetComments(IDaxComment daxComment, string padding)
        {
            string result = string.Empty;
            if (daxComment.Comments != null)
            {
                result = string.Join(string.Empty, daxComment.Comments.Select(commentLine => $"{padding}{CommentPrefix}{commentLine}\r\n"));
            }
            return result;
        }

        // Padding for DAX expressions generated
        static protected readonly string PadGlobalVarDefinition = string.Empty;
        static protected readonly string PadStepDefinition = string.Empty;
        static protected readonly string PadGlobalVarExpression = new(' ', 4);
        static protected readonly string PadRowVarExpression = new(' ', 12);
        static protected readonly string PadRowVarDefinition = new(' ', 8);
        static protected readonly string PadColumnGenerateExpression = new(' ', 16);
        static protected readonly string PadColumnGenerateDefinition = new(' ', 12);
        static protected readonly string PadColumnAddColumnsExpression = new(' ', 12);
        static protected readonly string PadColumnAddColumnsDefinition = new(' ', 8);

        public virtual string? GetDaxTableExpression(Microsoft.AnalysisServices.Tabular.Model? model)
        {
            var listDependencies = Columns.GetDependencies();

            var elements = listDependencies.TSort(v => v.Dependencies);

            var groupElements =
                from element in elements
                group element by element.level into newLevel
                orderby newLevel.Key
                select newLevel;

            var result = string.Empty;
            var previousStepName = string.Empty;
            var lastStepName = string.Empty;
            foreach (var level in groupElements)
            {
                var daxSteps = GetLevelElements<DaxStep>(level);
                lastStepName = daxSteps.LastOrDefault()?.Name ?? lastStepName;
                string previousStepToReference = (!string.IsNullOrEmpty(previousStepName) ? previousStepName : lastStepName);
                var globalVars = GetLevelElements<VarGlobal>(level);
                var daxElements = GetLevelElements<DaxElement>(level).Except(daxSteps);
                var rowVars = GetLevelElements<VarRow>(level);
                // Skip columns without definition, such as Date assigned to a step
                var columns = GetLevelElements<Column>(level).Where(c => !string.IsNullOrEmpty(ProcessDaxExpression(c.Expression, previousStepToReference, model)));

                var configurableGlobalVars = globalVars.Where(v => v.IsConfigurable);
                var internalGlobalVars = globalVars.Where(v => !v.IsConfigurable);
                if (configurableGlobalVars.Any())
                {
                    result += CommentPrefixNewLine;
                    result += $"{CommentPrefixNewLine}   Configuration";
                    result += CommentPrefixNewLine;
                    result += string.Join(string.Empty, configurableGlobalVars.Select(e => $"\r\n{GetComments(e, PadGlobalVarDefinition)}VAR {e.Name} = {ProcessDaxExpression(e.Expression, previousStepToReference, model)}"));
                    result += CommentLineSeparator;
                }
                result += string.Join(string.Empty, internalGlobalVars.Select(e => $"\r\n{GetComments(e, PadGlobalVarDefinition)}VAR {e.Name} = {ProcessDaxExpression(e.Expression, previousStepToReference, model)}"));
                result += string.Join(string.Empty, daxSteps.Select(e => $"\r\n{GetComments(e, PadStepDefinition)}VAR {e.Name} = {ProcessDaxExpression(e.Expression, previousStepToReference, model)}"));
                if (rowVars.Any() || columns.Any())
                {
                    if (!columns.Any())
                    {
                        // TODO: the current configuration 05 creates a wrong dependency order of the variables
                        //       Level 4 has an unused variable
                        //       Level 5 has a reference in the wrong order (now fixed)
                        // TODO ALBERTO : we should review how to fix this issue by investigating cases that
                        //                raise the following exception (uncomment exception and try)
                        //                the current workaround is to keep going in this condition, the code 
                        //                works the same but it is more verbose (additional steps and skipped steps numbers)
                        // throw new TemplateException($"Row variables without columns in level {level.Key}.");
                        continue;
                    }
                    if (daxElements.Count() > 1)
                    {
                        throw new TemplateException($"Multiple DaxElements found in level {level.Key}.");
                    }
                    var stepName = $"__Step{level.Key}";
                    if (rowVars.Any())
                    {
                        DaxElement defaultElement = new() { Expression = @$"
VAR {stepName} = 
    GENERATE (
        {(!string.IsNullOrEmpty(previousStepName) ? previousStepName : lastStepName)},
@@VARS@@@@COLUMNS@@
    )" };

                        var daxElement = daxElements.FirstOrDefault() ?? defaultElement;
                        var daxRowVars = (rowVars?.Any() == true) ?
                            string.Join("\r\n", rowVars.Select(e => $"{GetComments(e,PadRowVarDefinition)}{PadRowVarDefinition}VAR {e.Name} = {ProcessDaxExpression(e.Expression, previousStepToReference, model)}")) + "\r\n        RETURN " :
                            "        ";
                        var columnsList = string.Join(",\r\n", columns.Select(c => $"{GetComments(c, PadColumnGenerateDefinition)}{PadColumnGenerateDefinition}\"{c.Name}\", {ProcessDaxExpression(c.Expression, previousStepToReference, model)}"));
                        var daxColumns = $@"ROW ( 
{columnsList} 
        )";
                        var x = ProcessDaxExpression(daxElement.Expression, previousStepToReference, model)?.Replace("@@VARS@@", daxRowVars).Replace("@@COLUMNS@@", daxColumns);
                        result += x;
                    }
                    else
                    {
                        DaxElement defaultElement = new() { Expression = @$"
VAR {stepName} = 
    ADDCOLUMNS (
        {(!string.IsNullOrEmpty(previousStepName) ? previousStepName : lastStepName)},
@@COLUMNS@@
    )" };

                        var daxElement = daxElements.FirstOrDefault() ?? defaultElement;
                        var columnsList = string.Join(",\r\n", columns.Select(e => $"{PadColumnAddColumnsDefinition}\"{e.Name}\", {ProcessDaxExpression(e.Expression, previousStepToReference, model)}"));
                        string? replacedExpression = daxElement.Expression?.Replace("@@COLUMNS@@", columnsList);
                        if (replacedExpression == daxElement.Expression)
                        {
                            throw new TemplateException($"Missing columns list in DaxElement for {stepName}: columnsList: {columnsList}");
                        }
                        result += ProcessDaxExpression(replacedExpression, previousStepToReference, model);
                    }
                    previousStepName = stepName;
                }
            }

            if (Columns.Where(c => c.IsTemporary).Any())
            {
                string stepName = STEP_RESULT_NAME;
                DaxElement daxElement = new() { Expression = @$"
VAR {stepName} = 
    SELECTCOLUMNS (
        {(!string.IsNullOrEmpty(previousStepName) ? previousStepName : lastStepName)},
@@COLUMNS@@
    )" };
                var columnsList = string.Join(
                    ",\r\n",
                    from c in Columns
                    where !c.IsTemporary
                    select $"        \"{c.Name}\", [{c.Name}]"
                );
                string replacedExpression = daxElement.Expression.Replace("@@COLUMNS@@", columnsList);
                if (replacedExpression == daxElement.Expression)
                {
                    throw new TemplateException($"Missing columns list in final result - are all columns hidden?");
                }
                result += ProcessDaxExpression(replacedExpression, (!string.IsNullOrEmpty(previousStepName) ? previousStepName : lastStepName), model);
                previousStepName = stepName;
            }
            result += $"\r\nRETURN\r\n    {previousStepName}";
            return result;
        }
    }
}
