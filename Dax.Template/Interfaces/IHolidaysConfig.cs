namespace Dax.Template.Interfaces
{
    public interface IHolidaysConfig: IDateTemplateConfig
    {
        public string? HolidaysDefinitionTemplate { get; set; }
        public string? IsoCountry { get; set; }
        public string? InLieuOfPrefix { get; set; } 
        public string? InLieuOfSuffix { get; set; }
        public string? HolidaysDefinitionTable { get; set; }
        public string? WorkingDays { get; set; } 
    }
}
