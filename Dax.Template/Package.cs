using Dax.Template.Exceptions;
using Dax.Template.Tables;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text.Json;

namespace Dax.Template
{
    public class Package
    {
        public const string PACKAGE_CONFIG_KEY = "Config";
        protected Package() 
        {
        }
        public string? Path { get; set; }
        public TemplateConfiguration Configuration { get; init; } = new();
        public IDictionary<string, object>? Content { get; set; }

        public T? GetContent<T>(string key)
        {
            return GetContent<T>(key, Content);
        }
        private static T? GetContent<T>(string key, IDictionary<string, object>? content)
        {
            if (content == null)
            {
                throw new InvalidConfigurationException($"Package content {key}");
            }
            return JsonSerializer.Deserialize<T>(content[key].ToString() ?? string.Empty);
        }

        private string GetFullPath(string filename)
        {
            return System.IO.Path.Combine(Path ?? string.Empty, filename);
        }

        protected T ReadFileDefinition<T>(string filename)
        {
            string json = File.ReadAllText(GetFullPath(filename));
            if (JsonSerializer.Deserialize(
                    json,
                    typeof(T))
                is not T templateDefinition)
            {
                throw new InvalidConfigurationException($"Invalid definition: {filename}");
            }
            return templateDefinition;
        }
        protected T ReadEmbeddedDefinition<T>(string contentKey)
        {
            T? definition = GetContent<T>(contentKey);
            if (definition == null)
            {
                throw new InvalidConfigurationException($"Missing definition: {contentKey}");
            }
            return definition;
        }
        public T ReadDefinition<T>(string filename)
        {
            string contentKey = 
                System.IO.Path.GetExtension(filename).ToLower() == ".json" 
                ? System.IO.Path.GetFileNameWithoutExtension(filename) 
                : filename;
            if (Content?.ContainsKey(contentKey) == true)
            {
                return ReadEmbeddedDefinition<T>(contentKey);
            }
            else
            {
                return ReadFileDefinition<T>(filename);
            }
        }
        static public Package LoadPackage(string pathPackage)
        {
            string? pathRoot = System.IO.Path.GetDirectoryName(pathPackage);
            string json = File.ReadAllText(pathPackage);
            var content = JsonSerializer.Deserialize<ExpandoObject>(json) as IDictionary<string, object>;
            if (content?.ContainsKey(PACKAGE_CONFIG_KEY) == true)
            {
                TemplateConfiguration? configuration = GetContent<TemplateConfiguration>(PACKAGE_CONFIG_KEY, content);
                if (configuration == null)
                {
                    throw new TemplateException("Invalid package, missing configuration");
                }
                if (string.IsNullOrEmpty(configuration.Name))
                {
                    configuration.Name = System.IO.Path.GetFileNameWithoutExtension(pathPackage);
                }
                Package package = new() { Configuration = configuration, Content = content };
                return package;
            }
            {
                // The file is config only, mapping external files
                var configUnchecked = JsonSerializer.Deserialize<TemplateConfiguration>(json);
                if (configUnchecked is not TemplateConfiguration config) throw new TemplateException("Invalid configuration");
                if (string.IsNullOrEmpty(configUnchecked.Name))
                {
                    configUnchecked.Name = System.IO.Path.GetFileNameWithoutExtension(pathPackage);
                }
                Package package = new() { Path = pathRoot, Configuration = config };
                return package;
            }
        }

    }
}
