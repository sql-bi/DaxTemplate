namespace Dax.Template.Syntax;

/// <summary>
/// A DAX syntax element that carries optional comments to be emitted alongside the generated DAX.
/// </summary>
public interface IDaxComment
{
    /// <summary>Optional comment lines emitted alongside the generated DAX code.</summary>
    public string[]? Comments { get; set; }
}