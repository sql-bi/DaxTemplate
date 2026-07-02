using System.Collections.Generic;
using System.Linq;

namespace Dax.Template;

public class Translations
{
    #region Internal translation entities definition
    public class Entity
    {
        public string? OriginalName { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class EntityDisplayFolder : Entity
    {
        public string? DisplayFolders { get; set; }
    }
    public class EntityFormat : EntityDisplayFolder
    {
        public string? FormatString { get; set; }
    }
    public class Level : Entity { }
    public class Hierarchy : EntityDisplayFolder
    {
        public Level[] Levels { get; set; } = [];
    }
    public class Measure : EntityFormat { }
    public class Column : EntityFormat { }
    public class Table : Entity { }
    #endregion

    public class Language
    {
        public string? Iso { get; set; }
        public Table? Table { get; set; }
        public Measure[] Measures { get; set; } = [];
        public Column[] Columns { get; set; } = [];
        public Hierarchy[] Hierarchies { get; set; } = [];
    }

    public class Definitions
    {
        public Language[] Translations { get; set; } = [];
    }

    protected Definitions LanguageDefinitions;

    /// <summary>
    /// Define the Iso translation to use as default name, ignoring translations
    /// </summary>
    public string? DefaultIso { get; set; }
    /// <summary>
    /// List of ISO translations to apply
    /// </summary>
    public string[] ApplyIso { get; set; } = [];
    /// <summary>
    /// TRUE if all the ISO translations available should be applied as translations
    /// </summary>
    public bool ApplyAllIso { get; set; }
    public Translations(Definitions definitions)
    {
        LanguageDefinitions = definitions;
    }

    public Language? GetTranslationIso(string iso)
    {
        // First, search for perfect match ("it-IT" must be "it-IT", "it" must be "it")
        var matchingTranslation = LanguageDefinitions.Translations.FirstOrDefault(t => t.Iso == iso);
        if (matchingTranslation == null)
        {
            // Second, search for generic match ("it" instead of "it-IT")
            var genericIsoLanguage = iso[..2];
            matchingTranslation = LanguageDefinitions.Translations.FirstOrDefault(t => t.Iso == genericIsoLanguage);
            if (matchingTranslation == null)
            {
                // Third, search for the first compatible match ("it-IT" instead of "it-CH" or "it")
                matchingTranslation = LanguageDefinitions.Translations.FirstOrDefault(t => t.Iso?[..2] == genericIsoLanguage);
            }
        }
        return matchingTranslation;
    }

    public IEnumerable<Language> GetTranslations()
    {
        return LanguageDefinitions.Translations;
    }
}