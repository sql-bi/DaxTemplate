namespace Dax.Template.Tests
{
    using Dax.Template.Constants;
    using Dax.Template.Enums;
    using Dax.Template.Interfaces;
    using Dax.Template.Measures;
    using Dax.Template.Tables;
    using Dax.Template.Tests.Infrastructure;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Characterization tests for <see cref="MeasuresTemplate"/>'s time-intelligence-style wrapping: the
    /// generated measure name (<see cref="MeasuresTemplate.GetTargetMeasureName"/>, driven by AutoNaming /
    /// AutoNamingSeparator), the templated expression's <c>@@GETMEASURE()</c> substitution
    /// (<see cref="Measures.MeasureTemplateBase.GetDaxExpression(Microsoft.AnalysisServices.Tabular.Model, string?)"/>),
    /// the <c>SQLBI_Template</c> annotation tagging applied to every generated measure, and the
    /// DisplayFolderRule macro substitution (<see cref="MeasuresTemplate.GetDisplayFolder"/>). These
    /// construct <see cref="MeasuresTemplate"/> directly against the synthetic <see cref="OfflineModelFixture"/>
    /// model, bypassing <see cref="Engine"/>/<see cref="Package"/> JSON loading entirely, so each behavior is
    /// pinned in isolation rather than through the combined Config-01 golden file.
    /// </summary>
    public class MeasuresTemplateWrappingCharacterizationTests
    {
        private static TemplateConfiguration BuildConfig(AutoNamingEnum autoNaming, string separator = " ")
        {
            return new TemplateConfiguration
            {
                AutoNaming = autoNaming,
                AutoNamingSeparator = separator,
                TargetMeasures = new[] { new IMeasureTemplateConfig.TargetMeasure { Name = "Sales Amount" } },
                DefaultVariables = new Dictionary<string, string>(),
            };
        }

        [Fact]
        public void ApplyTemplate_AutoNamingSuffix_GeneratesMeasureNamedReferenceThenTemplate()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var config = BuildConfig(AutoNamingEnum.Suffix);
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SQLBI_TEMPLATE_ATTRIBUTE] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Rounded", Expression = "ROUND ( @@GETMEASURE(), 0 )" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act
            template.ApplyTemplate(database.Model, isEnabled: true);

            // Assert: Suffix naming is "<ReferenceMeasureName><Separator><TemplateName>".
            var sales = database.Model.Tables.Find("Sales")!;
            var generated = sales.Measures.Find("Sales Amount Rounded");
            Assert.NotNull(generated);
            Assert.Equal("ROUND ( [Sales Amount], 0 )", generated!.Expression);
        }

        [Fact]
        public void ApplyTemplate_AutoNamingPrefix_GeneratesMeasureNamedTemplateThenReference()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var config = BuildConfig(AutoNamingEnum.Prefix);
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SQLBI_TEMPLATE_ATTRIBUTE] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Rounded", Expression = "ROUND ( @@GETMEASURE(), 0 )" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act
            template.ApplyTemplate(database.Model, isEnabled: true);

            // Assert: Prefix naming is "<TemplateName><Separator><ReferenceMeasureName>".
            var sales = database.Model.Tables.Find("Sales")!;
            Assert.NotNull(sales.Measures.Find("Rounded Sales Amount"));
            Assert.Null(sales.Measures.Find("Sales Amount Rounded"));
        }

        [Fact]
        public void ApplyTemplate_GeneratedMeasure_IsTaggedWithConfiguredSqlbiTemplateAnnotation()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var config = BuildConfig(AutoNamingEnum.Suffix);
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SQLBI_TEMPLATE_ATTRIBUTE] = "MyWrapper" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Rounded", Expression = "ROUND ( @@GETMEASURE(), 0 )" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act
            template.ApplyTemplate(database.Model, isEnabled: true);

            // Assert: the idempotency contract's SQLBI_Template annotation carries the configured value,
            // and is present on every measure the template generates.
            var sales = database.Model.Tables.Find("Sales")!;
            var generated = sales.Measures.Find("Sales Amount Rounded")!;
            var annotation = generated.Annotations.FirstOrDefault(a => a.Name == Attributes.SQLBI_TEMPLATE_ATTRIBUTE);
            Assert.NotNull(annotation);
            Assert.Equal("MyWrapper", annotation!.Value);
        }

        [Fact]
        public void ApplyTemplate_DisplayFolderRuleWithMacroPlaceholders_SubstitutesTemplateAndMeasureNames()
        {
            // Arrange: DisplayFolderRule is read from the `Properties` dictionary passed to MeasuresTemplate
            // (see Engine's ApplyMeasuresTemplate, which forwards templateEntry.Properties). Its placeholders
            // @_TEMPLATEFOLDER_@ / @_MEASURE_@ are substituted with the template's own DisplayFolder and the
            // target measure's Name, respectively.
            var database = OfflineModelFixture.Build();
            var config = BuildConfig(AutoNamingEnum.Suffix);
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SQLBI_TEMPLATE_ATTRIBUTE] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate
                    {
                        Name = "Rounded",
                        DisplayFolder = "TemplateFolder",
                        Expression = "ROUND ( @@GETMEASURE(), 0 )"
                    }
                }
            };
            var properties = new Dictionary<string, object>
            {
                ["DisplayFolderRule"] = @"Wrapped\@_TEMPLATEFOLDER_@\@_MEASURE_@"
            };
            var template = new MeasuresTemplate(config, definition, properties);

            // Act
            template.ApplyTemplate(database.Model, isEnabled: true);

            // Assert
            var sales = database.Model.Tables.Find("Sales")!;
            var generated = sales.Measures.Find("Sales Amount Rounded")!;
            Assert.Equal(@"Wrapped\TemplateFolder\Sales Amount", generated.DisplayFolder);
        }
    }
}