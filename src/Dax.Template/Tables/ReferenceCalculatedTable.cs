using Dax.Template.Model;
using System.Threading;

namespace Dax.Template.Tables
{
    public abstract class ReferenceCalculatedTable : CalculatedTableTemplateBase
    {
        public string? HiddenTable { get; init; } 

        public override string? GetDaxTableExpression(Microsoft.AnalysisServices.Tabular.Model? model, CancellationToken cancellationToken = default)
        {
            return QuotedHiddenTable ?? base.GetDaxTableExpression(model, cancellationToken);
        }

        private string? QuotedHiddenTable { get
            {
                // TODO: there could be a bug in TOM or SSAS because when we use a quoted identifier
                //       in Source Column, the column is considered "invalid" if we try to deploy a change 
                //       to hierarchies or relationships that reference the column that uses this identifier
                //       Therefore, we keep the "unquoted" table name in the Source Column Name property 
                //       so it works with table names that don't require quotes in the name.
                //
                //if (HiddenTable == null)
                //{
                //    return null;
                //}
                //else
                //{
                //    return $"'{HiddenTable}'";
                //}
                return HiddenTable;
            }
        }

        protected override string GetSourceColumnName(Column column)
        {
            return $"{QuotedHiddenTable ?? string.Empty}[{column.Name}]";
        }
    }
}
