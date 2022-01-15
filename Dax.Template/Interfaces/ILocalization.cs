namespace Dax.Template.Interfaces
{
    public interface ILocalization 
    {
        public string? IsoTranslation { get; set; }
        public string? IsoFormat { get; set; }
        public string[] LocalizationFiles { get; set; }
    }
}
