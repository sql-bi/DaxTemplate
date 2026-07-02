namespace Dax.Template.Tests
{
    using Dax.Template.Exceptions;
    using Dax.Template.Measures;
    using Dax.Template.Tables;
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using System.Collections.Generic;
    using Xunit;

    /// <summary>
    /// Characterization tests exercising <see cref="MeasureTemplateBase"/> branches not reached by the
    /// Standard golden config or <see cref="MeasuresTemplateWrappingCharacterizationTests"/>: the
    /// <c>@_T-...@</c> / <c>@_CL-...@</c> / <c>@_CT-...@</c> placeholder entities (only <c>@_C-...@</c> is
    /// used by the shipped Config-01 template), their MultipleMatches/AttributeNotFound failure paths, the
    /// unknown-entity-code default case, the <c>@@GETDEFAULTVARIABLE</c> / <c>@@GETYEARENDFROMFIRSTMONTHVARIABLE</c>
    /// macros (unused by any shipped template), the polymorphic <c>Expression</c> override inherited from
    /// <see cref="Model.Measure"/>, and the "measure already exists in a different table" clone-and-move
    /// branch of <see cref="MeasureTemplateBase.ApplyTemplate"/>. These construct <see cref="MeasureTemplateBase"/>
    /// directly (its public ctor, settable properties, and <see cref="MeasureTemplateBase.GetDaxExpression(Model, string?)"/>
    /// / <see cref="MeasureTemplateBase.ApplyTemplate"/>) against the synthetic <see cref="OfflineModelFixture"/>,
    /// bypassing <see cref="MeasuresTemplate.ApplyTemplate"/> orchestration entirely.
    /// </summary>
    public class MeasureTemplateBaseCharacterizationTests
    {
        private static MeasuresTemplate BuildOwnerTemplate()
        {
            var config = new TemplateConfiguration
            {
                DefaultVariables = new Dictionary<string, string>(),
                OnlyTablesColumns = Array.Empty<string>(),
                ExceptTablesColumns = Array.Empty<string>(),
            };
            var definition = new MeasuresTemplateDefinition
            {
                TargetTable = new Dictionary<string, string> { ["Name"] = "Sales" },
            };
            return new MeasuresTemplate(config, definition, new Dictionary<string, object>());
        }

        private static MeasureTemplateBase BuildInstance(string? templateExpression, Dictionary<string, string>? defaultVariables = null)
        {
            return new MeasureTemplateBase(BuildOwnerTemplate())
            {
                Name = "Test Measure",
                TemplateExpression = templateExpression,
                DefaultVariables = defaultVariables,
            };
        }

        [Fact]
        public void Expression_ReferenceMeasureSet_DelegatesToGetDaxExpression()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var referenceMeasure = database.Model.Tables.Find("Sales")!.Measures.Find("Sales Amount")!;
            var instance = BuildInstance("[Sales Amount] * 2");
            instance.ReferenceMeasure = referenceMeasure;

            // Act
            var expression = instance.Expression;

            // Assert: the override delegates to GetDaxExpression(ReferenceMeasure.Model, ReferenceMeasure.Name).
            Assert.Equal("[Sales Amount] * 2", expression);
        }

        [Fact]
        public void Expression_ReferenceMeasureNotSet_ThrowsTemplateException()
        {
            // Arrange
            var instance = BuildInstance("[Sales Amount] * 2");

            // Act & Assert
            Assert.Throws<TemplateException>(() => instance.Expression);
        }

        [Fact]
        public void GetDaxExpression_SingleArgumentOverload_UsesNullOriginalMeasureName()
        {
            // Arrange: the single-argument convenience overload forwards originalMeasureName: null, so an
            // expression without any @@GETMEASURE() macro round-trips unchanged.
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("[Sales Amount] * 2");

            // Act
            var result = instance.GetDaxExpression(database.Model);

            // Assert
            Assert.Equal("[Sales Amount] * 2", result);
        }

        [Fact]
        public void GetDaxExpression_TemplateExpressionNotSet_ThrowsTemplateException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance(templateExpression: null);

            // Act & Assert
            Assert.Throws<TemplateException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_SingleTablePlaceholder_ResolvesToQuotedTableName()
        {
            // Arrange: the @_T-...@ entity (ENTITY_SINGLE_TABLE) is never used by the shipped Config-01
            // template, which only exercises @_C-...@ (single column).
            var database = OfflineModelFixture.Build();
            database.Model.Tables.Find("Sales")!.Annotations.Add(new Annotation { Name = "TableAttr", Value = "Y" });
            var instance = BuildInstance("@_T-TableAttr-Y_@[Measure]");

            // Act
            var result = instance.GetDaxExpression(database.Model, null);

            // Assert
            Assert.Equal("'Sales'[Measure]", result);
        }

        [Fact]
        public void GetDaxExpression_SingleTablePlaceholderMatchesMultipleTables_ThrowsInvalidMacroReferenceException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            foreach (var tableName in new[] { "Sales", "Orders" })
            {
                database.Model.Tables.Find(tableName)!.Annotations.Add(new Annotation { Name = "TableAttr", Value = "Y" });
            }
            var instance = BuildInstance("@_T-TableAttr-Y_@[Measure]");

            // Act & Assert
            Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_SingleTablePlaceholderNoMatch_ThrowsInvalidMacroReferenceException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@_T-TableAttr-Missing_@[Measure]");

            // Act & Assert
            Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_ColumnsListPlaceholder_ResolvesToCommaSeparatedColumnList()
        {
            // Arrange: the @_CL-...@ entity (ENTITY_COLUMNS_LIST) is never used by the shipped template.
            var database = OfflineModelFixture.Build();
            database.Model.Tables.Find("Sales")!.Columns.Find("Order Date")!.Annotations.Add(new Annotation { Name = "ColAttr", Value = "Z" });
            database.Model.Tables.Find("Orders")!.Columns.Find("Delivery Date")!.Annotations.Add(new Annotation { Name = "ColAttr", Value = "Z" });
            var instance = BuildInstance("{ @_CL-ColAttr-Z_@ }");

            // Act
            var result = instance.GetDaxExpression(database.Model, null);

            // Assert
            Assert.Equal("{ 'Sales'[Order Date], 'Orders'[Delivery Date] }", result);
        }

        [Fact]
        public void GetDaxExpression_TablesListPlaceholder_ResolvesToCommaSeparatedTableList()
        {
            // Arrange: the @_CT-...@ entity (ENTITY_COLUMNS_TABLE) is never used by the shipped template.
            var database = OfflineModelFixture.Build();
            database.Model.Tables.Find("Sales")!.Annotations.Add(new Annotation { Name = "TabAttr", Value = "W" });
            database.Model.Tables.Find("Orders")!.Annotations.Add(new Annotation { Name = "TabAttr", Value = "W" });
            var instance = BuildInstance("{ @_CT-TabAttr-W_@ }");

            // Act
            var result = instance.GetDaxExpression(database.Model, null);

            // Assert
            Assert.Equal("{ 'Sales', 'Orders' }", result);
        }

        [Fact]
        public void GetDaxExpression_TablesListPlaceholderNoMatches_ThrowsInvalidMacroReferenceException()
        {
            // Arrange: unlike FindSingleTable/FindSingleColumn, FindTablesList never throws on an empty
            // match set -- it returns an empty joined string, which GetDaxExpression's blank-result guard
            // then treats as an unresolved placeholder.
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("{ @_CT-TabAttr-Missing_@ }");

            // Act & Assert
            Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_SingleColumnPlaceholderNoMatch_ThrowsInvalidMacroReferenceException()
        {
            // Arrange: the @_C-...@ success path is covered by the Standard golden; this pins its
            // AttributeNotFoundException failure path instead.
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@_C-ColAttr-Missing_@");

            // Act & Assert
            Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_SingleColumnPlaceholderMatchesMultipleColumns_ThrowsInvalidMacroReferenceException()
        {
            // Arrange: pins the @_C-...@ MultipleMatchesException failure path.
            var database = OfflineModelFixture.Build();
            database.Model.Tables.Find("Sales")!.Columns.Find("Order Date")!.Annotations.Add(new Annotation { Name = "ColAttr", Value = "Z" });
            database.Model.Tables.Find("Orders")!.Columns.Find("Delivery Date")!.Annotations.Add(new Annotation { Name = "ColAttr", Value = "Z" });
            var instance = BuildInstance("@_C-ColAttr-Z_@");

            // Act & Assert
            Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_UnknownEntityCode_ThrowsInvalidMacroReferenceException()
        {
            // Arrange: pins the switch expression's default arm for an entity code that isn't C/T/CL/CT.
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@_X-Whatever-Value_@");

            // Act & Assert
            Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_GetMeasureMacroWithoutOriginalMeasureName_ThrowsInvalidMacroReferenceException()
        {
            // Arrange: originalMeasureName is null when a single-instance measure (IsSingleInstance = true)
            // is applied without a reference measure -- see MeasuresTemplate.ApplyTemplate. An expression
            // that still contains @@GETMEASURE() in that situation cannot be resolved.
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@@GETMEASURE()");

            // Act & Assert
            var ex = Assert.Throws<InvalidMacroReferenceException>(() => instance.GetDaxExpression(database.Model, null));
            Assert.Contains("IsSingleInstance", ex.Message);
        }

        [Fact]
        public void GetDaxExpression_DefaultVariablePlaceholder_ReplacesWithConfiguredValue()
        {
            // Arrange: @@GETDEFAULTVARIABLE(...) is unused by any shipped template.
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@@GETDEFAULTVARIABLE( MySetting )", new Dictionary<string, string> { ["MySetting"] = "42" });

            // Act
            var result = instance.GetDaxExpression(database.Model, null);

            // Assert
            Assert.Equal("42", result);
        }

        [Fact]
        public void GetDaxExpression_DefaultVariablePlaceholderNotConfigured_ThrowsTemplateException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@@GETDEFAULTVARIABLE( MissingSetting )", new Dictionary<string, string>());

            // Act & Assert
            Assert.Throws<TemplateException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Theory]
        [InlineData("1", "\"12-31\"")]
        [InlineData("2", "\"1-31\"")]
        [InlineData("3", "\"2-28\"")]
        [InlineData("4", "\"3-31\"")]
        [InlineData("5", "\"4-30\"")]
        [InlineData("6", "\"5-31\"")]
        [InlineData("7", "\"6-30\"")]
        [InlineData("8", "\"7-31\"")]
        [InlineData("9", "\"8-31\"")]
        [InlineData("10", "\"9-30\"")]
        [InlineData("11", "\"10-31\"")]
        [InlineData("12", "\"11-30\"")]
        public void GetDaxExpression_YearEndFromFirstMonthVariablePlaceholder_ReturnsFiscalYearEndForFirstMonth(string firstMonth, string expectedYearEnd)
        {
            // Arrange: @@GETYEARENDFROMFIRSTMONTHVARIABLE(...) is unused by any shipped template; this pins
            // the fiscal-year-end lookup for every valid first-fiscal-month value (1-12).
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@@GETYEARENDFROMFIRSTMONTHVARIABLE( FirstMonth )", new Dictionary<string, string> { ["FirstMonth"] = firstMonth });

            // Act
            var result = instance.GetDaxExpression(database.Model, null);

            // Assert
            Assert.Equal(expectedYearEnd, result);
        }

        [Fact]
        public void GetDaxExpression_YearEndFromFirstMonthVariableOutOfRange_ThrowsTemplateException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@@GETYEARENDFROMFIRSTMONTHVARIABLE( FirstMonth )", new Dictionary<string, string> { ["FirstMonth"] = "13" });

            // Act & Assert
            Assert.Throws<TemplateException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void GetDaxExpression_YearEndFromFirstMonthVariableNotNumeric_ThrowsTemplateException()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var instance = BuildInstance("@@GETYEARENDFROMFIRSTMONTHVARIABLE( FirstMonth )", new Dictionary<string, string> { ["FirstMonth"] = "abc" });

            // Act & Assert
            Assert.Throws<TemplateException>(() => instance.GetDaxExpression(database.Model, null));
        }

        [Fact]
        public void ApplyTemplate_MeasureExistsInDifferentTable_ClonesMeasureIntoTargetTable()
        {
            // Arrange: applying a measure template whose Name already exists as a measure in a different
            // table (e.g. TableSingleInstanceMeasures changed between runs) clones-and-moves it rather than
            // leaving a stale copy behind.
            var database = OfflineModelFixture.Build();
            var sales = database.Model.Tables.Find("Sales")!;
            var orders = database.Model.Tables.Find("Orders")!;
            var owner = BuildOwnerTemplate();
            var firstRun = new MeasureTemplateBase(owner) { Name = "Moved Measure", TemplateExpression = "1" };
            firstRun.ApplyTemplate(database.Model, sales);

            var secondRun = new MeasureTemplateBase(owner) { Name = "Moved Measure", TemplateExpression = "2" };

            // Act
            secondRun.ApplyTemplate(database.Model, orders);

            // Assert
            Assert.Null(sales.Measures.Find("Moved Measure"));
            var moved = orders.Measures.Find("Moved Measure");
            Assert.NotNull(moved);
            Assert.Equal("2", moved!.Expression);
        }
    }
}