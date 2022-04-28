using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices.AdomdClient;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using TabularMeasure = Microsoft.AnalysisServices.Tabular.Measure;
using TabularHierarchy = Microsoft.AnalysisServices.Tabular.Hierarchy;
using System.Threading;
using Dax.Template.Exceptions;

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
        internal void AddColumn(TabularColumn column, Table? table, bool isRemoved)
        {
            AddColumn(column, table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected void AddColumn(TabularColumn column, Table? table, ICollection<TableChanges> collection)
        {
            if (column.Type == ColumnType.RowNumber) return;
            if (table == null) throw new TemplateException("Parent table not found");

            TableChanges tableChanges = AddTable(table, collection);
            if (tableChanges.Columns.Any(c => c.Name == column.Name)) return;

            var columnChanges = new ColumnChanges(column.Name)
            {
                IsHidden = column.IsHidden,
                DataType = column.DataType.ToString()
            };
            tableChanges.Columns.Add(columnChanges);
        }
        internal void AddMeasure(TabularMeasure measure, Table? table, bool isRemoved)
        {
            AddMeasure(measure, table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected void AddMeasure(TabularMeasure measure, Table? table, ICollection<TableChanges> collection)
        {
            if (table == null) throw new TemplateException("Parent table not found");

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
        internal void AddHierarchy(TabularHierarchy hierarchy, Table? table, bool isRemoved)
        {
            AddHierarchy(hierarchy, table, isRemoved ? RemovedObjects : ModifiedObjects);
        }
        protected void AddHierarchy(TabularHierarchy hierarchy, Table? table, ICollection<TableChanges> collection)
        {
            if (table == null) throw new TemplateException("Parent table not found");

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

        internal void SimplifyRemovedObjects(CancellationToken cancellationToken = default)
        {
            var removedObjects = RemovedObjects.ToArray();
            foreach( var tableChanges in removedObjects )
            {
                cancellationToken.ThrowIfCancellationRequested();

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
        private const string PREVIEW_PREFIX = "__PREVIEW__";

        private static string GetQueryTablesDefinition(
            TabularModel model,
            List<(string tableName, string expression, List<(string tableName, string expression)> innerTables)>? previewQueryTables)
        {
            string queryTablesDefinition = string.Empty;
            if (previewQueryTables?.Any() == true)
            {
                var internalTableNames = previewQueryTables.Select(qt => qt.tableName);
                var tableDefinitions =
                    from qt in previewQueryTables
                    select $"TABLE '{PREVIEW_PREFIX}{qt.tableName}' =\r\n{AddInnerVar(qt.innerTables)}{RenameTableReferences(qt.expression, internalTableNames.Union(qt.innerTables.Select(t=>t.tableName)).ToArray())}";

                queryTablesDefinition =
                    $"DEFINE\r\n{string.Join("\r\n", tableDefinitions)}";
            }
            return queryTablesDefinition;

            string AddInnerVar(List<(string tableName, string expression)> innerTables)
            {
                var internalTableNames = innerTables.Select(qt => qt.tableName).ToArray();
                var varDefinitions = innerTables.Select((it, index) =>
                {
                    var varName = TableNameToVarName(it.tableName, index);
                    return $"VAR {PREVIEW_PREFIX}{varName} =\r\n{RenameColumns(it.tableName, varName, RenameTableReferences(it.expression, internalTableNames))}";
                });
                return varDefinitions.Any() ? $"{string.Join("\r\n", varDefinitions)}\r\nRETURN\r\n" : string.Empty;
            }

            string RenameColumns(string tableName, string varName, string tableExpression)
            {
                var table = model.Tables[tableName];
                string columns = string.Join(
                    ",\r\n    ",
                    table.Columns.Where(c => c.Type != ColumnType.RowNumber).Select(column => $"\"'{PREVIEW_PREFIX}{varName}'[{column.Name}]\", [{column.Name}]"));
                var renamedTableExpression = $"SELECTCOLUMNS (\r\n    {tableExpression}\r\n    ,\r\n    {columns}\r\n)";
                return renamedTableExpression;
            }

            static string TableNameToVarName(string tableName, int index)
            {
                // https://docs.microsoft.com/it-it/dax/var-dax#parameters
                var allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
                var varName = tableName.Replace(" ", "_");

                foreach (var currentChar in varName.ToCharArray())
                {
                    if (!allowedChars.Contains(currentChar))
                        varName = varName.Replace(currentChar, '_');
                }

                if (varName != tableName)
                    varName += $"{ index }"; // if value is changed add unique index to ensure uniqueness 

                return varName;
            }
        }
        
        static string RenameTableReferences(string queryExpression, string[] renameTableNames)
        {
            foreach (var tableName in renameTableNames)
            {
                queryExpression = queryExpression.Replace($"'{tableName}'", $"'{PREVIEW_PREFIX}{tableName}'");
            }
            return queryExpression;
        }

        private static object? GetPreviewData(
            AdomdConnection connection,
            string? tableExpression,
            int previewRows,
            string? queryTablesDefinition)
        {
            if (string.IsNullOrWhiteSpace(tableExpression)) return null;
            queryTablesDefinition ??= string.Empty;

            string daxQuery = $"{queryTablesDefinition}\r\nEVALUATE TOPNSKIP ( {previewRows}, 0, {tableExpression} )";
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

        public void PopulatePreview(AdomdConnection connection, TabularModel model, int previewRows = 5, CancellationToken cancellationToken = default)
        {
            foreach( var tableChanges in ModifiedObjects )
            {
                cancellationToken.ThrowIfCancellationRequested();

                // table has the column definitions
                // tableReference has the desired expression
                var table = model.Tables[tableChanges.Name];
                string? tableExpression = GetTableExpression(model, tableChanges, table);
                // Skip table if it is not a calculated table
                if (tableExpression == null) continue;

                // Skip table if there are no changes in columns, because it is an original calculated column not modified by the template
                // (we ignore measures and hierarchies for the preview dependencies)
                if (!tableChanges.Columns.Any()) continue;

                // Search dependencies on other modified tables
                var referencedTables =
                    from t in ModifiedObjects
                    where t != tableChanges 
                       && tableExpression.Contains($"'{t.Name}'")
                       && t.Columns.Any() // Ignore calculated tables without columns modified by the template
                    select t;

                List<(string tableName, string expression, List<(string tableName, string expression)> innerTables)> previewQueryTables = new();
                if (referencedTables.Any())
                {
                    // For each reference table prepare the expression to include in the DEFINE TABLE statement
                    foreach (var referencedTable in referencedTables)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var modelTable = model.Tables[referencedTable.Name];
                        var referenceTableExpression = GetTableExpression(model, referencedTable, modelTable);
                        // Skip table if it is not a calculated table
                        if (referenceTableExpression == null) continue;
                        // Skip table if it is already in query tables
                        if (previewQueryTables.Any(t => t.tableName == referencedTable.Name)) continue;

                        var innerTables =
                            from t in ModifiedObjects
                            where t != tableChanges 
                                && t != referencedTable
                                && referenceTableExpression.Contains($"'{t.Name}'")
                                && t.Columns.Any() // Ignore calculated tables without columns modified by the template
                            select t;

                        List<(string tableName, string expression)> innerQueryTables = new();
                        foreach (var innerTable in innerTables )
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var innerModelTable = model.Tables[innerTable.Name];
                            var innerReferenceTableExpression = GetTableExpression(model, innerTable, innerModelTable);
                            // Skip table if it is not a calculated table
                            if (innerReferenceTableExpression == null) continue;
                            // Skip table if it is already in query tables
                            if (innerQueryTables.Any(t => t.tableName == innerTable.Name)) continue;
                            innerQueryTables.Add( (tableName: innerTable.Name, expression: innerReferenceTableExpression) );
                        }

                        // Add the table to the query tables for preview
                        previewQueryTables.Add( (tableName: referencedTable.Name, expression: referenceTableExpression, innerTables: innerQueryTables) );
                    }
                }

                string columns = string
                    .Join(",\r\n    ", table.Columns.Where(column => column.Type != ColumnType.RowNumber && column.Type != ColumnType.Calculated)
                    .Select(column =>
                {
                    var calcColumn = column as CalculatedTableColumn;
                    var sourceColumn = calcColumn?.SourceColumn ?? column.Name;
                    if (sourceColumn.Length > 0 && sourceColumn[^1] != ']')
                    {
                        sourceColumn = $"[{sourceColumn}]";
                    }
                    sourceColumn = sourceColumn[sourceColumn.IndexOf('[')..];

                    var columnExpression = sourceColumn;
                    if (!string.IsNullOrWhiteSpace(calcColumn?.FormatString))
                    {
                        // Escape double quotes i.e. a user-defined format expression like "POSITIVE";"NEGATIVE";"ZERO" should be escaped to ""POSITIVE"";""NEGATIVE"";""ZERO""
                        var escapedFormatString = calcColumn.FormatString.Replace("\"", "\"\"");
                        columnExpression = $"FORMAT( {sourceColumn}, \"{escapedFormatString}\" )";
                    }
                    
                    string result = $"\"{column.Name}\", {columnExpression}";
                    return result;
                }));
                var internalTableNames = previewQueryTables.Select(qt => qt.tableName).ToArray();
                var previewQuery = $"SELECTCOLUMNS (\r\n    {RenameTableReferences(tableExpression, internalTableNames)}\r\n    ,\r\n    {columns}\r\n)";
                string queryTablesDefinition = GetQueryTablesDefinition(model, previewQueryTables);
                tableChanges.Preview = GetPreviewData(connection, previewQuery, previewRows, queryTablesDefinition);
            }

            static string? GetTableExpression(TabularModel model, TableChanges tableChanges, Table table)
            {
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

                return tableExpression;
            }
        }
    }
}
