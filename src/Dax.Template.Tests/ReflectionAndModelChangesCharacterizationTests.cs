namespace Dax.Template.Tests
{
    using System;
    using Dax.Template.Extensions;
    using Dax.Template.Tests.Infrastructure;
    using Xunit;

    /// <summary>
    /// Characterization tests pinning the CURRENT behavior of <see cref="ReflectionHelper"/>
    /// (Extensions/ReflectionHelper.cs) and <see cref="Engine.GetModelChanges"/> (Engine.cs:29-62), both
    /// of which read/write object state via .NET reflection rather than typed access.
    /// </summary>
    public class ReflectionAndModelChangesCharacterizationTests
    {
        private class SampleObject
        {
            public string PublicProperty { get; set; } = "initial";
            private string PrivateProperty { get; set; } = "hidden";
        }

        private class SampleObjectDerived : SampleObject
        {
            // Intentionally declares no members: PublicProperty/PrivateProperty are inherited, exercising
            // GetPropertyInfo's BaseType walk.
        }

        [Fact]
        public void GetPropertyValue_PublicProperty_ReturnsCurrentValue()
        {
            // Arrange
            var sample = new SampleObject();

            // Act
            var value = sample.GetPropertyValue(nameof(SampleObject.PublicProperty));

            // Assert
            Assert.Equal("initial", value);
        }

        [Fact]
        public void GetPropertyValue_NonPublicProperty_ReturnsCurrentValue()
        {
            // Arrange: current behavior -- GetPropertyInfo searches Public | NonPublic instance members,
            // so private properties are readable too (this is how Engine.GetModelChanges reaches TOM's
            // internal TxManager/CurrentSavepoint/AllBodies chain).
            var sample = new SampleObject();

            // Act
            var value = sample.GetPropertyValue("PrivateProperty");

            // Assert
            Assert.Equal("hidden", value);
        }

        [Fact]
        public void GetPropertyValue_PropertyDeclaredOnBaseType_IsFoundThroughInheritanceWalk()
        {
            // Arrange: current behavior -- GetPropertyInfo walks up BaseType when the property isn't
            // declared directly on the runtime type.
            var sample = new SampleObjectDerived();

            // Act
            var value = sample.GetPropertyValue(nameof(SampleObject.PublicProperty));

            // Assert
            Assert.Equal("initial", value);
        }

        [Fact]
        public void GetPropertyValue_UnknownProperty_ThrowsArgumentOutOfRangeExceptionByDefault()
        {
            // Arrange
            var sample = new SampleObject();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sample.GetPropertyValue("DoesNotExist"));
        }

        [Fact]
        public void GetPropertyValue_UnknownPropertyWithErrorIfNotFoundFalse_ReturnsNull()
        {
            // Arrange
            var sample = new SampleObject();

            // Act
            var value = sample.GetPropertyValue("DoesNotExist", errorIfNotFound: false);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public void GetPropertyValue_NullObject_ThrowsArgumentNullException()
        {
            // Arrange
            object? sample = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sample!.GetPropertyValue("Whatever"));
        }

        [Fact]
        public void SetPropertyValue_PublicProperty_RoundTripsThroughGetPropertyValue()
        {
            // Arrange
            var sample = new SampleObject();

            // Act
            sample.SetPropertyValue(nameof(SampleObject.PublicProperty), "changed");

            // Assert
            Assert.Equal("changed", sample.PublicProperty);
            Assert.Equal("changed", sample.GetPropertyValue(nameof(SampleObject.PublicProperty)));
        }

        [Fact]
        public void SetPropertyValue_UnknownProperty_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var sample = new SampleObject();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sample.SetPropertyValue("DoesNotExist", "value"));
        }

        [Fact]
        public void GetModelChanges_NoLocalChanges_ReturnsEmptyModelChanges()
        {
            // Arrange: a freshly-built offline model straight out of the fixture builder.
            var database = OfflineModelFixture.Build();

            // Act
            var changes = Engine.GetModelChanges(database.Model);

            // Assert
            Assert.NotNull(changes);
            Assert.Empty(changes.ModifiedObjects);
            Assert.Empty(changes.RemovedObjects);
        }

        [Fact]
        public void GetModelChanges_AfterOfflineApply_StillReturnsEmptyBecauseModelIsDisconnected()
        {
            // Arrange
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(@".\_data\Templates\Config-01 - Standard.template.json"));

            // Act
            engine.ApplyTemplates(database.Model);
            var changes = Engine.GetModelChanges(database.Model);

            // Assert: current behavior/quirk -- GetModelChanges only inspects TOM's internal transaction
            // log (TxManager -> CurrentSavepoint -> AllBodies) when model.HasLocalChanges is true, and a
            // disconnected/offline model (as built by OfflineModelFixture, never attached to a Server)
            // never flips HasLocalChanges to true even though the apply visibly added tables ("Date",
            // "Holidays", ...) and measures to the in-memory model (see ApplyTemplatesGoldenTests). So
            // GetModelChanges silently returns an empty ModelChanges here; it is only meaningful against
            // a connected model (see the opt-in StandardConfig_LiveServerApply_ProducesModelChanges test).
            Assert.NotNull(changes);
            Assert.Empty(changes.ModifiedObjects);
            Assert.Empty(changes.RemovedObjects);

            // Sanity check: the apply really did add tables/measures to the model itself.
            Assert.NotNull(database.Model.Tables.Find("Date"));
        }
    }
}
