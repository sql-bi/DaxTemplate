using System.Diagnostics.CodeAnalysis;

namespace Dax.Template.Model
{
    public abstract class EntityBase
    {
        public string Name { get; init; } = default!;
        public string? Description { get; set; }

        /// <summary>
        /// Reset internal state for Tabular objects
        /// </summary>
        public abstract void Reset();

        // Debugger/diagnostics-only display helper; never invoked by production logic or the
        // offline golden-file suite (see docs/design/coverage.md exclusion policy).
        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"{GetType().Name} : {Name}";
        }
    }
}
