#if NET8_0_OR_GREATER
using System.Diagnostics;

using Jaunty.Attributes;
using Jaunty.Dialects;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// Diagnostic + regression coverage for the SQLite bulk-insert path
/// (PROD-120: measured 16x slower than a plain transactional loop).
/// </summary>
public class SqliteBulkPathDiagnosticTests : IDisposable
{
    [Table("bulk_diag_products")]
    public class DiagProduct
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int UnitsInStock { get; set; }
        public bool Discontinued { get; set; }
    }

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"jaunty-bulkdiag-{Guid.NewGuid():N}.db");
    private readonly SqliteConnection _connection;
    private readonly ITestOutputHelper _output;

    public SqliteBulkPathDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE bulk_diag_products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName TEXT NOT NULL,
                UnitPrice REAL NOT NULL,
                UnitsInStock INTEGER NOT NULL,
                Discontinued INTEGER NOT NULL)
            """;
        cmd.ExecuteNonQuery();
    }

    private static List<DiagProduct> MakeProducts(int n) =>
        [.. Enumerable.Range(0, n).Select(i => new DiagProduct
        {
            ProductName = $"P{i}",
            UnitPrice = 1.5m + i,
            UnitsInStock = i % 100,
            Discontinued = i % 7 == 0
        })];

    [Fact]
    public void Diagnose_BulkInsert_Layers()
    {
        const int N = 10_000;

        ISqlDialect dialect = SqlDialectFactory.GetDialect(_connection);
        _output.WriteLine($"dialect: {dialect.GetType().FullName}");
        var provider = dialect.CreateBulkCopyProvider();
        _output.WriteLine($"provider: {provider?.GetType().FullName ?? "null"}");

        // warmup (JIT + metadata caches)
        _connection.BulkInsert(MakeProducts(100));

        var swBulk = Stopwatch.StartNew();
        int inserted = _connection.BulkInsert(MakeProducts(N));
        swBulk.Stop();
        Assert.Equal(N, inserted);
        _output.WriteLine($"Jaunty BulkInsert {N}: {swBulk.ElapsedMilliseconds} ms");

        // manual baseline: one prepared command, one transaction
        var products = MakeProducts(N);
        var swLoop = Stopwatch.StartNew();
        using (var txn = _connection.BeginTransaction())
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = txn;
            cmd.CommandText = "INSERT INTO bulk_diag_products (ProductName, UnitPrice, UnitsInStock, Discontinued) VALUES (@n, @p, @s, @d)";
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
            var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; cmd.Parameters.Add(pp);
            var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; cmd.Parameters.Add(ps);
            var pd = cmd.CreateParameter(); pd.ParameterName = "@d"; cmd.Parameters.Add(pd);
            cmd.Prepare();
            foreach (var e in products)
            {
                pn.Value = e.ProductName; pp.Value = e.UnitPrice; ps.Value = e.UnitsInStock; pd.Value = e.Discontinued;
                cmd.ExecuteNonQuery();
            }
            txn.Commit();
        }
        swLoop.Stop();
        _output.WriteLine($"manual prepared loop {N}: {swLoop.ElapsedMilliseconds} ms");

        double ratio = (double)swBulk.ElapsedMilliseconds / Math.Max(1, swLoop.ElapsedMilliseconds);
        _output.WriteLine($"ratio: {ratio:F2}x");

        // Regression gate (PROD-120): BulkInsert must not be dramatically slower
        // than a plain transactional prepared loop on SQLite.
        Assert.True(ratio < 3.0, $"SQLite BulkInsert is {ratio:F2}x slower than a plain prepared loop");
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
        GC.SuppressFinalize(this);
    }
}
#endif
