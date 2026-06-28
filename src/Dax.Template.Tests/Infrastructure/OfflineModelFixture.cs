namespace Dax.Template.Tests.Infrastructure
{
    using Microsoft.AnalysisServices.Tabular;

    /// <summary>
    /// Builds a small, synthetic in-memory tabular model used to exercise <see cref="Engine.ApplyTemplates"/>
    /// fully offline (no server connection). The model intentionally contains the shapes the Standard
    /// template config expects: date columns to auto-scan and target measures to wrap with time intelligence.
    /// </summary>
    public static class OfflineModelFixture
    {
        public const int CompatibilityLevel = 1600;

        /// <summary>
        /// Creates a disconnected <see cref="Database"/> with a Sales fact (date column + target measures)
        /// and an Orders table (second date column). Because the database is never added to a Server,
        /// the model is disconnected and the engine skips refresh requests (see Engine.RequestTableRefresh).
        /// </summary>
        public static Database Build()
        {
            var database = new Database
            {
                Name = "OfflineFixture",
                ID = "OfflineFixture",
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
