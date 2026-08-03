using DuckDB.NET.Data;

using Jaunty.Configuration;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.Import;
using Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Unit;

/// <summary>
/// AUD-R34-007. AUD-R26's interception fix reached <c>NonQueryExecutor</c>, <c>DuckDbRead</c>,
/// <c>DuckDbMultiple</c> and <c>TablePromoter</c>, and <c>DuckDbObservation</c>'s own remarks name
/// "write-back and import" as part of what it fixed - but the import pipeline was never touched. A
/// grep of <c>Internals/Import</c> for <c>CommandObservation</c> returned nothing: the CREATE TABLE,
/// the schema probe, the source read and every INSERT ran on raw <c>DbCommand</c>/<c>DbBatch</c>
/// instances. A registered audit interceptor - the compliance scenario the original finding was
/// filed under - saw nothing at all for an <c>ImportIntoAsync</c> call.
/// <para>
/// The insert side is reported once for the whole import rather than once per row, matching what
/// core's bulk paths already do (see <c>BulkInsertAsync</c>): a large import is one logical write,
/// and firing the pipeline per row would swamp an auditor.
/// </para>
/// </summary>
[Collection(GlobalInterceptorStateCollection.Name)]
public class ImportObservabilityTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_observability_{Guid.NewGuid():N}");
    private readonly DuckDb _db;
    private readonly RecordingInterceptor _interceptor = new();
    private readonly List<string> _logged = [];

    public ImportObservabilityTests()
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

    private sealed class RecordingInterceptor : ICommandInterceptor
    {
        public List<string> Executing { get; } = [];
        public int Executed { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executing.Add(context.CommandText);
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed++;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    private static SqliteConnection CreateSqliteConnection()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        return conn;
    }

    private static void CreateInventoryTable(SqliteConnection conn)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE ""inventory"" (
                ""ItemId"" INTEGER PRIMARY KEY,
                ""ItemName"" TEXT NOT NULL,
                ""Category"" TEXT NOT NULL,
                ""StockQuantity"" INTEGER NOT NULL,
                ""UnitPrice"" REAL NOT NULL,
                ""InStock"" INTEGER NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task AnImportIsObserved()
    {
        using SqliteConnection sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        long count = await _db.ImportIntoAsync<InventoryItem>(sqlite);

        Assert.Equal(3, count);
        Assert.NotEmpty(_interceptor.Executing);
        Assert.True(_interceptor.Executed > 0, "the pipeline reported Executing but never Executed");
        Assert.NotEmpty(_logged);
    }

    [Fact]
    public async Task TheInsertItselfReachesTheInterceptor()
    {
        using SqliteConnection sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        Assert.Contains(_interceptor.Executing, sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheSourceReadReachesTheInterceptor()
    {
        using SqliteConnection sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        Assert.Contains(_interceptor.Executing, sql =>
            sql.Contains("SELECT * FROM", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("inventory", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The whole import is one logical write, so the INSERT is reported once - not once per row.
    /// Three rows must not produce three INSERT notifications.
    /// </summary>
    [Fact]
    public async Task TheInsertIsReportedOncePerImportNotOncePerRow()
    {
        using SqliteConnection sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        int insertNotifications = _interceptor.Executing
            .Count(sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, insertNotifications);
    }

    [Fact]
    public async Task TheCreatedTableDdlReachesTheInterceptor()
    {
        using SqliteConnection sqlite = CreateSqliteConnection();

        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(createTableIfMissing: true));

        Assert.Contains(_interceptor.Executing, sql => sql.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
    }
}
