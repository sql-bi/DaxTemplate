using System.Linq;

namespace Dax.Template.Tables.CalculationGroups;

/// <summary>
/// External JSON definition of a TOM <see cref="Microsoft.AnalysisServices.Tabular.CalculationGroup"/> to
/// attach to a table, read by <see cref="CalculationGroupTemplate"/> via <see cref="Package.ReadDefinition{T}"/>.
/// This is a generic calculation-group generator: the JSON author defines any calculation items and
/// selection-expression DAX, independent of the <c>Measures</c>/<c>Syntax</c>/time-intelligence-macro
/// machinery elsewhere in this library.
/// </summary>
public class CalculationGroupTemplateDefinition
{
    /// <summary>Precedence of the calculation group, applied to <see cref="Microsoft.AnalysisServices.Tabular.CalculationGroup.Precedence"/>.</summary>
    public int Precedence { get; set; }

    /// <summary>Name of the single string data column backing the calculation group.</summary>
    public required string ColumnName { get; set; }

    /// <summary>Optional description applied to the <see cref="Microsoft.AnalysisServices.Tabular.CalculationGroup"/>.</summary>
    public string? Description { get; set; }

    /// <summary>The calculation items to create on the calculation group.</summary>
    public CalculationItemDefinition[] CalculationItems { get; set; } = [];

    /// <summary>
    /// DAX expression for <see cref="Microsoft.AnalysisServices.Tabular.CalculationGroup.MultipleOrEmptySelectionExpression"/>.
    /// Requires database compatibility level &gt;= 1605 (enforced by TOM at set-time); when
    /// <see langword="null"/> or empty, any previously-set expression is cleared.
    /// </summary>
    public string? MultipleOrEmptySelectionExpression { get; set; }

    /// <summary>Optional format-string DAX expression for <see cref="MultipleOrEmptySelectionExpression"/>.</summary>
    public string? MultipleOrEmptySelectionFormatStringExpression { get; set; }

    /// <summary>
    /// DAX expression for <see cref="Microsoft.AnalysisServices.Tabular.CalculationGroup.NoSelectionExpression"/>.
    /// Requires database compatibility level &gt;= 1605 (enforced by TOM at set-time); when
    /// <see langword="null"/> or empty, any previously-set expression is cleared.
    /// </summary>
    public string? NoSelectionExpression { get; set; }

    /// <summary>Optional format-string DAX expression for <see cref="NoSelectionExpression"/>.</summary>
    public string? NoSelectionFormatStringExpression { get; set; }

    /// <summary>External JSON definition of a single <see cref="Microsoft.AnalysisServices.Tabular.CalculationItem"/>.</summary>
    public class CalculationItemDefinition
    {
        /// <summary>Name of the calculation item.</summary>
        public required string Name { get; set; }

        /// <summary>Optional description applied to the calculation item.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Explicit ordinal of the calculation item. When unset, the item's position in
        /// <see cref="CalculationItems"/> is used instead (see <see cref="CalculationGroupTemplate.ApplyTemplate"/>
        /// for the uniqueness rule across explicit and implicit ordinals).
        /// </summary>
        public int? Ordinal { get; set; }

        /// <summary>Single-line DAX expression for the calculation item. Mutually exclusive in practice with <see cref="MultiLineExpression"/> (see <see cref="GetExpression"/>).</summary>
        public string? Expression { get; set; }

        /// <summary>Multi-line DAX expression for the calculation item, used when <see cref="Expression"/> is unset (see <see cref="GetExpression"/>).</summary>
        public string[]? MultiLineExpression { get; set; }

        /// <summary>Optional format-string DAX expression for the calculation item.</summary>
        public string? FormatStringExpression { get; set; }

        /// <summary>
        /// Returns <see cref="Expression"/> when set; otherwise joins <see cref="MultiLineExpression"/> with a
        /// leading <c>\r\n</c> per line, mirroring <c>Dax.Template.Measures.MeasuresTemplateDefinition.MeasureTemplate.GetExpression</c>.
        /// </summary>
        public string? GetExpression()
        {
            return (string.IsNullOrEmpty(Expression) && MultiLineExpression != null)
                ? string.Join("", MultiLineExpression.Select(line => $"\r\n{line}"))
                : Expression;
        }
    }
}