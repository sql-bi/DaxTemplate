namespace Dax.Template.Syntax;

/// <summary>
/// A <see cref="Var"/> fixed to <see cref="VarScope.Row"/> (row-context) scope.
/// </summary>
public class VarRow : Var
{
    public VarRow() => Scope = VarScope.Row;
}