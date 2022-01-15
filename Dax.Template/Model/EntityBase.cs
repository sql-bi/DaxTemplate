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
        public override string ToString()
        {
            return $"{GetType().Name} : {Name}";
        }
    }
}
