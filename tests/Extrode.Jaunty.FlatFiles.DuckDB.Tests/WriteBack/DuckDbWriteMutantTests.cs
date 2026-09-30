using DuckDB.NET.Data;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Unit;
using Extrode.Jaunty.Interceptors;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

[Collection(GlobalInterceptorStateCollection.Name)]
public sealed class DuckDbWriteMutantTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_write_mutants_{Guid.NewGuid():N}");
    private readonly string _csv;
    private readonly List<string> _logged = [];

    public DuckDbWriteMutantTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csv = Path.Combine(_dataDir, "inventory.csv");
        File.WriteAllText(_csv, "ItemId,ItemName,Category,StockQuantity,UnitPrice,InStock\n1,Widget,Tools,5,2.5,true\n");

        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = (sql, _) => _logged.Add(sql);
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    private sealed class DeferringInterceptor(int deferred) : ICommandInterceptor
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        private ValueTask Step() =>
            Interlocked.Increment(ref _calls) - 1 == deferred ? new ValueTask(Task.Delay(20)) : default;

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken) => Step();

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken) => Step();

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    private DuckDb NewDb()
    {
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(_csv);
        return new DuckDb(options);
    }

    private static IEnumerable<InventoryItem> Batch(params InventoryItem[] items) => items;

    private static InventoryItem Item(int id) => new() { ItemId = id, ItemName = $"Item {id}", Category = "Tools", StockQuantity = id, UnitPrice = id, InStock = true };

    private int RunWithOneInterceptorCallDeferred(Func<DuckDb, Task> operation, int deferred)
    {
        using DuckDb db = NewDb();
        var interceptor = new DeferringInterceptor(deferred);
        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(interceptor);
        var context = new CountingSynchronizationContext();

        SynchronizationContext? previous = SynchronizationContext.Current;
        Task running;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            running = operation(db);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        running.GetAwaiter().GetResult();
        Assert.True(context.Posts == 0, $"deferring interceptor call {deferred} returned the operation to the caller's context {context.Posts} time(s)");
        return interceptor.Calls;
    }

    private void AssertNeverReturnsToTheCallersContext(Func<DuckDb, Task> operation)
    {
        int calls = RunWithOneInterceptorCallDeferred(operation, deferred: -1);

        Assert.True(calls > 0, "the operation never reached the interceptor");
        for (int deferred = 0; deferred < calls; deferred++)
            RunWithOneInterceptorCallDeferred(operation, deferred);
    }

    [Fact]
    public void InsertAsyncOfOneEntity_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.InsertAsync(Item(2)).AsTask());

    [Fact]
    public void InsertAsyncOfOneEntityWithOptions_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.InsertAsync(Item(2), new CommandOptions()).AsTask());

    [Fact]
    public void InsertAsyncOfABatch_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.InsertAsync(Batch(Item(2), Item(3))).AsTask());

    [Fact]
    public void InsertAsyncOfABatchWithOptions_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.InsertAsync(Batch(Item(2), Item(3)), new CommandOptions()).AsTask());

    [Fact]
    public void SaveAsyncToAPath_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.SaveAsync<InventoryItem>(Path.Combine(_dataDir, "saved.csv")).AsTask());

    [Fact]
    public void ExportAsync_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.ExportAsync<InventoryItem>(Path.Combine(_dataDir, "exported.csv")).AsTask());

    [Fact]
    public void SaveAsyncInPlace_NeverReturnsToTheCallersContext() =>
        AssertNeverReturnsToTheCallersContext(db => db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite).AsTask());

    private const string BatchInsertSql =
        "INSERT INTO \"inventory\" (\"ItemId\", \"ItemName\", \"Category\", \"StockQuantity\", \"UnitPrice\", \"InStock\") " +
        "VALUES ($1, $2, $3, $4, $5, $6), ($7, $8, $9, $10, $11, $12)";

    [Fact]
    public void ABatchInsert_NumbersItsParametersAcrossRows()
    {
        using DuckDb db = NewDb();

        db.Insert(Batch(Item(2), Item(3)));

        Assert.Equal(BatchInsertSql, Assert.Single(_logged, sql => sql.StartsWith("INSERT", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ABatchInsertAsync_NumbersItsParametersAcrossRows()
    {
        using DuckDb db = NewDb();

        await db.InsertAsync(Batch(Item(2), Item(3)));

        Assert.Equal(BatchInsertSql, Assert.Single(_logged, sql => sql.StartsWith("INSERT", StringComparison.Ordinal)));
    }

    [Fact]
    public void ABatchInsertWithANullElement_NamesTheIndexAndTheEntity()
    {
        using DuckDb db = NewDb();

        var ex = Assert.Throws<ArgumentException>(() => db.Insert(Batch(Item(2), null!)));

        Assert.Equal("entities", ex.ParamName);
        Assert.StartsWith("The element at index 1 is null. A batch insert of InventoryItem cannot contain null entities.", ex.Message);
    }

    [Fact]
    public async Task ABatchInsertAsyncWithANullElement_NamesTheIndexAndTheEntity()
    {
        using DuckDb db = NewDb();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => db.InsertAsync(Batch(Item(2), null!)).AsTask());

        Assert.Equal("entities", ex.ParamName);
        Assert.StartsWith("The element at index 1 is null. A batch insert of InventoryItem cannot contain null entities.", ex.Message);
    }

    [Fact]
    public void ExportOfANullPath_IsRejectedByName()
    {
        using DuckDb db = NewDb();

        var ex = Assert.Throws<ArgumentNullException>(() => db.Export<InventoryItem>(null!));

        Assert.Equal("outputPath", ex.ParamName);
    }

    [Fact]
    public async Task ExportAsyncOfANullPath_IsRejectedByName()
    {
        using DuckDb db = NewDb();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => db.ExportAsync<InventoryItem>(null!).AsTask());

        Assert.Equal("outputPath", ex.ParamName);
    }

    [Fact]
    public async Task SaveAsyncOfANullPath_IsRejectedByName()
    {
        using DuckDb db = NewDb();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => db.SaveAsync<InventoryItem>((string)null!).AsTask());

        Assert.Equal("outputPath", ex.ParamName);
    }

    private void ReplaceTheSourceFileWithADirectory()
    {
        File.Delete(_csv);
        Directory.CreateDirectory(_csv);
        File.WriteAllText(Path.Combine(_csv, "keep"), "x");
    }

    [Fact]
    public void AnInPlaceSaveWhoseMoveFails_RemovesItsTempFileAndRethrows()
    {
        using DuckDb db = NewDb();
        db.Insert(Item(2));
        ReplaceTheSourceFileWithADirectory();

        Assert.ThrowsAny<Exception>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Empty(Directory.GetFiles(_dataDir, ".*.tmp.*"));
    }

    [Fact]
    public async Task AnInPlaceSaveAsyncWhoseMoveFails_RemovesItsTempFileAndRethrows()
    {
        using DuckDb db = NewDb();
        await db.InsertAsync(Item(2));
        ReplaceTheSourceFileWithADirectory();

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite).AsTask());

        Assert.Empty(Directory.GetFiles(_dataDir, ".*.tmp.*"));
    }
}
