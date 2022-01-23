using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices.AdomdClient;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using TabularMeasure = Microsoft.AnalysisServices.Tabular.Measure;
using TabularHierarchy = Microsoft.AnalysisServices.Tabular.Hierarchy;

namespace Dax.Template.Model
{
    public class ModelChanges
    {
        public ICollection<TableChanges> RemovedObjects { get; init; } = new SortedSet<TableChanges>(new ByItemName());
        public ICollection<TableChanges> ModifiedObjects { get; init; } = new SortedSet<TableChanges>(new ByItemName());
        public abstract class ItemChanges
        {
            public ItemChanges(string name) { Name = name; }
            public string Name { get; init; }
        }
        public class ByItemName : IComparer<ItemChanges>
        {
            private static readonly CaseInsensitiveComparer caseiComp = new();
            public int Compare(ItemChanges? x, ItemChanges? y)
            {
                return caseiComp.Compare(x?.Name, y?.Name);
            }
        }
        public class TableChanges : ItemChanges
        {
            public TableChanges(string name) : base(name) { }
            public bool IsHidden { get; set; }
            public string? Expression { get; set; }
            public object? Preview { get; set; }

            public ICollection<ColumnChanges> Columns { get; init; } = new SortedSet<ColumnChanges>(new ByItemName());
            public ICollection<MeasureChanges> Measures { get; set; } = new SortedSet<MeasureChanges>(new ByItemName());
            public ICollection<HierarchyChanges> Hierarchies { get; set; } = new SortedSet<HierarchyChanges>(new ByItemName());
        }
        public class ColumnChanges : ItemChanges
        {
            public ColumnChanges(string name) : base(name) { }
            public bool IsHidden { get; set; }
            public string? DataType { get; set; }

        }
        public class MeasureChanges : ItemChanges
        {
            public MeasureChanges(string name) : base(name) { }
            public bool IsHidden { get; set; }
            public string? Expression { get; set; }
            public string? DisplayFolder { get; set; }
        }
        public class HierarchyChanges : ItemChanges
        {
            public HierarchyChanges(string name) : base(name) { }
            public bool IsHidden { get; set; }
            public string[]? Levels { get; set; }
        }

        public void AddTable(Table table, bool isRemoved)
        {
            AddTable(table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected virtual TableChanges AddTable(Table table, ICollection<TableChanges> collection)
        {
            TableChanges? tableChanges = collection.FirstOrDefault(t => t.Name == table.Name);
            if (tableChanges != null)
            {
                return tableChanges;
            }

            string? expression = null;
            if (table.Partitions.Count == 1)
            {
                CalculatedPartitionSource? existingPartition = table.Partitions[0].Source as CalculatedPartitionSource;
                expression = existingPartition?.Expression;
            }

            tableChanges = new TableChanges(table.Name)
            {
                IsHidden = table.IsHidden,
                Expression = expression
            };
            collection.Add(tableChanges);
            return tableChanges;
        }
        public void AddColumn(TabularColumn column, Table? table, bool isRemoved)
        {
            AddColumn(column, table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected void AddColumn(TabularColumn column, Table? table, ICollection<TableChanges> collection)
        {
            if (column.Type == ColumnType.RowNumber) return;
            if (table == null) throw new System.Exception("Parent table not found");

            TableChanges tableChanges = AddTable(table, collection);
            if (tableChanges.Columns.Any(c => c.Name == column.Name)) return;

            var columnChanges = new ColumnChanges(column.Name)
            {
                IsHidden = column.IsHidden,
                DataType = column.DataType.ToString()
            };
            tableChanges.Columns.Add(columnChanges);
        }
        public void AddMeasure(TabularMeasure measure, Table? table, bool isRemoved)
        {
            AddMeasure(measure, table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected void AddMeasure(TabularMeasure measure, Table? table, ICollection<TableChanges> collection)
        {
            if (table == null) throw new System.Exception("Parent table not found");

            TableChanges tableChanges = AddTable(table, collection);
            if (tableChanges.Measures.Any(m => m.Name == measure.Name)) return;

            var measureChanges = new MeasureChanges(measure.Name)
            {
                IsHidden = measure.IsHidden,
                DisplayFolder = measure.DisplayFolder,
                Expression = measure.Expression
            };
            tableChanges.Measures.Add(measureChanges);
        }
        public void AddHierarchy(TabularHierarchy hierarchy, Table? table, bool isRemoved)
        {
            AddHierarchy(hierarchy, table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected void AddHierarchy(TabularHierarchy hierarchy, Table? table, ICollection<TableChanges> collection)
        {
            if (table == null) throw new System.Exception("Parent table not found");

            TableChanges tableChanges = AddTable(table, collection);
            if (tableChanges.Hierarchies.Any(h => h.Name == hierarchy.Name)) return;

            var hierarchyChanges = new HierarchyChanges(hierarchy.Name)
            {
                IsHidden = hierarchy.IsHidden,
                Levels = (from level in hierarchy.Levels
                          select level.Name).ToArray()
            };
            tableChanges.Hierarchies.Add(hierarchyChanges);
        }

        public void SimplifyRemovedObjects()
        {
            var removedObjects = RemovedObjects.ToArray();
            foreach( var tableChanges in removedObjects )
            {
                var modifiedTable = ModifiedObjects.FirstOrDefault(t => t.Name == tableChanges.Name);
                if (modifiedTable != null)
                {
                    ClearModifiedItems(tableChanges.Columns, modifiedTable.Columns);
                    ClearModifiedItems(tableChanges.Measures, modifiedTable.Measures);
                    ClearModifiedItems(tableChanges.Hierarchies, modifiedTable.Hierarchies);
                }
                if (tableChanges.Columns.Count == 0 && tableChanges.Measures.Count == 0 && tableChanges.Hierarchies.Count == 0)
                {
                    RemovedObjects.Remove(tableChanges);
                }
            }

            void ClearModifiedItems<T>(ICollection<T> removedItems, ICollection<T> modifiedItems) where T: ItemChanges
            {
                var notRemovedItems = removedItems
                    .ToArray()
                    .Where(removedItem => modifiedItems.Any(c => c.Name == removedItem.Name));
                foreach (var removedItem in notRemovedItems)
                {
                    removedItems.Remove(removedItem);
                }

            }
        }

        private static object? GetPreviewData(AdomdConnection connection, string? tableExpression, int previewRows )
        {
            if (string.IsNullOrWhiteSpace(tableExpression)) return null;
            string daxQuery = $"EVALUATE TOPNSKIP ( {previewRows}, 0, {tableExpression} )";
            if (connection.State != System.Data.ConnectionState.Open) connection.Open();
            using AdomdCommand command = new(daxQuery, connection);
            using var reader = command.ExecuteReader();
            List<object> result = new();
            while (reader.Read())
            {
                Dictionary<string, object> record = new();
                for( int i = 0; i < reader.FieldCount; i++)
                {
                    record.Add(CleanupColumnName(reader.GetName(i)), reader[i]);
                }
                result.Add(record);
            }
            return result;

            static string CleanupColumnName(string columnName)
                => (columnName.Length > 1 && columnName[0] == '[' && columnName[^1] == ']')
                    ? columnName[1..^1]
                    : columnName;
        }

        public void PopulatePreview(AdomdConnection connection, TabularModel model, int previewRows = 5)
        {
            foreach( var tableChanges in ModifiedObjects )
            {
                // table has the column definitions
                // tableReference has the desired expression
                var table = model.Tables[tableChanges.Name];
                var tableReference = 
                    string.IsNullOrWhiteSpace(tableChanges.Expression) 
                    ? table 
                    : model.Tables.Find(tableChanges.Expression) ?? table;
                string? tableExpression = null;
                if (tableReference.Partitions.Count == 1)
                {
                    CalculatedPartitionSource? existingPartition = tableReference.Partitions[0].Source as CalculatedPartitionSource;
                    tableExpression = existingPartition?.Expression;
                }
                // Skip table if it is not a calculated table
                if (tableExpression == null) continue;

                string columns = string.Join(",\r\n    ", table.Columns.Select( column =>
                {
                    var calcColumn = column as CalculatedTableColumn;
                    var sourceColumn = calcColumn?.SourceColumn ?? column.Name;
                    if (sourceColumn.Length > 0 && sourceColumn[^1] != ']')
                    {
                        sourceColumn = $"[{sourceColumn}]";
                    }
                    sourceColumn = sourceColumn[sourceColumn.IndexOf('[')..];
                    string columnExpression =
                        (!string.IsNullOrWhiteSpace(calcColumn?.FormatString))
                        ? $"FORMAT( {sourceColumn}, \"{calcColumn.FormatString}\" )"
                        : sourceColumn;
                    string result = $"\"{column.Name}\", {columnExpression}";
                    return result;
                } ));
                var previewQuery = $"SELECTCOLUMNS (\r\n    {tableExpression},\r\n    {columns}\r\n)";
                tableChanges.Preview = GetPreviewData(connection, previewQuery, previewRows);
            }
        }
    }
}
