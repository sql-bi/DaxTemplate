namespace Dax.Template.Interfaces
{
    public interface IDateTemplateConfig : ICustomTableConfig
    {
        public int? FirstYearMin { get; set; }
        public int? FirstYearMax { get; set; }
        public int? LastYearMin { get; set; }
        public int? LastYearMax { get; set; }

        public Tables.Dates.HolidaysConfig? HolidaysReference { get; set; }
    }
}
