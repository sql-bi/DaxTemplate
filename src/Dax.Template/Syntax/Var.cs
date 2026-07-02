namespace Dax.Template.Syntax;

public abstract class Var : DaxBase, IDependencies<DaxBase>, IDaxName, IDaxComment
{
    bool IDependencies<DaxBase>.AddLevel { get; init; }
    public bool IgnoreAutoDependency { get; init; }

    public VarScope Scope { get; init; }
    public string Name { get; init; } = default!;
    public string? Expression { get; set; }
    public string[]? Comments { get; set; }
    public string DaxName => Name;

    public IDependencies<DaxBase>[]? Dependencies { get; set; }
    public string GetDebugInfo() => $"VAR {Name}: {Expression}";
    public override string ToString() => $"{GetType().Name} : {Name}";
}