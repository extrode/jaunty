using DuckDB.NET.Data;

using Jaunty.Configuration;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;
using Jaunty.Interceptors;

namespace Jaunty.FlatFiles.DuckDB.Tests.Unit;

/// <summary>
/// AUD-R26 (batch 7, high/security; part of the interception cluster). Neither
/// <c>Jaunty.FlatFiles</c> nor <c>Jaunty.FlatFiles.DuckDB</c> contained a single reference to
/// <c>InterceptorPipeline</c> or <c>JauntyConfig.Logger</c> - grep over every file in both returned
/// zero - against 21 <c>CreateCommand()</c> sites. Every DuckDB read, write, delete, update and
/// write-back was invisible to a registered <see cref="ICommandInterceptor"/>.
///
/// <para>
/// The finding's own conclusion was that this, batch 5's identical finding against Jaunty.Fluent
/// and batch 2's against core's write paths are one problem needing one fix: "a shared execution
/// helper that routes through the pipeline, used by every assembly that opens a command". These
/// tests pin that this assembly now uses it.
/// </para>
/// </summary>
[Collection(GlobalInterceptorStateCollection.Name)]
public class DuckDbObservabilityTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_observability_{Guid.NewGuid():N}");
    private readonly DuckDb _db;
    private readonly CountingInterceptor _interceptor = new();
    private readonly List<string> _logged = [];

    public DuckDbObservabilityTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "inventory.csv");

        using (var genConnection = new DuckDBConnection("DataSource=:memory:"))
        {
            genConnection.Open();
            using DuckDBCommand cmd = genConnection.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'Widget A', 'Electronics', 150, 29.99, true),
                        (2, 'Widget B', 'Electronics', 0, 49.99, false),
                        (3, 'Gadget C', 'Hardware', 75, 15.50, true)
                    ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
                ) TO '{csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(csvPath);
        _db = new DuckDb(options);

        // Registered after construction so the DuckDb session setup - extension loading and view
        // registration - is not what these assertions are seeing.
        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(_interceptor);
        JauntyConfig.Logger = (sql, _) => _logged.Add(sql);
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private sealed class CountingInterceptor : ICommandInterceptor
    {
        public int Executing { get; private set; }
        public int Executed { get; private set; }
        public string? LastSql { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executing++;
            LastSql = context.CommandText;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed++;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    private void AssertObserved(string what)
    {
        Assert.True(_interceptor.Executing > 0,
            $"'{what}' executed a command that no registered ICommandInterceptor saw. " +
            "Jaunty.FlatFiles.DuckDB used to open every command directly, bypassing the pipeline entirely.");

        Assert.True(_interceptor.Executed > 0,
            $"'{what}' reported Executing but never Executed - the pipeline did not wrap the whole operation.");

        Assert.NotNull(_interceptor.LastSql);
        Assert.NotEmpty(_logged);
    }

    [Fact]
    public void ARawDuckDbQueryIsObserved()
    {
        List<InventoryItem> items = _db.Query<InventoryItem>("SELECT * FROM inventory");

        Assert.Equal(3, items.Count);
        AssertObserved("DuckDb.Query<T>(sql)");
    }

    [Fact]
    public void AParameterisedQueryReportsItsParameters()
    {
        List<InventoryItem> items = _db.Query<InventoryItem>(
            "SELECT * FROM inventory WHERE Category = $category", ("category", "Hardware"));

        Assert.Single(items);
        AssertObserved("DuckDb.Query<T>(sql, parameters)");
    }

    [Fact]
    public async Task AnAsyncQueryIsObserved()
    {
        List<InventoryItem> items = await _db.QueryAsync<InventoryItem>(
            "SELECT * FROM inventory", TestContext.Current.CancellationToken);

        Assert.Equal(3, items.Count);
        AssertObserved("DuckDb.QueryAsync<T>(sql)");
    }

    /// <summary>
    /// A write is the case the finding was filed under - someone registering an audit interceptor
    /// for a compliance requirement got nothing from any DuckDB mutation. This also crosses
    /// TablePromoter, since the first mutation promotes the view to a table.
    /// </summary>
    [Fact]
    public void AWriteIsObserved()
    {
        _db.Insert(new InventoryItem
        {
            ItemId = 99,
            ItemName = "Observability probe",
            Category = "Misc",
            StockQuantity = 1,
            UnitPrice = 1.00m,
            InStock = true,
        });

        AssertObserved("DuckDb.Insert<T>(entity)");
    }

    [Fact]
    public void ADeleteIsObserved()
    {
        _ = _db.Delete<InventoryItem>(i => i.ItemId == 2);

        AssertObserved("DuckDb delete");
    }
}
