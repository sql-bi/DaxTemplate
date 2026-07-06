namespace Dax.Template.Tests.Infrastructure
{
    using Microsoft.AnalysisServices.Tabular;

    /// <summary>
    /// Builds a small, synthetic in-memory tabular model used to exercise Calendar (Phase 1) template
    /// tests fully offline (no server connection). Shaped identically to <see cref="OfflineModelFixture"/>
    /// (Sales fact + Orders table), but pinned at compatibility level 1701 instead of 1600.
    /// </summary>
    /// <remarks>
    /// The TOM <c>Calendar</c> object model requires compatibility level 1701 or higher — see
    /// <see cref="Tables.Calendars.CalendarTemplate"/>'s <c>MinimumCompatibilityLevel</c>. The Standard
    /// (Config-01) golden-file tests are pinned to a byte-identical snapshot generated against
    /// <see cref="OfflineModelFixture"/> at compatibility level 1600, so that fixture is intentionally left
    /// untouched. Bumping its <c>CompatibilityLevel</c> in place (or parameterizing it) would regenerate
    /// every existing golden BIM file and mask unrelated regressions. This class duplicates the fixture body
    /// deliberately, at the cost of a small amount of duplication, to keep the two test surfaces isolated.
    /// </remarks>
    public static class CalendarOfflineModelFixture
    {
        public const int CompatibilityLevel = 1701;

        /// <summary>
        /// Creates a disconnected <see cref="Database"/> with a Sales fact (date column + target measures)
        /// and an Orders table (second date column), at compatibility level 1701. Because the database is
        /// never added to a Server, the model is disconnected and the engine skips refresh requests (see
        /// Engine.RequestTableRefresh).
        /// </summary>
        public static Database Build()
        {
            var database = new Database
            {
                Name = "CalendarOfflineFixture",
                ID = "CalendarOfflineFixture",
                CompatibilityLevel = CompatibilityLevel,
                StorageEngineUsed = Microsoft.AnalysisServices.StorageEngineUsed.TabularMetadata,
            };
            database.Model = new Model();

            var sales = new Table { Name = "Sales" };
            sales.Columns.Add(new DataColumn { Name = "Order Date", DataType = DataType.DateTime, SourceColumn = "Order Date" });
            sales.Columns.Add(new DataColumn { Name = "Quantity", DataType = DataType.Int64, SourceColumn = "Quantity" });
            sales.Partitions.Add(new Partition
            {
                Name = "Sales",
                Source = new CalculatedPartitionSource { Expression = "{ (DATE(2020,1,1), 1) }" }
            });
            sales.Measures.Add(new Measure { Name = "Sales Amount", Expression = "SUM(Sales[Quantity])" });
            sales.Measures.Add(new Measure { Name = "Total Cost", Expression = "SUM(Sales[Quantity])" });
            sales.Measures.Add(new Measure { Name = "Margin", Expression = "[Sales Amount] - [Total Cost]" });
            sales.Measures.Add(new Measure { Name = "Margin %", Expression = "DIVIDE([Margin], [Sales Amount])" });
            database.Model.Tables.Add(sales);

            var orders = new Table { Name = "Orders" };
            orders.Columns.Add(new DataColumn { Name = "Delivery Date", DataType = DataType.DateTime, SourceColumn = "Delivery Date" });
            orders.Partitions.Add(new Partition
            {
                Name = "Orders",
                Source = new CalculatedPartitionSource { Expression = "{ (DATE(2020,1,1)) }" }
            });
            database.Model.Tables.Add(orders);

            return database;
        }
    }
}