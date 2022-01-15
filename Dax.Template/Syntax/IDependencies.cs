namespace Dax.Template.Syntax
{
    public interface IDependencies<T> where T : DaxBase
    {
        public bool AddLevel { get; init; }
        public bool IgnoreAutoDependency { get; init; }
        public IDependencies<T>[]? Dependencies { get; set; }
        public string? Expression { get; set; } 
        public string GetDebugInfo();
    }
}
    