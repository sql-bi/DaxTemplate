namespace Dax.Template.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// Characterization tests for <see cref="Package.SaveTo"/> (not exercised by any existing test) and the
    /// packaged-template branches of <see cref="Package.LoadFromFile"/> / <see cref="Package.ReadDefinition{T}"/>
    /// that only trigger when a file embeds its "Config" section and sub-template definitions inline, rather
    /// than referencing them as external files -- the shape every existing fixture under <c>_data/Templates</c>
    /// uses today (see <see cref="PackageTests"/> and <see cref="PackageInvalidConfigCharacterizationTests"/>).
    ///
    /// <see cref="Package.SaveTo"/> reads the sub-template/localization files referenced by
    /// <see cref="Package.Configuration"/>'s <c>Templates</c> entries from disk (relative to the loaded
    /// package's own directory) and re-packages them inline as a single JSON document. Loading the existing
    /// unpackaged "Config-01 - Standard.template.json" fixture, saving it to a temp file, and reloading that
    /// temp file exercises both the write side and the packaged-read side in one round trip.
    /// </summary>
    public class PackageSaveToCharacterizationTests : IDisposable
    {
        private const string StandardTemplatePath = @".\_data\Templates\Config-01 - Standard.template.json";
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"DaxTemplateTests-{Guid.NewGuid():N}");

        private string SavePackagedCopy()
        {
            Directory.CreateDirectory(_tempDirectory);
            var packagedPath = Path.Combine(_tempDirectory, "Standard.packaged.template.json");
            var package = Package.LoadFromFile(StandardTemplatePath);
            package.SaveTo(packagedPath);
            return packagedPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }

        [Fact]
        public void SaveTo_LoadedStandardPackage_WritesConfigSectionAndEmbeddedSubTemplates()
        {
            // Arrange
            var packagedPath = SavePackagedCopy();

            // Act
            var packageText = File.ReadAllText(packagedPath);
            using var document = JsonDocument.Parse(packageText);

            // Assert: SaveTo writes the "Config" section plus one embedded entry per non-empty
            // Template/LocalizationFiles reference declared in the config's Templates[] entries. Config-01's
            // "HolidaysTable" entry has Template: null so it contributes nothing; the other three do.
            Assert.True(document.RootElement.TryGetProperty(Package.PACKAGE_CONFIG, out var configElement));
            Assert.Equal(JsonValueKind.Object, configElement.ValueKind);

            foreach (var expectedDefinition in new[] { "HolidaysDefinition", "DateTemplate-01", "TimeIntelligence-01" })
            {
                Assert.True(document.RootElement.TryGetProperty(expectedDefinition, out var definitionElement), $"Missing embedded definition '{expectedDefinition}'");
                Assert.Equal(JsonValueKind.Object, definitionElement.ValueKind);
            }
        }

        [Fact]
        public void LoadFromFile_PackagedCopyOfStandardTemplate_ReadsConfigFromEmbeddedSection()
        {
            // Arrange
            var packagedPath = SavePackagedCopy();

            // Act: this reload hits the branch of LoadFromFile that reads the "Config" property's raw text
            // (packaged shape) rather than treating the whole file as the config (unpackaged shape, the only
            // one exercised by PackageTests / PackageInvalidConfigCharacterizationTests today).
            var reloaded = Package.LoadFromFile(packagedPath);

            // Assert
            Assert.Equal("Standard calendar with holidays and standard time intelligence", reloaded.Configuration.Description);
            Assert.NotNull(reloaded.Configuration.Templates);
            Assert.Equal(4, reloaded.Configuration.Templates!.Length);
        }

        [Fact]
        public void ReadDefinition_PackagedCopyOfStandardTemplate_ReadsEmbeddedDefinitionRatherThanExternalFile()
        {
            // Arrange
            var packagedPath = SavePackagedCopy();
            var reloaded = Package.LoadFromFile(packagedPath);

            // Act: ReadDefinition first checks whether the definition is embedded in the package's own
            // JsonDocument (the packaged shape) before falling back to an external file on disk -- the temp
            // directory here doesn't even contain a "DateTemplate-01.json" file, so the external-file
            // fallback would throw FileNotFoundException if this branch weren't taken.
            var definition = reloaded.ReadDefinition<JsonElement>("DateTemplate-01");

            // Assert
            Assert.Equal(JsonValueKind.Object, definition.ValueKind);
        }
    }
}