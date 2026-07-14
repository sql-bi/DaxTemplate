namespace Dax.Template.Syntax;

/// <summary>
/// Root base type for all DAX syntax elements (<see cref="DaxElement"/>, <see cref="Var"/>).
/// It carries no members of its own; it exists as the common type and as the generic constraint
/// used by <see cref="IDependencies{T}"/> (<c>where T : DaxBase</c>) to build dependency graphs.
/// </summary>
public abstract class DaxBase
{
}