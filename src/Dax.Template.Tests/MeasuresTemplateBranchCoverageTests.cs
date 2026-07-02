namespace Dax.Template.Tests
{
    using Dax.Template.Constants;
    using Dax.Template.Enums;
    using Dax.Template.Exceptions;
    using Dax.Template.Interfaces;
    using Dax.Template.Measures;
    using Dax.Template.Tables;
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using System.Collections.Generic;
    using Xunit;

    /// <summary>
    /// Characterization tests for <see cref="MeasuresTemplate"/> branches not reached by the Standard golden
    /// config or <see cref="MeasuresTemplateWrappingCharacterizationTests"/>: <see cref="MeasuresTemplate.ReplaceMacros"/>'s
    /// <c>AutoScan.Disabled</c> fallback and its <c>@@GETMINDATE()</c> arm (the golden config only exercises
    /// <c>@@GETMAXDATE()</c> with scanning enabled), the disabled-template cleanup branch of
    /// <see cref="MeasuresTemplate.ApplyTemplate"/> (<c>isEnabled: false</c> after a prior enabled run), and
    /// every failure branch of <c>GetTargetTable</c> (attribute-based table resolution: no match, multiple
    /// matches, a second attribute resolving a different table, and an entirely empty <c>TargetTable</c>
    /// configuration). These drive the public <see cref="MeasuresTemplate.ApplyTemplate"/> entry point against
    /// the synthetic <see cref="OfflineModelFixture"/>, matching the style of
    /// <see cref="MeasuresTemplateWrappingCharacterizationTests"/>.
    /// </summary>
    public class MeasuresTemplateBranchCoverageTests
    {
        [Fact]
        public void ApplyTemplate_AutoScanDisabledWithMinAndMaxDateMacros_ReplacesBothWithToday()
        {
            // Arrange: when AutoScan is explicitly Disabled, ReplaceMacros falls back to TODAY() for both
            // @@GETMINDATE() and @@GETMAXDATE() rather than attempting to scan the model for a date range.
            var database = OfflineModelFixture.Build();
            var config = new TemplateConfiguration
            {
                AutoScan = AutoScan.Disabled,
                OnlyTablesColumns = Array.Empty<string>(),
                ExceptTablesColumns = Array.Empty<string>(),
                DefaultVariables = new Dictionary<string, string>(),
            };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Range" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate
                    {
                        Name = "DateRange",
                        IsSingleInstance = true,
                        Expression = "IF ( @@GETMINDATE() = @@GETMAXDATE(), 1, 0 )"
                    }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act
            template.ApplyTemplate(database.Model, isEnabled: true);

            // Assert
            var sales = database.Model.Tables.Find("Sales")!;
            var generated = sales.Measures.Find("DateRange")!;
            Assert.Equal("IF ( TODAY() = TODAY(), 1, 0 )", generated.Expression);
        }

        [Fact]
        public void ApplyTemplate_ScanColumnsAvailableWithGetMinDateMacro_GeneratesMinxAggregationAcrossScanColumns()
        {
            // Arrange: AutoScan.SelectedTablesColumns with an empty OnlyTablesColumns list scans every
            // DateTime column in the model (see Extensions.GetScanColumns' `scanAll` fallback) -- both
            // Sales[Order Date] and Orders[Delivery Date] in OfflineModelFixture. The Standard golden only
            // ever exercises @@GETMAXDATE(); this pins the parallel @@GETMINDATE() arm.
            var database = OfflineModelFixture.Build();
            var config = new TemplateConfiguration
            {
                AutoScan = AutoScan.SelectedTablesColumns,
                OnlyTablesColumns = Array.Empty<string>(),
                ExceptTablesColumns = Array.Empty<string>(),
                DefaultVariables = new Dictionary<string, string>(),
            };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Range" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate
                    {
                        Name = "MinDate",
                        IsSingleInstance = true,
                        Expression = "@@GETMINDATE()"
                    }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act
            template.ApplyTemplate(database.Model, isEnabled: true);

            // Assert
            var sales = database.Model.Tables.Find("Sales")!;
            var generated = sales.Measures.Find("MinDate")!;
            Assert.Contains("MINX (", generated.Expression);
            Assert.Contains("MIN ( 'Sales'[Order Date] )", generated.Expression);
            Assert.Contains("MIN ( 'Orders'[Delivery Date] )", generated.Expression);
        }

        [Fact]
        public void ApplyTemplate_DisabledAfterPreviousEnabledRun_RemovesPreviouslyGeneratedMeasures()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var config = new TemplateConfiguration
            {
                AutoNaming = AutoNaming.Suffix,
                AutoNamingSeparator = " ",
                TargetMeasures = new[] { new IMeasureTemplateConfig.TargetMeasure { Name = "Sales Amount" } },
                DefaultVariables = new Dictionary<string, string>(),
            };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Rounded", Expression = "ROUND ( @@GETMEASURE(), 0 )" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());
            template.ApplyTemplate(database.Model, isEnabled: true);
            var sales = database.Model.Tables.Find("Sales")!;
            Assert.NotNull(sales.Measures.Find("Sales Amount Rounded"));

            // Act: re-apply the same template entry as disabled, as Engine does when a config entry's
            // IsEnabled flag flips (or the entry is removed) between runs.
            template.ApplyTemplate(database.Model, isEnabled: false);

            // Assert
            Assert.Null(sales.Measures.Find("Sales Amount Rounded"));
        }

        [Fact]
        public void ApplyTemplate_TargetTableAttributeHasNoMatch_ThrowsTemplateException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var config = new TemplateConfiguration { DefaultVariables = new Dictionary<string, string>() };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["MissingAttr"] = "X" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Dummy", IsSingleInstance = true, Expression = "1" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act & Assert
            var ex = Assert.Throws<TemplateException>(() => template.ApplyTemplate(database.Model, isEnabled: true));
            Assert.Contains("No target tables found", ex.Message);
        }

        [Fact]
        public void ApplyTemplate_TargetTableAttributeMatchesMultipleTables_ThrowsTemplateException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            foreach (var tableName in new[] { "Sales", "Orders" })
            {
                database.Model.Tables.Find(tableName)!.Annotations.Add(new Annotation { Name = "DupAttr", Value = "X" });
            }
            var config = new TemplateConfiguration { DefaultVariables = new Dictionary<string, string>() };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["DupAttr"] = "X" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Dummy", IsSingleInstance = true, Expression = "1" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act & Assert
            var ex = Assert.Throws<TemplateException>(() => template.ApplyTemplate(database.Model, isEnabled: true));
            Assert.Contains("Multiple tables found", ex.Message);
        }

        [Fact]
        public void ApplyTemplate_TargetTableSecondAttributeResolvesDifferentTable_ThrowsTemplateException()
        {
            // Arrange: two TargetTable attribute entries that each resolve to a single, but different, table.
            var database = OfflineModelFixture.Build();
            database.Model.Tables.Find("Sales")!.Annotations.Add(new Annotation { Name = "AttrA", Value = "1" });
            database.Model.Tables.Find("Orders")!.Annotations.Add(new Annotation { Name = "AttrB", Value = "2" });
            var config = new TemplateConfiguration { DefaultVariables = new Dictionary<string, string>() };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["AttrA"] = "1", ["AttrB"] = "2" },
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Dummy", IsSingleInstance = true, Expression = "1" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act & Assert
            var ex = Assert.Throws<TemplateException>(() => template.ApplyTemplate(database.Model, isEnabled: true));
            Assert.Contains("Additional tables found", ex.Message);
        }

        [Fact]
        public void ApplyTemplate_TargetTableConfigurationEmpty_ThrowsTemplateException()
        {
            // Arrange: no "Name" entry and no attribute entries at all -- GetTargetTable's resolution loop
            // never executes, so targetTable is never assigned.
            var database = OfflineModelFixture.Build();
            var config = new TemplateConfiguration { DefaultVariables = new Dictionary<string, string>() };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string>(),
                TemplateAnnotations = new Dictionary<string, string> { [Attributes.SqlbiTemplate] = "Wrap" },
                MeasureTemplates = new[]
                {
                    new MeasuresTemplateDefinition.MeasureTemplate { Name = "Dummy", IsSingleInstance = true, Expression = "1" }
                }
            };
            var template = new MeasuresTemplate(config, definition, new Dictionary<string, object>());

            // Act & Assert
            var ex = Assert.Throws<TemplateException>(() => template.ApplyTemplate(database.Model, isEnabled: true));
            Assert.Contains("Target tables not found", ex.Message);
        }
    }
}