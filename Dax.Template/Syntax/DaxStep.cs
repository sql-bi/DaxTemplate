namespace Dax.Template.Syntax
{
    /// <summary>
    /// Explicit calculation step that can be included in the dependencies list.
    /// It is usually a table expression assigned to the variable specified in the Name property.
    /// The last DaxStep added to the list of dependencies is considered as a default reference for
    /// the following automatix DaxElement created to embed columns.
    /// </summary>
    public class DaxStep : DaxElement, IDaxName, IDaxComment
    {
        public string Name { get; init; } = default!;
        public string DaxName { get { return Name; } }
        public string[]? Comments { get; set; }

        public override string ToString()
        {
            return $"{GetType().Name} : {DaxName}";
        }
    }
}
