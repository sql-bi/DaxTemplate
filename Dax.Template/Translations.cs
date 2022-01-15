using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dax.Template
{
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
            public Level[] Levels { get; set; } = Array.Empty<Level>();
        }
        public class Measure : EntityFormat { }
        public class Column : EntityFormat { }
        public class Table : Entity { }
        #endregion

        public class Language
        {
            public string? Iso { get; set; } 
            public Table? Table { get; set; } 
            public Measure[] Measures { get; set; } = Array.Empty<Measure>();
            public Column[] Columns { get; set; } = Array.Empty<Column>();
            public Hierarchy[] Hierarchies { get; set; } = Array.Empty<Hierarchy>();
        }

        public class Definitions
        {
            public Language[] Translations { get; set; } = Array.Empty<Language>();
        }

        protected Definitions LanguageDefinitions;

        /// <summary>
        /// Define the Iso translation to use as default name, ignoring translations
        /// </summary>
        public string? DefaultIso { get; set; } 
        /// <summary>
        /// List of ISO translations to apply
        /// </summary>
        public string[] ApplyIso { get; set; } = Array.Empty<string>();
        /// <summary>
        /// TRUE if all the ISO translations available should be applied as translations
        /// </summary>
        public bool ApplyAllIso { get; set; } = false;
        public Translations(Definitions definitions)
        {
            LanguageDefinitions = definitions;
        }

        public Language GetTranslationIso( string iso )
        {
            return LanguageDefinitions.Translations.First(t => t.Iso == iso); 
        }

        public IEnumerable<Language> GetTranslations()
        {
            return LanguageDefinitions.Translations;
        }
    }
}
