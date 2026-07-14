namespace Dax.Template.Tests
{
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Offline regression tests for the Calendar (Phase 1) feature: build a synthetic compatibility-1701
    /// in-memory model via <see cref="CalendarOfflineModelFixture"/>, run the real
    /// <see cref="Engine.ApplyTemplates"/> dispatch for the <c>CalendarTemplate</c> class, and assert both
    /// the resulting TOM <see cref="Calendar"/> shape and a golden-file snapshot. These guard
    /// <see cref="Tables.Calendars.CalendarTemplate"/> and its <c>Engine.ApplyCalendarTemplate</c> dispatch
    /// so that later phases (calc groups, UDFs) cannot silently change current Calendar behavior. A
    /// parallel opt-in live-server test exercises the same path against a real engine, where compatibility
    /// level 1701 is actually enforced server-side.
    /// </summary>
    public class CalendarGoldenTests
    {
        private const string CalendarTemplatePath = @".\_data\Templates\Config-02 - Calendar.template.json";
        private const string CalendarDisabledTemplatePath = @".\_data\Templates\Config-02b - Calendar-Disabled.template.json";

        [Fact]
        public void ApplyTemplates_CalendarStandardConfig_CreatesCalendarWithTwoColumnGroups()
        {
            // Arrange
            var database = CalendarOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(CalendarTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var dateTable = database.Model.Tables.Find("Date");
            Assert.NotNull(dateTable);

            var calendar = dateTable!.Calendars.Find("Calendar");
            Assert.NotNull(calendar);
            Assert.Equal(2, calendar!.CalendarColumnGroups.Count);

            var timeUnitGroup = Assert.IsType<TimeUnitColumnAssociation>(
                Assert.Single(calendar.CalendarColumnGroups.OfType<TimeUnitColumnAssociation>()));
            Assert.Equal(TimeUnit.Year, timeUnitGroup.TimeUnit);
            Assert.NotNull(timeUnitGroup.PrimaryColumn);
            Assert.Equal("Year", timeUnitGroup.PrimaryColumn.Name);

            var timeRelatedGroup = Assert.IsType<TimeRelatedColumnGroup>(
                Assert.Single(calendar.CalendarColumnGroups.OfType<TimeRelatedColumnGroup>()));
            Assert.Contains(timeRelatedGroup.Columns, c => c.Name == "Day of Week");
        }

        [Fact]
        public void ApplyTemplates_CalendarStandardConfig_MatchesSnapshot()
        {
            // Arrange
            var database = CalendarOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(CalendarTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);

            // Assert
            var actual = GoldenFile.SerializeNormalized(database);
            GoldenFile.AssertMatchesSnapshot(actual, "Config-02 - Calendar");
        }

        [Fact]
        public void ApplyTemplates_CalendarStandardConfigAppliedTwice_ProducesIdenticalNormalizedOutput()
        {
            // Arrange
            var database = CalendarOfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(CalendarTemplatePath));

            // Act
            engine.ApplyTemplates(database.Model);
            var firstRun = GoldenFile.SerializeNormalized(database);

            engine.ApplyTemplates(database.Model);
            var secondRun = GoldenFile.SerializeNormalized(database);

            // Assert: re-running the same template against its own prior output is stable.
            Assert.Equal(firstRun, secondRun);
        }

        [Fact]
        public void ApplyTemplates_CalendarTemplateDisabledAfterEnabled_RemovesCalendarFromTargetTable()
        {
            // Arrange: apply the enabled Calendar config once, so a "Calendar" exists on "Date".
            var database = CalendarOfflineModelFixture.Build();
            var engineEnabled = new Engine(Package.LoadFromFile(CalendarTemplatePath));
            engineEnabled.ApplyTemplates(database.Model);

            var dateTable = database.Model.Tables.Find("Date");
            Assert.NotNull(dateTable);
            Assert.NotNull(dateTable!.Calendars.Find("Calendar")); // sanity check on the initial state

            // Act: re-apply a config whose CalendarTemplate entry is disabled for the same table/name.
            var engineDisabled = new Engine(Package.LoadFromFile(CalendarDisabledTemplatePath));
            engineDisabled.ApplyTemplates(database.Model);

            // Assert: the named calendar is removed, while the "Date" table itself (untouched by this
            // config, which contains no CustomDateTable entry) remains.
            Assert.NotNull(database.Model.Tables.Find("Date"));
            Assert.Null(dateTable.Calendars.Find("Calendar"));
        }

        [Fact]
        public void ApplyTemplates_CalendarTemplateDisabledWithMissingTargetTable_DoesNotThrow()
        {
            // Arrange: a fresh fixture has no "Date" table (only Sales/Orders), and the disabled config's
            // CalendarTemplate entry targets "Date" — simulating a prior entry in the same run (e.g. a
            // disabled CustomDateTable) having already removed it.
            var database = CalendarOfflineModelFixture.Build();
            Assert.Null(database.Model.Tables.Find("Date")); // sanity check on the initial state
            var engineDisabled = new Engine(Package.LoadFromFile(CalendarDisabledTemplatePath));

            // Act
            var exception = Record.Exception(() => engineDisabled.ApplyTemplates(database.Model));

            // Assert: disabled + already-missing target table is a safe no-op, matching every sibling
            // handler (HolidaysDefinitionTable, HolidaysTable, CustomDateTable).
            Assert.Null(exception);
            Assert.Null(database.Model.Tables.Find("Date"));
        }

        /// <summary>
        /// Opt-in: applies the Calendar config against a real connected model (env-configured) and verifies
        /// the engine produces model changes without persisting them. Skipped unless live-server env vars
        /// are set. This is also the only place real server-side compatibility-1701 enforcement for
        /// Calendars is exercised (see <see cref="Tables.Calendars.CalendarTemplate"/>).
        /// </summary>
        [LiveServerFact]
        public void CalendarStandardConfig_LiveServerApply_ProducesModelChanges()
        {
            var serverConn = Environment.GetEnvironmentVariable(LiveServerFactAttribute.ServerEnvVar)!;
            var databaseName = Environment.GetEnvironmentVariable(LiveServerFactAttribute.DatabaseEnvVar)!;

            using var server = new Server();
            server.Connect(serverConn);
            try
            {
                var database = server.Databases[databaseName];
                var engine = new Engine(Package.LoadFromFile(CalendarTemplatePath));

                engine.ApplyTemplates(database.Model);
                var changes = Engine.GetModelChanges(database.Model);

                Assert.NotNull(changes);
                // intentionally not calling SaveChanges: changes are discarded on disconnect.
            }
            finally
            {
                server.Disconnect();
            }
        }
    }
}