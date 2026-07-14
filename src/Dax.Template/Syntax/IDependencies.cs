namespace Dax.Template.Syntax;

/// <summary>
/// Contract implemented by DAX syntax elements that participate in the dependency graph used to
/// order variable/expression definitions (topologically sorted via <c>Extensions/TSort.cs</c>).
/// </summary>
/// <typeparam name="T">The base type of the dependent elements (constrained to <see cref="DaxBase"/>).</typeparam>
public interface IDependencies<T> where T : DaxBase
{
    /// <summary>Whether this element counts as a dependency level when ordering the graph.</summary>
    public bool AddLevel { get; init; }

    /// <summary>
    /// When <see langword="true"/>, suppresses the automatic dependency normally created for
    /// column embedding.
    /// </summary>
    public bool IgnoreAutoDependency { get; init; }

    /// <summary>The child elements this element depends on.</summary>
    public IDependencies<T>[]? Dependencies { get; set; }

    /// <summary>The generated DAX expression for this element.</summary>
    public string? Expression { get; set; }

    /// <summary>Returns a human-readable summary of this element, used for debugging/diagnostics.</summary>
    public string GetDebugInfo();
}