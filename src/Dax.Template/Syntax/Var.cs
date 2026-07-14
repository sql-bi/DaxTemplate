namespace Dax.Template.Syntax;

/// <summary>
/// Represents a DAX <c>VAR</c> definition: a named expression evaluated once and referenced
/// by <see cref="Name"/> in the generated DAX code. See <see cref="VarGlobal"/> and
/// <see cref="VarRow"/> for the concrete global-scope and row-context-scope variants.
/// </summary>
public abstract class Var : DaxBase, IDependencies<DaxBase>, IDaxName, IDaxComment
{
    bool IDependencies<DaxBase>.AddLevel { get; init; }
    public bool IgnoreAutoDependency { get; init; }

    /// <summary>
    /// The scope in which the <c>VAR</c> is evaluated (<see cref="VarScope.Global"/> or
    /// <see cref="VarScope.Row"/>). Set by the derived class constructor.
    /// </summary>
    public VarScope Scope { get; init; }

    /// <summary>The <c>VAR</c> identifier used in the generated DAX code.</summary>
    public required string Name { get; init; }

    /// <summary>The DAX expression assigned to the variable.</summary>
    public string? Expression { get; set; }
    public string[]? Comments { get; set; }
    public string DaxName => Name;

    public IDependencies<DaxBase>[]? Dependencies { get; set; }

    /// <summary>Returns <c>"VAR {Name}: {Expression}"</c> for debugging/diagnostics.</summary>
    public string GetDebugInfo() => $"VAR {Name}: {Expression}";
    public override string ToString() => $"{GetType().Name} : {Name}";
}