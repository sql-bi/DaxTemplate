using TabularHierarchy = Microsoft.AnalysisServices.Tabular.Hierarchy;

namespace Dax.Template.Model;

public class Hierarchy : EntityBase
{
    public string? DisplayFolder { get; init; }
    public bool IsHidden { get; init; }
    public Level[] Levels { get; init; } = [];
    internal TabularHierarchy? TabularHierarchy { get; set; }
    public override void Reset()
    {
        TabularHierarchy = null;
    }
}