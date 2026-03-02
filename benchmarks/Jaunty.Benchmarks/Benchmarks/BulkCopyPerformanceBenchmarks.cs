using System.Data;
using System.Data.Common;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using Jaunty.Configuration;

namespace Jaunty.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing native bulk copy vs standard INSERT operations.
/// Tests the performance improvement when native bulk copy is automatically activated.
/// </summary>
[MemoryDiagnoser]
public class BulkCopyPerformanceBenchmarks
{
    private DbConnection _connection = null!;
    private List<JauntyProduct> _smallBatch = null!;
    private List<JauntyProduct> _mediumBatch = null!;
    private List<JauntyProduct> _largeBatch = null!;
    private bool _originalEnableNativeBulkCopy;
    private int _originalMinimumRows;

    [Params(DatabaseProvider.Sqlite, DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MariaDb)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available. Set JAUNTY_TEST_{Provider.ToString().ToUpper()} environment variable to enable.");

        DatabaseSetup.EnsureDatabaseExists(Provider);

        _connection = DatabaseSetup.CreateConnection(Provider);
        _connection.Open();
        DatabaseSetup.CreateSchema(_connection, Provider);

        // Save original configuration
        _originalEnableNativeBulkCopy = BulkCopyConfiguration.EnableNativeBulkCopy;
        _originalMinimumRows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy;

        // Create test data
        _smallBatch = CreateProducts(50);   // Below threshold (100 default)
        _mediumBatch = CreateProducts(500); // Above threshold
        _largeBatch = CreateProducts(5000); // Well above threshold
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        // Restore original configuration
        BulkCopyConfiguration.EnableNativeBulkCopy = _originalEnableNativeBulkCopy;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = _originalMinimumRows;

        _connection?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM benchmark_products";
        cmd.ExecuteNonQuery();
    }

    private static List<JauntyProduct> CreateProducts(int count)
    {
        return Enumerable.Range(0, count).Select(i => new JauntyProduct
        {
            ProductName = $"Product {i}",
            UnitPrice = 10.00m + (i % 100),
            UnitsInStock = 50 + (i % 200),
            Discontinued = i % 10 == 0
        }).ToList();
    }

    // --- Small Batch (50 rows - below threshold) ---

    [Benchmark(Description = "Small batch (50) - Standard INSERT")]
    public void SmallBatch_StandardInsert()
    {
        // Force standard INSERT by disabling native bulk copy
        BulkCopyConfiguration.EnableNativeBulkCopy = false;
        _connection.BulkInsert(_smallBatch);
    }

    [Benchmark(Description = "Small batch (50) - Native bulk copy enabled")]
    public void SmallBatch_NativeBulkCopy()
    {
        // Native bulk copy enabled but below threshold
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;
        _connection.BulkInsert(_smallBatch);
    }

    // --- Medium Batch (500 rows - above threshold) ---

    [Benchmark(Description = "Medium batch (500) - Standard INSERT")]
    public void MediumBatch_StandardInsert()
    {
        BulkCopyConfiguration.EnableNativeBulkCopy = false;
        _connection.BulkInsert(_mediumBatch);
    }

    [Benchmark(Description = "Medium batch (500) - Native bulk copy")]
    public void MediumBatch_NativeBulkCopy()
    {
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;
        _connection.BulkInsert(_mediumBatch);
    }

    // --- Large Batch (5000 rows - well above threshold) ---

    [Benchmark(Description = "Large batch (5000) - Standard INSERT")]
    public void LargeBatch_StandardInsert()
    {
        BulkCopyConfiguration.EnableNativeBulkCopy = false;
        _connection.BulkInsert(_largeBatch);
    }

    [Benchmark(Description = "Large batch (5000) - Native bulk copy")]
    public void LargeBatch_NativeBulkCopy()
    {
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;
        _connection.BulkInsert(_largeBatch);
    }

    // --- Threshold Configuration Tests ---

    [Benchmark(Description = "Threshold test - 100 rows at threshold")]
    public void Threshold_AtBoundary()
    {
        var atThreshold = CreateProducts(100);
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;
        _connection.BulkInsert(atThreshold);
    }

    [Benchmark(Description = "Threshold test - 99 rows below threshold")]
    public void Threshold_BelowBoundary()
    {
        var belowThreshold = CreateProducts(99);
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;
        _connection.BulkInsert(belowThreshold);
    }
}
