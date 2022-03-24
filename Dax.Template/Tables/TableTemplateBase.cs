using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Column = Dax.Template.Model.Column;
using Hierarchy = Dax.Template.Model.Hierarchy;
using TabularHierarchy = Microsoft.AnalysisServices.Tabular.Hierarchy;
using TabularLevel = Microsoft.AnalysisServices.Tabular.Level;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using System.Threading;

namespace Dax.Template.Tables
{
    public abstract class TableTemplateBase
    {
        public const string ANNOTATION_ATTRIBUTE_TYPE = "SQLBI_AttributeTypes";

        public List<Column> Columns { get; set; } = new List<Column>();
        public List<Hierarchy> Hierarchies { get; set; } = new List<Hierarchy>();
        public Dictionary<string, string> Annotations { get; set; } = new();

        public Translations? Translation { get; set; }

        private bool templateApplied = false;
        private void ResetTabularReferences(CancellationToken? cancellationToken)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            foreach (var column in Columns) column.Reset();

            foreach (var hierarchy in Hierarchies)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                hierarchy.Reset();
                foreach (var level in hierarchy.Levels)
                {
                    level.Reset();
                }
            }

            FixRelationshipsTo = null;
            FixRelationshipsFrom = null;

            templateApplied = false;
        }

        protected virtual void RenameWithTranslation(Table tabularTable, Translations.Language language, CancellationToken? cancellationToken)
        {
            // if (!string.IsNullOrEmpty(language.Table.Name)) tabularTable.Name = language.Table.Name;
            if (!string.IsNullOrEmpty(language.Table?.Description)) tabularTable.Description = language.Table.Description;
            
            foreach(var column in tabularTable.Columns)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                var columnTranslation = language.Columns.FirstOrDefault(c => c.OriginalName == column.Name);
                if (columnTranslation != null)
                {
                    column.Name = columnTranslation.Name;
                    if (columnTranslation.Description != null) column.Description = columnTranslation.Description;
                    if (columnTranslation.DisplayFolders != null) column.DisplayFolder = columnTranslation.DisplayFolders;
                    if (columnTranslation.FormatString != null) column.FormatString = columnTranslation.FormatString;
                }
            }
            foreach (var measure in tabularTable.Measures)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                var measureTranslation = language.Measures.FirstOrDefault(c => c.OriginalName == measure.Name);
                if (measureTranslation != null)
                {
                    measure.Name = measureTranslation.Name;
                    if (measureTranslation.Description != null) measure.Description = measureTranslation.Description;
                    if (measureTranslation.DisplayFolders != null) measure.DisplayFolder = measureTranslation.DisplayFolders;
                    if (measureTranslation.FormatString != null) measure.FormatString = measureTranslation.FormatString;
                }
            }
            foreach (var hierarchy in tabularTable.Hierarchies)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                var hierarchyTranslation = language.Hierarchies.FirstOrDefault(h => h.OriginalName == hierarchy.Name);
                if (hierarchyTranslation != null)
                {
                    hierarchy.Name = hierarchyTranslation.Name;
                    if (hierarchyTranslation.Description != null) hierarchy.Description = hierarchyTranslation.Description;
                    if (hierarchyTranslation.DisplayFolders != null) hierarchy.DisplayFolder = hierarchyTranslation.DisplayFolders;
                    foreach(var level in hierarchy.Levels)
                    {
                        cancellationToken?.ThrowIfCancellationRequested();
                        var levelTranslation = hierarchyTranslation.Levels.FirstOrDefault(l => l.OriginalName == level.Name);
                        if (levelTranslation != null)
                        {
                            level.Name = levelTranslation.Name;
                            if (levelTranslation.Description != null) level.Description = levelTranslation.Description;
                        }
                    }
                }
            }
        }

        protected virtual void AddTranslation(Table tabularTable, Translations.Language language)
        {
            throw new NotImplementedException();
        }

        protected virtual void ApplyTranslations(Table tabularTable, CancellationToken? cancellationToken)
        {
            if (Translation == null) return;

            // Apply a different language to entity names
            if (!string.IsNullOrEmpty(Translation.DefaultIso))
            {
                var t = Translation.GetTranslationIso(Translation.DefaultIso);
                if (t != null)
                {
                    RenameWithTranslation(tabularTable, t, cancellationToken);
                }
            }

            // Apply required translations
            Translation.GetTranslations().Where(t => Translation.ApplyAllIso || Translation.ApplyIso.Contains(t.Iso)).ToList().ForEach(t => 
            {
                cancellationToken?.ThrowIfCancellationRequested();
                AddTranslation(tabularTable, t);
            });
        }
        public void ApplyTemplate(Table tabularTable, CancellationToken? cancellationToken, bool hideTable = false)
        {
            ApplyTemplate(tabularTable, cancellationToken);

            ApplyTranslations(tabularTable, cancellationToken);

            if (hideTable)
            {
                tabularTable.IsHidden = true;
                foreach (var c in tabularTable.Columns)
                {
                    c.IsHidden = true;
                }
                foreach (var h in tabularTable.Hierarchies)
                {
                    h.IsHidden = true;
                }
            }
        }

        protected IEnumerable<(SingleColumnRelationship relationshipTo, string columnName, bool isKey)>? FixRelationshipsTo = null;
        protected IEnumerable<(SingleColumnRelationship relationshipFrom, string columnName, bool isKey)>? FixRelationshipsFrom = null;

        public virtual void ApplyTemplate(Table tabularTable, CancellationToken? cancellationToken)
        {
            if (templateApplied)
            {
                ResetTabularReferences(cancellationToken);
            }
            templateApplied = true;

            SaveAffectedRelationships(tabularTable, cancellationToken);

            RemoveExistingElements(tabularTable, cancellationToken);

            AddPartitions(tabularTable, cancellationToken);

            AddColumns(tabularTable, cancellationToken);

            AddHierarchies(tabularTable, cancellationToken);

            AddAnnotations(tabularTable, cancellationToken);

            RestoreAffectedRelationships(tabularTable, cancellationToken);
        }

        private void SaveAffectedRelationships(Table tabularTable, CancellationToken? cancellationToken)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            if (tabularTable.Model != null)
            {
                // Save relationships
                FixRelationshipsTo =
                    from SingleColumnRelationship relationship in
                         (from r in tabularTable.Model.Relationships
                          where r is SingleColumnRelationship
                          select r)
                    where relationship.ToTable.Name == tabularTable.Name
                    select (relationship, relationship.ToColumn.Name, relationship.ToColumn.IsKey);
                FixRelationshipsFrom =
                    from SingleColumnRelationship relationship in
                         (from r in tabularTable.Model.Relationships
                          where r is SingleColumnRelationship
                          select r)
                    where relationship.FromTable.Name == tabularTable.Name
                    select (relationship, relationship.FromColumn.Name, relationship.ToColumn.IsKey);
            }
        }

        private void RestoreAffectedRelationships(Table tabularTable, CancellationToken? cancellationToken)
        {
            if (FixRelationshipsTo != null)
            {
                foreach (var (relationshipTo, columnName, isKey) in FixRelationshipsTo)
                {
                    TabularColumn column = GetColumn(isKey, columnName);
                    relationshipTo.ToColumn = column;
                }
            }

            cancellationToken?.ThrowIfCancellationRequested();

            if (FixRelationshipsFrom != null)
            {
                foreach (var (relationshipFrom, columnName, isKey) in FixRelationshipsFrom)
                {
                    TabularColumn column = GetColumn(isKey, columnName);
                    relationshipFrom.FromColumn = column;
                }
            }

            TabularColumn GetColumn(bool isKey, string columnName)
            {
                TabularColumn column;
                if (isKey)
                {
                    column = tabularTable.Columns.First(c => c.IsKey);
                }
                else
                {
                    column = tabularTable.Columns[columnName];
                }
                return column;
            }
        }


        protected virtual string GetSourceColumnName(Column column)
        {
            return $"[{column.Name}]";
        }

        protected virtual void AddColumns(Table dateTable, CancellationToken? cancellationToken)
        {
            // Save existing columns (like calculated columns)
            var existingColumns = dateTable.Columns.Select(c => c.Clone()).ToList();
            dateTable.Columns.Clear();

            try
            {
                // Add the columns
                foreach (var column in this.Columns.Where(c=>!c.IsTemporary))
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    column.TabularColumn = new CalculatedTableColumn
                    {
                        Name = column.Name,
                        Description = column.Description,
                        SourceColumn = GetSourceColumnName(column),
                        DataType = column.DataType,
                        FormatString = column.FormatString,
                        IsHidden = column.IsHidden,
                        DisplayFolder = column.DisplayFolder,
                        DataCategory = column.DataCategory,
                        IsKey = column.IsKey,
                        IsNameInferred = true,
                        IsDataTypeInferred = true,
                        SummarizeBy = AggregateFunction.None
                    };

                    if (dateTable.Model.Database.CompatibilityLevel >= 1540)
                        column.TabularColumn.LineageTag = Guid.NewGuid().ToString();

                    if (column.AttributeType != null)
                    {
                        column.TabularColumn.Annotations.Add(
                            new Annotation
                            {
                                Name = ANNOTATION_ATTRIBUTE_TYPE,
                                Value = string.Join(", ", column.AttributeType.Select(attr => attr.ToString()))
                            }
                        );
                    }

                    foreach (var annotation in column.Annotations )
                    {
                        column.TabularColumn.Annotations.Add(
                            new Annotation
                            {
                                Name = annotation.Key,
                                Value = annotation.Value.ToString()
                            }
                        );
                    }
                    
                    dateTable.Columns.Add(column.TabularColumn);
                }

                // Fix the Sort By Columns property
                foreach (var column in this.Columns.Where(c => c.SortByColumn is not null))
                {
                    if (column.TabularColumn is not null)
                    {
                        column.TabularColumn.SortByColumn = column.SortByColumn?.TabularColumn;
                    }
                }
            }
            finally
            {
                // Restore existing columns
                existingColumns.ForEach(c => dateTable.Columns.Add(c));
            }
        }

        // TODO: this code is very similar to ApplyAnnotations in MeasuresTemplateBase.cs - evaluate whether we should consolidate the code in a single function
        protected virtual void AddAnnotations(Table dateTable, CancellationToken? cancellationToken)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            if (Annotations == null) return;
            foreach (var annotation in Annotations)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                var annotationName = annotation.Key;
                var annotationValue = annotation.Value.ToString();

                Annotation? tabularAnnotation = dateTable.Annotations.FirstOrDefault(a => a.Name == annotationName);
                if (tabularAnnotation == null)
                {
                    tabularAnnotation = new Annotation { Name = annotationName, Value = annotationValue };
                    dateTable.Annotations.Add(tabularAnnotation);
                }
                else
                {
                    tabularAnnotation.Value = annotationValue;
                }
            }
        }

        protected virtual void AddHierarchies(Table dateTable, CancellationToken? cancellationToken)
        {
            // Create Tabular level for hierarchies
            var levels = from h in Hierarchies from l in h.Levels select l;
            foreach (var level in levels)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                level.TabularLevel = new TabularLevel
                {
                    Name = level.Name,
                    Column = level.Column.TabularColumn
                };
                if (dateTable.Model.Database.CompatibilityLevel >= 1540)
                    level.TabularLevel.LineageTag = Guid.NewGuid().ToString();
            }

            // Set the hierarchies
            foreach (var hierarchy in this.Hierarchies)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                var tabularHierarchy = new TabularHierarchy
                {
                    Name = hierarchy.Name,
                    IsHidden = hierarchy.IsHidden,
                    DisplayFolder = hierarchy.DisplayFolder,
                };
                if (dateTable.Model.Database.CompatibilityLevel >= 1540)
                    tabularHierarchy.LineageTag = Guid.NewGuid().ToString();
                dateTable.Hierarchies.Add(tabularHierarchy);
                int ordinal = 0;
                foreach (var level in hierarchy.Levels)
                {
                    var tabularLevel = new TabularLevel
                    {
                        Name = level.Name,
                        Column = level.Column.TabularColumn,
                        Ordinal = ordinal++
                    };
                    if (dateTable.Model.Database.CompatibilityLevel >= 1540)
                        tabularLevel.LineageTag = Guid.NewGuid().ToString();
                    tabularHierarchy.Levels.Add(tabularLevel);
                }
            }
        }

        protected virtual void RemoveExistingElements(Table dateTable, CancellationToken? cancellationToken)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            if (RemoveExistingPartitions(dateTable))
            {
                RemoveExistingColumns(dateTable);

                RemoveExistingHierarchies(dateTable);
            }
        }

        protected virtual void RemoveExistingHierarchies(Table dateTable)
        {
            // Remove existing hierarchies that come from the template
            // TODO: keep the custom hierarchies that do not have SQLBI annotations
            //       we should save them and recreate with the same column names
            //       however, what to do with invalid hierarchies referencing columns that disappears?
            var listHierarchies = from l in dateTable.Hierarchies select l;
            foreach (var hierarchy in listHierarchies.ToArray())
            {
                dateTable.Hierarchies.Remove(hierarchy);
            }
        }

        protected virtual void RemoveExistingColumns(Table dateTable)
        {
            // Remove existing columns that are not calculated columns
            var listColumns = from c in dateTable.Columns where c is not CalculatedColumn select c;
            foreach (var column in listColumns.ToArray())
            {
                dateTable.Columns.Remove(column);
            }
        }

        protected abstract bool RemoveExistingPartitions(Table dateTable);
        protected abstract void AddPartitions(Table dateTable, CancellationToken? cancellationToken);

    }
}
