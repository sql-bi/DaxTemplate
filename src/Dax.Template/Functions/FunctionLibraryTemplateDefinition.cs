using System.Linq;

namespace Dax.Template.Functions;

/// <summary>
/// External JSON definition of a library of TOM <see cref="Microsoft.AnalysisServices.Tabular.Function"/>
/// (DAX user-defined function) objects, read by <see cref="FunctionLibraryTemplate"/> via
/// <see cref="Package.ReadDefinition{T}"/>. Each entry in <see cref="Functions"/> becomes one
/// <see cref="Microsoft.AnalysisServices.Tabular.Function"/> attached to <c>Model.Functions</c>.
/// </summary>
public class FunctionLibraryTemplateDefinition
{
    /// <summary>The functions to create in the library.</summary>
    public FunctionDefinition[] Functions { get; set; } = [];
}

/// <summary>
/// External JSON definition of a single <see cref="Microsoft.AnalysisServices.Tabular.Function"/>.
/// Exactly one of <see cref="RawExpression"/> or <see cref="Body"/>/<see cref="MultiLineBody"/> must be
/// set — never both, never neither (enforced by <see cref="FunctionLibraryTemplate"/> before any
/// mutation). When <see cref="RawExpression"/> is set (an escape hatch: the literal
/// <c>( params ) => body</c> TOM expression string, used verbatim), <see cref="Parameters"/> is ignored,
/// not rejected.
/// </summary>
public class FunctionDefinition
{
    /// <summary>Name of the function.</summary>
    public required string Name { get; set; }

    /// <summary>Optional description applied to the function.</summary>
    public string? Description { get; set; }

    /// <summary>Applied to <see cref="Microsoft.AnalysisServices.Tabular.Function.IsHidden"/>.</summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// The function's parameters, in declaration order. Ignored when <see cref="RawExpression"/> is set.
    /// </summary>
    public ParameterDefinition[] Parameters { get; set; } = [];

    /// <summary>Single-line DAX body of the function. Mutually exclusive in practice with <see cref="MultiLineBody"/> (see <see cref="GetBody"/>).</summary>
    public string? Body { get; set; }

    /// <summary>Multi-line DAX body of the function, used when <see cref="Body"/> is unset (see <see cref="GetBody"/>).</summary>
    public string[]? MultiLineBody { get; set; }

    /// <summary>
    /// Escape hatch: the literal <c>( params ) => body</c> TOM expression string, used verbatim as
    /// <see cref="Microsoft.AnalysisServices.Tabular.Function.Expression"/> instead of assembling it from
    /// <see cref="Parameters"/> and <see cref="GetBody"/>. Mutually exclusive with <see cref="Body"/>/
    /// <see cref="MultiLineBody"/> (enforced by <see cref="FunctionLibraryTemplate"/>); when set,
    /// <see cref="Parameters"/> is ignored, not rejected.
    /// </summary>
    public string? RawExpression { get; set; }

    /// <summary>
    /// Returns <see cref="Body"/> when set; otherwise joins <see cref="MultiLineBody"/> with a leading
    /// <c>\r\n</c> per line, mirroring <c>Dax.Template.Measures.MeasuresTemplateDefinition.MeasureTemplate.GetExpression</c>.
    /// </summary>
    public string? GetBody()
    {
        return (string.IsNullOrEmpty(Body) && MultiLineBody != null)
            ? string.Join("", MultiLineBody.Select(line => $"\r\n{line}"))
            : Body;
    }
}

/// <summary>
/// External JSON definition of a single DAX user-defined-function parameter:
/// <c>&lt;Name&gt; [ : &lt;Type&gt; [&lt;Subtype&gt;] [&lt;PassingMode&gt;] ] [ = &lt;DefaultExpression&gt; ]</c>.
/// </summary>
public class ParameterDefinition
{
    /// <summary>Name of the parameter.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The parameter type: <c>ANYVAL</c> (default when unset), <c>SCALAR</c> (requires <see cref="Subtype"/>),
    /// <c>TABLE</c>, a scalar-subtype shorthand (<c>VARIANT</c>, <c>INT64</c>, <c>DECIMAL</c>, <c>DOUBLE</c>,
    /// <c>STRING</c>, <c>BOOLEAN</c>, <c>DATETIME</c>, <c>NUMERIC</c>), or a reference type (<c>ANYREF</c>,
    /// <c>MEASUREREF</c>, <c>COLUMNREF</c>, <c>TABLEREF</c>, <c>CALENDARREF</c>).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>Scalar subtype, required when <see cref="Type"/> is <c>SCALAR</c>.</summary>
    public string? Subtype { get; set; }

    /// <summary>
    /// <c>VAL</c> (eager, the default when omitted) or <c>EXPR</c> (lazy). Not applicable to reference
    /// types, which are always passed by reference: setting this alongside a reference <see cref="Type"/>
    /// is rejected by <see cref="FunctionLibraryTemplate"/>.
    /// </summary>
    public string? PassingMode { get; set; }

    /// <summary>
    /// DAX expression for the parameter's default value, making it optional. Once a parameter has a
    /// default, every following parameter must also have one (enforced by <see cref="FunctionLibraryTemplate"/>).
    /// </summary>
    public string? DefaultExpression { get; set; }
}