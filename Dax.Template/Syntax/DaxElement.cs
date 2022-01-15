namespace Dax.Template.Syntax
{
    /// <summary>
    /// Internal use to create automatic DAX code in templates
    /// This could be partial code, it has no name because it is assigned internally 
    /// to variables or to other DAX syntaxes.
    /// For example, it is used to create the GENERATE / ADDCOLUMNS functions to embed columns.
    /// Adding a DaxElement in the list of dependencies would replace the default DaxElement created
    /// for the level depending on the presence of variables. However, usually it is not used
    /// in the list of dependencies, using a DaxStep instead.
    /// </summary>
    public class DaxElement : DaxBase, IDependencies<DaxBase>
    {
        bool IDependencies<DaxBase>.AddLevel { get; init; } = true;
        public bool IgnoreAutoDependency { get; init; } = false;
        public string? Expression { get; set; } 

        public IDependencies<DaxBase>[]? Dependencies { get; set; } 
        public string GetDebugInfo() { return $"{this.GetType().Name}: {Expression}"; }

    }
}
