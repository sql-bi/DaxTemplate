namespace Dax.Template.Syntax;

/// <summary>
/// A <see cref="IDependencies{T}"/> element that is a named DAX element and exposes an
/// identifier that other elements can reference.
/// </summary>
public interface IDaxName : IDependencies<DaxBase>
{
    /// <summary>The identifier this DAX element exposes.</summary>
    public string DaxName { get; }
}