using Extrode.Jaunty.FlatFiles.DuckDB.Internals.Import;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

public class ImportExecutorDirectTests
{
    private sealed class StubSource : IFileSource
    {
        public string TableName => "inventory";
        public string FilePath => throw new NotSupportedException();
        public IReadOnlyList<string> FilePaths => throw new NotSupportedException();
        public string Format => throw new NotSupportedException();
        public Type EntityType => typeof(InventoryItem);
        public bool IsPromotedToTable { get; set; }
        public bool IsPreloaded { get; set; }
        public string DuckDbFormatName => throw new NotSupportedException();
        public string GenerateReadFunction(string pathExpression) => throw new NotSupportedException();
        public string? GenerateCopyToOptions() => throw new NotSupportedException();
    }

    private const string CreateInventory = @"
        CREATE TABLE ""inventory"" (
            ""ItemId"" INTEGER PRIMARY KEY NOT NULL,
            ""ItemName"" TEXT NOT NULL,
            ""Category"" TEXT NOT NULL,
            ""StockQuantity"" INTEGER NOT NULL,
            ""UnitPrice"" REAL NOT NULL,
            ""InStock"" INTEGER NOT NULL)";

    private static void Execute(ProbeSqliteConnection connection, string sql)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Inner.Open();
        using SqliteCommand cmd = connection.Inner.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static ProbeSqliteConnection SourceWithRows(CountingSynchronizationContext? context = null)
    {
        var source = new ProbeSqliteConnection(canBatch: false, context);
        Execute(source, CreateInventory);
        Execute(source, @"
            INSERT INTO ""inventory"" VALUES
                (1, 'Widget A', 'Electronics', 150, 29.99, 1),
                (2, 'Widget B', 'Electronics', 0, 49.99, 0),
                (3, 'Gadget C', 'Hardware', 75, 15.5, 1),
                (4, 'Gadget D', 'Hardware', 200, 8.25, 1),
                (5, 'Thingamajig', 'Misc', 10, 99.99, 1)");
        return source;
    }

    private static ValueTask<long> Import(ProbeSqliteConnection source, ProbeSqliteConnection target, ImportOptions options, CancellationToken cancellationToken = default) =>
        ImportExecutor.ExecuteAsync<InventoryItem>(source, new StubSource(), target, options, cancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnImportCancelledMidStream_StopsAtTheNextRow(bool canBatch)
    {
        using ProbeSqliteConnection source = SourceWithRows();
        using var target = new ProbeSqliteConnection(canBatch);
        using var cts = new CancellationTokenSource();
        var options = new ImportOptions(batchSize: 1, createTableIfMissing: true, onProgress: (_, _) => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Import(source, target, options, cts.Token));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TheImportNeverReturnsToTheCallersContext(bool canBatch, bool createTable)
    {
        var context = new CountingSynchronizationContext();
        using ProbeSqliteConnection source = SourceWithRows(context);
        using var target = new ProbeSqliteConnection(canBatch, context);
        if (!createTable)
            Execute(target, CreateInventory);

        SynchronizationContext? previous = SynchronizationContext.Current;
        Task<long> import;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            import = Import(source, target, new ImportOptions(batchSize: 2, createTableIfMissing: createTable)).AsTask();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.Equal(5, import.GetAwaiter().GetResult());
        Assert.Equal(0, context.Posts);
    }
}
