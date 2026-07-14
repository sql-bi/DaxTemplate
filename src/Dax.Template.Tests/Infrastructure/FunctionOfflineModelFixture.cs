namespace Dax.Template.Tests.Infrastructure
{
    using Microsoft.AnalysisServices.Tabular;

    /// <summary>
    /// Builds a small, synthetic in-memory tabular model used to exercise the DAX user-defined-function
    /// library (Phase 3, <see cref="Functions.FunctionLibraryTemplate"/>) tests fully offline (no server
    /// connection). Shaped identically to <see cref="OfflineModelFixture"/> (Sales fact + Orders table),
    /// but pinned at compatibility level 1702 instead of 1600.
    /// </summary>
    /// <remarks>
    /// The TOM DAX user-defined-function object model requires compatibility level 1702 or higher — see
    /// <see cref="Functions.FunctionLibraryTemplate"/>'s <c>MinimumCompatibilityLevel</c>. This is the
    /// highest compatibility floor of any phase so far (Calendar: 1701 via
    /// <see cref="CalendarOfflineModelFixture"/>; CalculationGroup: 1605 via
    /// <see cref="CalcGroupOfflineModelFixture"/>). The Standard (Config-01) golden-file tests are pinned to
    /// a byte-identical snapshot generated against <see cref="OfflineModelFixture"/> at compatibility level
    /// 1600, so that fixture is intentionally left untouched. Bumping its <c>CompatibilityLevel</c> in place
    /// (or parameterizing it) would regenerate every existing golden BIM file and mask unrelated
    /// regressions. This class duplicates the fixture body deliberately, at the cost of a small amount of
    /// duplication, to keep the test surfaces isolated — the same approach already taken by
    /// <see cref="CalendarOfflineModelFixture"/> and <see cref="CalcGroupOfflineModelFixture"/> for their
    /// respective phases.
    /// </remarks>
    public static class FunctionOfflineModelFixture
    {
        public const int CompatibilityLevel = 1702;

        /// <summary>
        /// Creates a disconnected <see cref="Database"/> with a Sales fact (date column + target measures)
        /// and an Orders table (second date column), at compatibility level 1702. Because the database is
        /// never added to a Server, the model is disconnected and the engine skips refresh requests (see
        /// Engine.RequestTableRefresh).
        /// </summary>
        public static Database Build()
        {
            var database = new Database
            {
                Name = "FunctionOfflineFixture",
                ID = "FunctionOfflineFixture",
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