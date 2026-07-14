namespace Dax.Template.Syntax;

/// <summary>
/// The scope in which a <see cref="Var"/> is evaluated.
/// </summary>
public enum VarScope
{
    /// <summary>Evaluated once at the model/global level (see <see cref="VarGlobal"/>).</summary>
    Global,

    /// <summary>Evaluated within row context (see <see cref="VarRow"/>).</summary>
    Row
}