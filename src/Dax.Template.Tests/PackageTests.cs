namespace Dax.Template.Tests
{
    using System;
    using System.IO;
    using Xunit;

    public class PackageTests
    {
        private const string StandardTemplatePath = @".\_data\Templates\Config-01 - Standard.template.json";
        private const string TemplatePath = @".\_data\Templates";

        [Fact]
        public void FindTemplateFiles_NotEmptyTest()
        {
            var templates = Package.FindTemplateFiles(TemplatePath);

            Assert.NotEmpty(templates);
        }

        [Fact]
        public void FindTemplateFiles_FileExtensionTest()
        {
            var templates = Package.FindTemplateFiles(TemplatePath);

            foreach (var template in templates)
            {
                Assert.EndsWith(Package.TEMPLATE_FILE_EXTENSION, template);
            }
        }

        [Fact]
        public void LoadFromFile_ConfigurationNotNullTest()
        {
            var package = Package.LoadFromFile(StandardTemplatePath);

            Assert.NotNull(package.Configuration);
        }

        [Fact]
        public void LoadFromFile_ConfigurationNameTest()
        {
            var package = Package.LoadFromFile(StandardTemplatePath);

            var expected = Path.GetFileName(StandardTemplatePath.Remove(StandardTemplatePath.Length - Package.TEMPLATE_FILE_EXTENSION.Length));
            var actual = package.Configuration.Name;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LoadFromFile_TemplateUriPathTest()
        {
            var package = Package.LoadFromFile(StandardTemplatePath);

            Assert.NotNull(package.Configuration.TemplateUri);
           
            var expected = Path.GetFullPath(StandardTemplatePath);
            var actual = (new Uri(package.Configuration.TemplateUri!)).LocalPath;

            Assert.Equal(expected, actual);
        }
    }
}