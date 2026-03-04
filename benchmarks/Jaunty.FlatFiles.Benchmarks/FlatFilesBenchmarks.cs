using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Jaunty.FlatFiles.DuckDB;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class FlatFilesBenchmarks
{
    private const string SmallCsvPath = "Data/small-sales.csv";
    private const string ParquetPath = "Data/inventory.parquet";
    private const string JsonPath = "Data/customers.json";

    private IFlatFileDatabase? _csvDb;
    private IFlatFileDatabase? _parquetDb;
    private IFlatFileDatabase? _jsonDb;

    [GlobalSetup]
    public void Setup()
    {
        // Generate test data if not exists
        TestDataGenerator.GenerateAll();

        // Open databases for benchmarks
        _csvDb = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        _parquetDb = FlatFileDatabase.Open(opts =>
            opts.AddParquet<InventoryItem>(ParquetPath));

        _jsonDb = FlatFileDatabase.Open(opts =>
            opts.AddJson<CustomerProfile>(JsonPath, json =>
                json.JsonFormat = JsonFileFormat.NewlineDelimited));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _csvDb?.Dispose();
        _parquetDb?.Dispose();
        _jsonDb?.Dispose();
    }

    // ==========================================
    // File Open Benchmarks
    // ==========================================

    [Benchmark]
    [BenchmarkCategory("FileOpen")]
    public IFlatFileDatabase Open_SingleCsv()
    {
        return FlatFileDatabase.Open(SmallCsvPath);
    }

    [Benchmark]
    [BenchmarkCategory("FileOpen")]
    public IFlatFileDatabase Open_MultiSource()
    {
        return FlatFileDatabase.Open(opts =>
        {
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true);
            opts.AddParquet<InventoryItem>(ParquetPath);
            opts.AddJson<CustomerProfile>(JsonPath, json =>
                json.JsonFormat = JsonFileFormat.NewlineDelimited);
        });
    }

    // ==========================================
    // CRUD Benchmarks
    // ==========================================

    [Benchmark]
    [BenchmarkCategory("CRUD")]
    public async Task<int> Crud_Insert_Single()
    {
        var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        try
        {
            return await db.InsertAsync(new SalesRecord
            {
                Id = 99999,
                ProductName = "Benchmark Product",
                Revenue = 99999.99m,
                Quantity = 100,
                Date = DateTime.UtcNow,
                Region = "Benchmark"
            });
        }
        finally
        {
            db.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("CRUD")]
    public async Task<int> Crud_Insert_Batch()
    {
        var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        try
        {
            var entities = new List<SalesRecord>();
            for (int i = 0; i < 100; i++)
            {
                entities.Add(new SalesRecord
                {
                    Id = 100000 + i,
                    ProductName = $"Product {i}",
                    Revenue = 1000 + i,
                    Quantity = 10 + i,
                    Date = DateTime.UtcNow.AddDays(-i),
                    Region = "Batch"
                });
            }
            return await db.InsertAsync(entities);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("CRUD")]
    public async Task<int> Crud_Update()
    {
        var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        try
        {
            return await db.UpdateAsync<SalesRecord>(
                x => x.Revenue > 0,
                x => x.Revenue,
                9999.99m);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("CRUD")]
    public async Task<int> Crud_Delete()
    {
        var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        try
        {
            return await db.DeleteAsync<SalesRecord>(x => x.Revenue < 100);
        }
        finally
        {
            db.Dispose();
        }
    }

    // ==========================================
    // Write-Back Benchmarks
    // ==========================================

    [Benchmark]
    [BenchmarkCategory("WriteBack")]
    public async Task WriteBack_Export_Csv()
    {
        var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        try
        {
            await db.ExportAsync<SalesRecord>("Data/export-test.csv");
        }
        finally
        {
            db.Dispose();
            if (File.Exists("Data/export-test.csv"))
                File.Delete("Data/export-test.csv");
        }
    }

    [Benchmark]
    [BenchmarkCategory("WriteBack")]
    public async Task WriteBack_Export_Parquet()
    {
        var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        try
        {
            await db.ExportAsync<SalesRecord>("Data/export-test.parquet");
        }
        finally
        {
            db.Dispose();
            if (File.Exists("Data/export-test.parquet"))
                File.Delete("Data/export-test.parquet");
        }
    }

    // ==========================================
    // Import Benchmarks
    // ==========================================

    [Benchmark]
    [BenchmarkCategory("Import")]
    public async Task<long> Import_ToSqlite()
    {
        using var db = FlatFileDatabase.Open(opts =>
            opts.AddCsv<SalesRecord>(SmallCsvPath, csv => csv.HasHeader = true));

        using var target = new SqliteConnection("Data Source=:memory:");
        await target.OpenAsync();

        return await db.ImportIntoAsync<SalesRecord>(target, import =>
        {
            import.BatchSize = 1000;
            import.CreateTableIfMissing = true;
        });
    }
}

[HideColumns("Job", "StdDev", "Error")]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<FlatFilesBenchmarks>();
    }
}
