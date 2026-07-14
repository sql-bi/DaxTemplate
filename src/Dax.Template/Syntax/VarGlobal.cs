namespace Dax.Template.Syntax;

/// <summary>
/// A <see cref="Var"/> fixed to <see cref="VarScope.Global"/> scope.
/// </summary>
public class VarGlobal : Var, IGlobalScope
{
    public VarGlobal() => Scope = VarScope.Global;

    /// <summary>
    /// Whether this variable's expression can be overridden via template configuration.
    /// </summary>
    public bool IsConfigurable { get; set; }
}