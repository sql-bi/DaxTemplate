using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using Dax.Template.Syntax;
using Column = Dax.Template.Model.Column;
using Hierarchy = Dax.Template.Model.Hierarchy;
using Level = Dax.Template.Model.Level;
using Dax.Template.Extensions;
using Dax.Template.Exceptions;
using AttributeType = Microsoft.AnalysisServices.AttributeType;
using Dax.Template.Interfaces;

namespace Dax.Template.Tables
{
    /// <summary>
    /// This class creates a date template based on the external definition in a JSON file.
    /// In order to edit the JSON file, it could be useful a system that generates the DAX
    /// code and tries to execute it - it's the quickest way to validate the correctness
    /// of the content, most of the errors are usually in the DAX definition
    /// </summary>
    public class CustomTableTemplate<T> : ReferenceCalculatedTable where T : ICustomTableConfig
    {        
        protected class FormatPrefix
        {
            private readonly string _name;
            private readonly string _prefixSearch;
            private readonly string _prefixFormat;
            public FormatPrefix(string name)
            {
                _name = name;
                _prefixSearch = $"@_{name}_@"; 
                _prefixFormat = string.Concat(from c in Name select @"\" + c);
            }
            public string Name => _name;
            public string PrefixSearch => _prefixSearch;        
            public string PrefixFormat => _prefixFormat;
        }
        protected static string ReplacePrefixes( string expression, List<FormatPrefix> prefixes )
        {
            prefixes.ForEach(prefix =>
            {
                expression = expression.Replace(prefix.PrefixSearch, prefix.PrefixFormat);
            });
            return expression;
        }

        public T Config { get; init; }

        /// <summary>
        /// Default constructor needs config but doesn't apply any template
        /// </summary>
        public CustomTableTemplate(T config)
        {
            Config = config;
        }

        /// <summary>
        /// Apply template definition and specified configuration
        /// </summary>
        /// <param name="config"></param>
        /// <param name="template"></param>
        /// <param name="model"></param>
        public CustomTableTemplate(T config, CustomTemplateDefinition template, TabularModel? model)
            : this( config, template, null, model)
        {
        }

        /// <summary>
        /// Internal use: can skip columns based on specialized templates/configurations
        /// </summary>
        /// <param name="config"></param>
        /// <param name="template"></param>
        /// <param name="skipColumn"></param>
        /// <param name="model"></param>
        protected CustomTableTemplate(T config, CustomTemplateDefinition template, Predicate<CustomTemplateDefinition.Column>? skipColumn, TabularModel? model)
            : this(config)
        {
            InitTemplate(config, template, skipColumn ?? ((c) => false), model);
        }

        protected virtual void InitTemplate(T config, CustomTemplateDefinition template, Predicate<CustomTemplateDefinition.Column> skipColumn, TabularModel? model)
        {
            // Add prefixes
            List<FormatPrefix> Prefixes = new();
            template.FormatPrefixes.ToList().ForEach(prefixDefinition => Prefixes.Add(new FormatPrefix(prefixDefinition)));

            List<DaxStep> steps = GetSteps(template);
            List<VarGlobal> globalVariables = GetGlobalVariables(template, Prefixes);
            UpdateDefaultVariables(globalVariables.Where(v => v.IsConfigurable), config.DefaultVariables);

            List<VarRow> rowVariables = GetRowVariables(template, Prefixes);
            
            GetColumns(template, Prefixes, steps, skipColumn);
            GetHierarchies(template);

            List<IDaxName> templateItems = new();
            templateItems.AddRange(steps);
            templateItems.AddRange(globalVariables);
            templateItems.AddRange(rowVariables);
            templateItems.AddRange(Columns);

            // Set dependencies for all the items
            templateItems.AddDependenciesFromExpression();
        }

        private static void UpdateDefaultVariables(IEnumerable<VarGlobal> globalVariables, Dictionary<string, string> defaultVariables)
        {
            foreach( var setting in defaultVariables )
            {
                var globalVariable = globalVariables.FirstOrDefault(v => v.Name == setting.Key);
                if (globalVariable == null)
                {
                    throw new InvalidConfigurationException(setting.Key, setting.Value);
                }
                globalVariable.Expression = setting.Value;
            }
        }

        private void GetHierarchies(CustomTemplateDefinition template)
        {
            template.Hierarchies.ToList().ForEach(hierarchyDefinition =>
            {
                if (string.IsNullOrEmpty(hierarchyDefinition.Name)) throw new TemplateException("Missing Hierarchy Name definition");

                List<Level> levels = new();
                hierarchyDefinition.Levels.ToList().ForEach(level =>
                {
                    if (string.IsNullOrEmpty(level.Name)) throw new TemplateException("Missing Hierarchy Level Name definition");
                    if (string.IsNullOrEmpty(level.Column)) throw new TemplateException("Missing Hierarchy Level Column definition");
                    var modelColumn = Columns.First(column => column.Name == level.Column);
                    Level modelLevel = new() { Name = level.Name, Column = modelColumn, Description = level.Description };
                    modelLevel.Description = level.Description;
                    levels.Add(modelLevel);
                });
                Hierarchy hierarchy = new()
                {
                    Name = hierarchyDefinition.Name,
                    Levels = levels.ToArray()
                };
                hierarchy.Description = hierarchyDefinition.Description;
                Hierarchies.Add(hierarchy);
            });
        }

        /// <summary>
        /// Create a new column initializing Name and DataType properties
        /// </summary>
        /// <param name="name">Column name</param>
        /// <param name="dataType">Column data type</param>
        /// <returns></returns>
        protected virtual Column CreateColumn( string name, DataType dataType)
        {
            return new Column()
            {
                Name = name,
                DataType = dataType
            };
        }
        protected virtual void GetColumns(CustomTemplateDefinition template, List<FormatPrefix> Prefixes, List<DaxStep> steps, Predicate<CustomTemplateDefinition.Column> skipColumn ) // bool hasHolidays)
        {
            template.Columns.ToList().ForEach(columnDefinition =>
            {
                if (string.IsNullOrEmpty(columnDefinition.Name)) throw new TemplateException("Missing Column Name definition");
                string? expression = null;
                IDependencies<DaxBase>[]? columnDependencies = null;
                if (!string.IsNullOrEmpty(columnDefinition.Step))
                {
                    var stepParent = steps.FirstOrDefault(i => i.DaxName == columnDefinition.Step);
                    if (stepParent == null) throw new TemplateException($"Step {columnDefinition.Step} not found for column {columnDefinition.Name}");
                    columnDependencies = new DaxStep[] { stepParent };
                    expression = string.Empty;
                }
                else
                {
                    var columnExpression = columnDefinition.GetExpression(PadColumnGenerateExpression);
                    if (string.IsNullOrEmpty(columnExpression)) throw new TemplateException("Missing Column Expression definition");
                    expression = ReplacePrefixes(columnExpression, Prefixes);
                }

                // Skip columns if required by the caller
                // For example, derived class can skip columns related to holidays if no holidays configuration available
                if (skipColumn(columnDefinition)) return;

                if (!Enum.TryParse(columnDefinition.DataType, out DataType dataType)) throw new TemplateException("Missing or invalid Column DataType definition");
                Column column = CreateColumn(columnDefinition.Name, dataType);

                column.Expression = expression;
                column.Comments = columnDefinition.GetComments();
                column.FormatString = columnDefinition.FormatString;
                column.Dependencies = columnDependencies;
                column.IsTemporary = columnDefinition.IsTemporary;
                column.IsHidden = columnDefinition.IsHidden;
                column.DisplayFolder = columnDefinition.DisplayFolder;
                column.Description = columnDefinition.Description;
                column.Comments = columnDefinition.GetComments();
                column.Annotations = columnDefinition.Annotations;
                if (columnDefinition.AttributeType != null)
                {
                    if (Enum.TryParse<AttributeType>(columnDefinition.AttributeType, true, out AttributeType attributeType))
                    {
                        column.AttributeType = new AttributeType[] { attributeType };
                    }
                    else
                    {
                        throw new InvalidAttributeException(columnDefinition.AttributeType, $"Column: {columnDefinition.Name}");
                    }
                }
                else if (columnDefinition.AttributeTypes?.Length > 0)
                {
                    column.AttributeType = columnDefinition.AttributeTypes.Select(atName =>
                       {
                           if (Enum.TryParse<AttributeType>(atName, true, out AttributeType attributeType))
                           {
                               return attributeType;
                           }
                           else
                           {
                               throw new InvalidAttributeException(atName, $"Column: {columnDefinition.Name}");
                           }
                       }).ToArray();
                }
                Columns.Add(column);
            });

            // Fix SortByColumn
            template.Columns
                .Where(columnDefinition => !string.IsNullOrEmpty(columnDefinition.SortByColumn))
                .ToList().ForEach(columnDefinition =>
                {
                    var modelColumn = Columns.First(column => column.Name == columnDefinition.Name);
                    var sortByColumn = Columns.First(column => column.Name == columnDefinition.SortByColumn);
                    modelColumn.SortByColumn = sortByColumn;
                });
        }

        private static List<VarRow> GetRowVariables(CustomTemplateDefinition template, List<FormatPrefix> Prefixes)
        {
            List<VarRow> rowVariables = new();
            template.RowVariables.ToList().ForEach(variableDefinition =>
            {
                if (string.IsNullOrEmpty(variableDefinition.Name)) throw new TemplateException("Missing RowVariable Name definition");
                string? varExpression = variableDefinition.GetExpression(PadRowVarExpression);
                if (string.IsNullOrEmpty(varExpression)) throw new TemplateException("Missing RowVariable Expression definition");
                rowVariables.Add(
                    new VarRow
                    {
                        Name = variableDefinition.Name,
                        Expression = ReplacePrefixes(varExpression, Prefixes),
                        Comments = variableDefinition.GetComments()
                    });
            });
            return rowVariables;
        }

        private static List<VarGlobal> GetGlobalVariables(CustomTemplateDefinition template, List<FormatPrefix> Prefixes)
        {
            List<VarGlobal> globalVariables = new();
            template.GlobalVariables.ToList().ForEach(variableDefinition =>
            {
                if (string.IsNullOrEmpty(variableDefinition.Name)) throw new TemplateException("Missing GlobalVariable Name definition");
                string? varExpression = variableDefinition.GetExpression(PadGlobalVarExpression);
                if (string.IsNullOrEmpty(varExpression)) throw new TemplateException("Missing GlobalVariable Expression definition");
                globalVariables.Add(
                    new VarGlobal
                    {
                        Name = variableDefinition.Name,
                        Expression = ReplacePrefixes(varExpression, Prefixes),
                        IsConfigurable = variableDefinition.IsConfigurable,
                        Comments = variableDefinition.GetComments()
                    });
            });
            return globalVariables;
        }

        private static List<DaxStep> GetSteps(CustomTemplateDefinition template)
        {
            List<DaxStep> steps = new();
            template.Steps.ToList().ForEach(stepDefinition =>
            {
                if (string.IsNullOrEmpty(stepDefinition.Name)) throw new TemplateException("Missing Step Name definition");
                string? stepExpression = stepDefinition.GetExpression(PadGlobalVarExpression);
                if (string.IsNullOrEmpty(stepExpression)) throw new TemplateException("Missing Step Expression definition");
                steps.Add(
                    new DaxStep
                    {
                        Name = stepDefinition.Name,
                        Expression = stepExpression,
                        Comments = stepDefinition.GetComments()
                    });
            });
            return steps;
        }
    }
}
