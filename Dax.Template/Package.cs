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
        private const string PACKAGE_CONFIG_KEY = "Config";

        public static Package Load(string path)
        {
            var packageFile = new FileInfo(path);
            var packageText = System.IO.File.ReadAllText(path);
            var packageDocument = JsonDocument.Parse(packageText);

            string configurationText;

            if (packageDocument.RootElement.TryGetProperty(PACKAGE_CONFIG_KEY, out var configurationElement))
            {
                if (configurationElement.ValueKind != JsonValueKind.Object)
                    throw new TemplateConfigurationException($"Invalid json object [{ PACKAGE_CONFIG_KEY }]");

                // File is a packaged template which contains the config and all referenced templates as embeded objects
                configurationText = configurationElement.GetRawText();
            }
            else
            {
                // File is an unpackaged template which only contains the config object, all referenced templates are mapped as external files
                configurationText = packageText;
            }

            var templateConfiguration = JsonSerializer.Deserialize<TemplateConfiguration>(configurationText) ?? throw new TemplateUnexpectedException("Deserialized configurationText is null");
            if (templateConfiguration.Name.IsNullOrEmpty())
                templateConfiguration.Name = Path.GetFileNameWithoutExtension(packageFile.Name);

            var package = new Package(packageFile, packageDocument, templateConfiguration);
            return package;
        }

        private Package(FileInfo file, JsonDocument document, TemplateConfiguration configuration) 
        {
            File = file;
            Content = document.RootElement;
            Configuration = configuration;
        }

        public FileInfo File { get; private set; }

        public JsonElement Content { get; private set; }

        public TemplateConfiguration Configuration { get; private set; }

        public string PackagePath => File.DirectoryName ?? throw new TemplateUnexpectedException($"DirectoryName is null");

        public T ReadDefinition<T>(string name)
        {
            string definitionName = Path.GetExtension(name).EqualsI(".json") ? Path.GetFileNameWithoutExtension(name) : name;
            string definitionText;

            if (Content.TryGetProperty(definitionName, out var element))
            {
                definitionText = element.GetRawText();
            }
            else
            {
                definitionText = System.IO.File.ReadAllText(path: Path.Combine(PackagePath, name));
            }

            return JsonSerializer.Deserialize<T>(definitionText) ?? throw new TemplateUnexpectedException($"Deserialized json is null [{ definitionName }]");
        }

        public void SaveTo(string path)
        {
            Dictionary<string, object> package = new();
            package.Add(PACKAGE_CONFIG_KEY, Configuration);

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
                string filePath = Path.Combine(PackagePath, fileName);
                string fileText = System.IO.File.ReadAllText(filePath);

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

            System.IO.File.WriteAllText(path, packageText);
        }
    }
}
