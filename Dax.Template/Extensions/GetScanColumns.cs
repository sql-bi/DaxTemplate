using System.Linq;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using Column = Dax.Template.Model.Column;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using TabularColumn = Microsoft.AnalysisServices.Tabular.Column;
using System.Text.RegularExpressions;
using Dax.Template.Interfaces;
using Dax.Template.Enums;

namespace Dax.Template.Extensions
{
    public static partial class Extensions
    {
        private static readonly Regex regexTable = new(@".+(?=\[)", RegexOptions.Compiled);
        private static readonly Regex regexColumn = new(@"(\[.+\])", RegexOptions.Compiled);

        public static (string? tableName, string? columnName) SplitDaxIdentifier(string daxIdentifier)
        {
            var matchTable = regexTable.Matches(daxIdentifier);
            var matchColumn = regexColumn.Matches(daxIdentifier);

            var columnName = matchColumn.FirstOrDefault()?.Value;
            var tableName = matchTable.FirstOrDefault()?.Value;
            if (tableName == null && columnName == null) tableName = daxIdentifier;

            // Remove quoted identifier for table name
            if (tableName?[0] == '\'')
            {
                tableName = tableName[1..^1];
            }
            return (tableName, columnName);
        }

        public static IEnumerable<TabularColumn>? GetScanColumns(this TabularModel model, IScanConfig Config, string? dataCategory = null)
        {
            IEnumerable<TabularColumn>? scanColumns = null;
            var scanTargets =
                from item in Config.OnlyTablesColumns
                select SplitDaxIdentifier(item);
            var exceptTargets =
                from item in Config.ExceptTablesColumns
                select SplitDaxIdentifier(item);
            var exceptTables = exceptTargets.Where(x => string.IsNullOrEmpty(x.columnName)).ToList();
            var exceptColumns = exceptTargets.Where(x => !string.IsNullOrEmpty(x.columnName)).ToList();

            if ((Config.AutoScan & AutoScanEnum.SelectedTablesColumns) == AutoScanEnum.SelectedTablesColumns)
            {
                List<Column> columnsToScan = new();
                bool scanAll = Config.OnlyTablesColumns.Length == 0;

                var onlyTables = scanTargets.Where(x => string.IsNullOrEmpty(x.columnName)).ToList();
                var onlyColumns = scanTargets.Where(x => !string.IsNullOrEmpty(x.columnName)).ToList();

                scanColumns =
                    from t in model.Tables
                    from c in t.Columns
                    where c.DataType == DataType.DateTime
                        && (
                            scanAll
                            || onlyTables.Any(o => o.tableName == t.Name)
                            || onlyColumns.Any(o => o.columnName == c.Name && o.tableName == t.Name)
                        )
                        && !exceptTables.Any(o => o.tableName == t.Name)
                        && !exceptColumns.Any(o => o.columnName == c.Name && o.tableName == t.Name)
                        && (dataCategory == null || t.DataCategory != dataCategory) // DATACATEGORY_TIME
                    select c;
            }
            bool checkInactive = (Config.AutoScan & AutoScanEnum.ScanInactiveRelationships) == AutoScanEnum.ScanInactiveRelationships;
            bool checkActive = (Config.AutoScan & AutoScanEnum.ScanActiveRelationships) == AutoScanEnum.ScanActiveRelationships;
            if ( checkInactive || checkActive )
            {
                var scanRelationshipsFrom =
                    from r in (
                        from r in model.Relationships
                        where r is SingleColumnRelationship
                        select r as SingleColumnRelationship)
                    where r.FromColumn.DataType == DataType.DateTime
                        && r.FromCardinality == RelationshipEndCardinality.Many
                        && (dataCategory == null || r.FromTable.DataCategory != dataCategory) // DATACATEGORY_TIME
                        && ((checkActive && r.IsActive) || (checkInactive && !r.IsActive))
                        && !exceptTables.Any(o => o.tableName == r.FromTable.Name)
                        && !exceptColumns.Any(o => o.columnName == r.FromColumn.Name && o.tableName == r.FromTable.Name)
                    select r.FromColumn;
                var scanRelationshipsTo =
                    from r in (
                        from r in model.Relationships
                        where r is SingleColumnRelationship
                        select r as SingleColumnRelationship)
                    where r.ToColumn.DataType == DataType.DateTime
                        && r.ToCardinality == RelationshipEndCardinality.Many 
                        && (dataCategory == null || r.ToTable.DataCategory != dataCategory) // DATACATEGORY_TIME
                        && ((checkActive && r.IsActive) || (checkInactive && !r.IsActive))
                        && !exceptTables.Any(o => o.tableName == r.ToTable.Name)
                        && !exceptColumns.Any(o => o.columnName == r.ToColumn.Name && o.tableName == r.ToTable.Name)
                    select r.ToColumn;
                var scanRelationships = scanRelationshipsFrom.Union(scanRelationshipsTo);
                scanColumns = (scanColumns == null) ? scanRelationships : scanColumns.Union(scanRelationships);
            }
            return scanColumns;
        }

    }
}
