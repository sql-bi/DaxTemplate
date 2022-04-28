namespace Dax.Template.Syntax
{
    public abstract class Var : DaxBase, IDependencies<DaxBase>, IDaxName, IDaxComment
    {
        bool IDependencies<DaxBase>.AddLevel { get; init; } = false;
        public bool IgnoreAutoDependency { get; init; } = false;

        public VarScope Scope { get; init; } 
        public string Name { get; init; } = default!;
        public string? Expression { get; set; }
        public string[]? Comments { get; set; } 
        public string DaxName { get { return Name; } }

        public IDependencies<DaxBase>[]? Dependencies { get; set; } 
        public string GetDebugInfo() { return $"VAR {Name}: {Expression}"; }
        public override string ToString()
        {
            return $"{GetType().Name} : {Name}";
        }
    }
}
