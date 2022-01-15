using TabularLevel = Microsoft.AnalysisServices.Tabular.Level;

namespace Dax.Template.Model
{
    public class Level : EntityBase
    {
        public Column Column { get; init; } = default!;
        internal TabularLevel? TabularLevel { get; set; }
        public override void Reset()
        {
            TabularLevel = null;
        }
    }
}
