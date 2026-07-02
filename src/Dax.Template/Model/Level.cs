using TabularLevel = Microsoft.AnalysisServices.Tabular.Level;

namespace Dax.Template.Model;

public class Level : EntityBase
{
    public required Column Column { get; init; }
    internal TabularLevel? TabularLevel { get; set; }
    public override void Reset()
    {
        TabularLevel = null;
    }
}