namespace Dax.Template.Tests
{
    using Dax.Template.Exceptions;
    using Dax.Template.Measures;
    using Dax.Template.Tests.Infrastructure;
    using System.IO;
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// Characterization tests pinning the CURRENT failure modes of <see cref="Package"/> that are not
    /// already covered by the happy-path tests in <see cref="PackageTests"/>: a missing file, malformed
    /// JSON, a packaged "Config" element of the wrong JSON kind, a referenced sub-template file that does
    /// not exist, and <see cref="Package.ReadDefinition{T}"/> given malformed or literally-null definition
    /// content. These pin the ACTUAL exception types thrown today (verified against Package.cs), not
    /// aspirational ones -- several are raw BCL exceptions (FileNotFoundException, JsonException) rather
    /// than a Dax.Template-specific exception type.
    ///
    /// Reachability: <see cref="Package.ReadDefinition{T}"/> is `internal` and made visible to this
    /// assembly via [InternalsVisibleTo("Dax.Template.Tests")] declared in src/Dax.Template/AssemblyInfo.cs.
    /// </summary>
    public class PackageInvalidConfigCharacterizationTests
    {
        private const string TemplatesDirectory = @".\_data\Templates";
        private const string StandardTemplatePath = TemplatesDirectory + @"\Config-01 - Standard.template.json";

        [Fact]
        public void LoadFromFile_FileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Arrange
            var missingPath = $@"{TemplatesDirectory}\DoesNotExist.template.json";

            // Act & Assert: File.ReadAllText is the first thing LoadFromFile does with the path.
            Assert.Throws<FileNotFoundException>(() => Package.LoadFromFile(missingPath));
        }

        [Fact]
        public void LoadFromFile_MalformedJson_ThrowsJsonException()
        {
            // Arrange
            var malformedPath = $@"{TemplatesDirectory}\InvalidConfig-01 - MalformedTemplateJson.template.json";

            // Act & Assert: JsonDocument.Parse(packageText) throws before any Dax.Template-specific
            // validation runs. The actual runtime type is the internal System.Text.Json.JsonReaderException
            // (a JsonException subclass that cannot be referenced by name outside its assembly), so this
            // uses ThrowsAny to match by base type rather than exact type.
            Assert.ThrowsAny<JsonException>(() => Package.LoadFromFile(malformedPath));
        }

        [Fact]
        public void LoadFromFile_PackagedConfigElementIsNotAnObject_ThrowsTemplateConfigurationException()
        {
            // Arrange: root document has a top-level "Config" property (the packaged-template shape) but
            // its value is a JSON string, not an object.
            var path = $@"{TemplatesDirectory}\InvalidConfig-02 - PackagedConfigNotObject.template.json";

            // Act & Assert
            Assert.Throws<TemplateConfigurationException>(() => Package.LoadFromFile(path));
        }

        [Fact]
        public void ApplyTemplates_ReferencedSubTemplateFileMissing_ThrowsFileNotFoundException()
        {
            // Arrange: the config's MeasuresTemplate entry references a Template file that isn't on disk.
            // The Undefined-Template guard in Engine.ApplyTemplates only checks for null/whitespace, so a
            // non-empty-but-nonexistent filename reaches Package.ReadDefinition<T> and fails there.
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile($@"{TemplatesDirectory}\InvalidConfig-03 - MissingSubTemplate.template.json"));

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => engine.ApplyTemplates(database.Model));
        }

        [Fact]
        public void ReadDefinition_ReferencedFileMissing_ThrowsFileNotFoundException()
        {
            // Arrange: same failure mode as above, exercised directly at the Package/ReadDefinition level
            // rather than through the full Engine dispatch.
            var package = Package.LoadFromFile(StandardTemplatePath);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => package.ReadDefinition<MeasuresTemplateDefinition>("DoesNotExist.json"));
        }

        [Fact]
        public void ReadDefinition_MalformedJsonContent_ThrowsJsonException()
        {
            // Arrange
            var package = Package.LoadFromFile(StandardTemplatePath);

            // Act & Assert: JsonSerializer.Deserialize<T> throws before the null-result guard runs (see the
            // ThrowsAny note on LoadFromFile_MalformedJson_ThrowsJsonException above for why this isn't an
            // exact-type Assert.Throws<JsonException>).
            Assert.ThrowsAny<JsonException>(() => package.ReadDefinition<MeasuresTemplateDefinition>("InvalidConfig-04 - MalformedDefinition.json"));
        }

        [Fact]
        public void ReadDefinition_ContentDeserializesToNull_ThrowsTemplateUnexpectedException()
        {
            // Arrange: the file's entire content is the JSON literal `null`, which is valid JSON and
            // deserializes successfully to a null reference -- ReadDefinition's explicit null-check then
            // throws its own exception type instead of returning null.
            var package = Package.LoadFromFile(StandardTemplatePath);

            // Act & Assert
            Assert.Throws<TemplateUnexpectedException>(() => package.ReadDefinition<MeasuresTemplateDefinition>("InvalidConfig-05 - NullDefinition.json"));
        }
    }
}