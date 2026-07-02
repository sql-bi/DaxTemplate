namespace Dax.Template.Tests
{
    using Dax.Template.Tests.Infrastructure;
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Offline regression tests: build a synthetic in-memory model, run the real <see cref="Engine.ApplyTemplates"/>
    /// dispatch, and snapshot the resulting metadata. These guard the existing date-table and time-intelligence
    /// measure output so that adding new template entities (calendars, calculation groups, UDFs) cannot silently
    /// change current behavior. A parallel opt-in live-server test exercises the same path against a real engine.
    /// </summary>
    public class ApplyTemplatesGoldenTests
    {
        private const string StandardTemplatePath = @".\_data\Templates\Config-01 - Standard.template.json";

        [Fact]
        public void StandardConfig_OfflineApply_ProducesExpectedTables()
        {
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(StandardTemplatePath));

            engine.ApplyTemplates(database.Model);

            var tableNames = database.Model.Tables.Select(t => t.Name).ToArray();
            Assert.Contains("Date", tableNames);
            Assert.Contains("DateAutoTemplate", tableNames);
            Assert.Contains("Holidays", tableNames);
            Assert.Contains("HolidaysDefinition", tableNames);

            // time-intelligence measures are generated against the Sales target measures
            var sales = database.Model.Tables.Find("Sales");
            Assert.NotNull(sales);
            Assert.True(sales!.Measures.Count > 4, "expected time-intelligence measures to be added to Sales");
        }

        [Fact]
        public void StandardConfig_OfflineApply_MatchesSnapshot()
        {
            var database = OfflineModelFixture.Build();
            var engine = new Engine(Package.LoadFromFile(StandardTemplatePath));

            engine.ApplyTemplates(database.Model);

            var actual = GoldenFile.SerializeNormalized(database);
            GoldenFile.AssertMatchesSnapshot(actual, "Config-01 - Standard");
        }

        /// <summary>
        /// Opt-in: applies the Standard template against a real connected model (env-configured) and verifies the
        /// engine produces model changes without persisting them. Skipped unless live-server env vars are set.
        /// </summary>
        [LiveServerFact]
        public void StandardConfig_LiveServerApply_ProducesModelChanges()
        {
            var serverConn = Environment.GetEnvironmentVariable(LiveServerFactAttribute.ServerEnvVar)!;
            var databaseName = Environment.GetEnvironmentVariable(LiveServerFactAttribute.DatabaseEnvVar)!;

            using var server = new Server();
            server.Connect(serverConn);
            try
            {
                var database = server.Databases[databaseName];
                var engine = new Engine(Package.LoadFromFile(StandardTemplatePath));

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