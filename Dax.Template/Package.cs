using Dax.Template.Exceptions;
using Dax.Template.Extensions;
using Dax.Template.Tables;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dax.Template
{
    public class Package
    {
        internal const string TEMPLATE_FILE_EXTENSION = ".template.json";
        internal const string PACKAGE_CONFIG = "Config";

        private readonly string _path;
        private readonly JsonElement _content;
        private readonly TemplateConfiguration _configuration;
        private readonly string _directoryName;

        /// <summary>
        /// Load a <see cref="Package"/> from a template file
        /// </summary>
        /// <param name="path">Full path to the template file</param>
        public static Package LoadFromFile(string path)
        {
            var packageFile = new FileInfo(path);
            var packageText = File.ReadAllText(path);
            var packageDocument = JsonDocument.Parse(packageText);

            string configurationText;

            if (packageDocument.RootElement.TryGetProperty(PACKAGE_CONFIG, out var configurationElement))
            {
                if (configurationElement.ValueKind != JsonValueKind.Object)
                    throw new TemplateConfigurationException($"Invalid json value kind [{ PACKAGE_CONFIG }]");

                // File is a packaged template which contains the config and all referenced templates as embeded objects
                configurationText = configurationElement.GetRawText();
            }
            else
            {
                // File is an unpackaged template which only contains the config object, all referenced templates are mapped as external files
                configurationText = packageText;
            }

            var templateConfiguration = JsonSerializer.Deserialize<TemplateConfiguration>(configurationText) ?? throw new TemplateUnexpectedException("Deserialized configurationText is null");
            {
                templateConfiguration.TemplateUri = packageFile.ToTemplateUri();
                
                if (templateConfiguration.Name.IsNullOrEmpty())
                    templateConfiguration.Name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(packageFile.Name));
            }

            var package = new Package(packageFile, packageDocument, templateConfiguration);
            return package;
        }

        /// <summary>
        /// Search for existing templates within local path
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        public static IEnumerable<string> FindTemplateFiles(string path)
        {
            var templateFiles = Directory.EnumerateFiles(path, searchPattern: $"*{ TEMPLATE_FILE_EXTENSION }");
            return templateFiles;
        }

        private void FixExcludedTables()
        {
            var templateTables = from item in Configuration.Templates select item.Table;
            Configuration.ExceptTablesColumns = Configuration.ExceptTablesColumns.Union(templateTables).Distinct().ToArray();
        }
        private Package(FileInfo file, JsonDocument document, TemplateConfiguration configuration) 
        {
            _path = file.FullName;
            _content = document.RootElement;
            _configuration = configuration;

            _directoryName = file.DirectoryName ?? throw new TemplateUnexpectedException($"DirectoryName is null");

            // Add template tables to excluded tables
            FixExcludedTables();
        }

        public TemplateConfiguration Configuration => _configuration;

        public T ReadDefinition<T>(string name)
        {
            string definitionName = Path.GetExtension(name).EqualsI(".json") ? Path.GetFileNameWithoutExtension(name) : name;
            string definitionText;

            if (_content.TryGetProperty(definitionName, out var element))
            {
                definitionText = element.GetRawText();
            }
            else
            {
                definitionText = File.ReadAllText(path: Path.Combine(_directoryName, name));
            }

            return JsonSerializer.Deserialize<T>(definitionText) ?? throw new TemplateUnexpectedException($"Deserialized definition is null [{ definitionName }]");
        }

        public void SaveTo(string path)
        {
            Dictionary<string, object> package = new();
            package.Add(PACKAGE_CONFIG, Configuration);

            var fileNames =
                from t in Configuration.Templates
                where !string.IsNullOrEmpty(t.Template)
                select t.Template;

            fileNames = fileNames.Union(
                from t in Configuration.Templates
                from l in t.LocalizationFiles
                where !string.IsNullOrEmpty(l)
                select l).Distinct();

            foreach (var fileName in fileNames)
            {
                var filePath = Path.Combine(_directoryName, fileName);
                var fileText = File.ReadAllText(filePath);

                var content = JsonSerializer.Deserialize<dynamic>(fileText);
                var name = Path.GetFileNameWithoutExtension(fileName);

                package.Add(name, content);
            }

            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            var packageText = JsonSerializer.Serialize(package, options);

            File.WriteAllText(path, packageText);
        }
    }
}
