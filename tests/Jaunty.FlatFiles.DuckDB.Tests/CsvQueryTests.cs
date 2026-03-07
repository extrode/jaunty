using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Integration tests for querying CSV files via Jaunty's fluent API.
/// Uses real DuckDB in-memory instances with actual CSV file data.
/// </summary>
public class CsvQueryTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public CsvQueryTests()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose() => _db.Dispose();

    // ==========================================
    // WHERE clause tests (F-016)
    // ==========================================

    [Fact]
    public void Where_Revenue_GreaterThan_FiltersCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Revenue > 10000m));
    }

    [Fact]
    public void Where_StringEquals_FiltersCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Region == "Northeast")
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Northeast", r.Region));
    }

    [Fact]
    public void Where_Compound_And_FiltersCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .And(s => s.Region == "Northeast")
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.True(r.Revenue > 10000m);
            Assert.Equal("Northeast", r.Region);
        });
    }

    [Fact]
    public void Where_In_FiltersCorrectly()
    {
        var regions = new[] { "Northeast", "Midwest" };
        var results = _db.Connection.From<SalesRecord>()
            .WhereIn(s => s.Region!, regions)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Contains(r.Region, regions));
    }

    [Fact]
    public void Where_Between_FiltersCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .WhereBetween(s => s.Revenue, 8000m, 20000m)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.True(r.Revenue >= 8000m);
            Assert.True(r.Revenue <= 20000m);
        });
    }

    // ==========================================
    // ORDER BY tests (F-017)
    // ==========================================

    [Fact]
    public void OrderBy_Revenue_Ascending()
    {
        var results = _db.Connection.From<SalesRecord>()
            .OrderBy(s => s.Revenue)
            .Select();

        Assert.Equal(10, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Revenue <= results[i].Revenue);
    }

    [Fact]
    public void OrderByDescending_Revenue()
    {
        var results = _db.Connection.From<SalesRecord>()
            .OrderByDescending(s => s.Revenue)
            .Select();

        Assert.Equal(10, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
    }

    [Fact]
    public void OrderBy_ThenBy_MultiColumn()
    {
        var results = _db.Connection.From<SalesRecord>()
            .OrderBy(s => s.Region)
            .ThenByDescending(s => s.Revenue)
            .Select();

        Assert.Equal(10, results.Count);
        // Within each region group, revenue should be descending
        for (int i = 1; i < results.Count; i++)
        {
            if (results[i - 1].Region == results[i].Region)
                Assert.True(results[i - 1].Revenue >= results[i].Revenue);
        }
    }

    // ==========================================
    // LIMIT/OFFSET tests (F-018)
    // ==========================================

    [Fact]
    public void Take_LimitsResults()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Skip_Take_Pagination()
    {
        var allResults = _db.Connection.From<SalesRecord>()
            .OrderBy(s => s.Id)
            .Select();

        var page = _db.Connection.From<SalesRecord>()
            .OrderBy(s => s.Id)
            .Skip(2)
            .Take(3)
            .Select();

        Assert.Equal(3, page.Count);
        Assert.Equal(allResults[2].Id, page[0].Id);
        Assert.Equal(allResults[3].Id, page[1].Id);
        Assert.Equal(allResults[4].Id, page[2].Id);
    }

    [Fact]
    public void Take_MoreThanAvailable_ReturnsAll()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Take(100)
            .Select();

        Assert.Equal(10, results.Count);
    }

    // ==========================================
    // Combination tests (F-022)
    // ==========================================

    [Fact]
    public void Where_OrderBy_Take_FullPipeline()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .OrderBy(s => s.Date)
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Revenue > 10000m));
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Date <= results[i].Date);
    }

    [Fact]
    public void Where_OrderByDescending_Skip_Take()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 5000m)
            .OrderByDescending(s => s.Revenue)
            .Skip(1)
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Revenue > 5000m));
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
    }

    // ==========================================
    // Aggregate tests
    // ==========================================

    [Fact]
    public void Count_ReturnsCorrectTotal()
    {
        var count = _db.Connection.From<SalesRecord>().Count();
        Assert.Equal(10, count);
    }

    [Fact]
    public void Count_WithFilter_ReturnsFiltered()
    {
        var count = _db.Connection.From<SalesRecord>()
            .Where(s => s.Region == "Northeast")
            .Count();

        Assert.Equal(3, count); // rows 1, 5, 9 are Northeast
    }

    [Fact]
    public void Sum_Revenue_ReturnsCorrectTotal()
    {
        var sum = _db.Connection.From<SalesRecord>()
            .Sum(s => s.Revenue);

        // Sum of all 10 rows: 15000.50 + 8500.00 + 22000.75 + 5200.00 + 31000.00 + 12000.25 + 18500.50 + 9200.00 + 27500.75 + 6100.00
        Assert.True(sum > 155000m);
    }

    [Fact]
    public void Min_Max_Revenue()
    {
        var min = _db.Connection.From<SalesRecord>()
            .Min(s => s.Revenue);
        var max = _db.Connection.From<SalesRecord>()
            .Max(s => s.Revenue);

        Assert.Equal(5200.00m, min);
        Assert.Equal(31000.00m, max);
    }

    // ==========================================
    // Column mapping tests
    // ==========================================

    [Fact]
    public void ColumnAttribute_MapsCorrectly()
    {
        // SalesRecord.ProductName has [Column("product_name")]
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.ProductName == "Widget A")
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Widget A", r.ProductName));
    }

    [Fact]
    public void KeyAttribute_IncludedInResults()
    {
        var results = _db.Connection.From<SalesRecord>()
            .OrderBy(s => s.Id)
            .Take(1)
            .Select();

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
    }

    [Fact]
    public void AllColumns_MaterializedCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Id == 1)
            .Select();

        Assert.Single(results);
        var row = results[0];
        Assert.Equal(1, row.Id);
        Assert.Equal("Widget A", row.ProductName);
        Assert.Equal(15000.50m, row.Revenue);
        Assert.Equal(120, row.Quantity);
        Assert.Equal("Northeast", row.Region);
        Assert.Equal(new DateTime(2024, 1, 15), row.Date);
    }

    // ==========================================
    // SELECT projection tests (F-019, P1)
    // ==========================================

    [Fact]
    public void SelectPartial_SubsetColumns_ReturnsCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .SelectPartial("product_name", "Revenue");

        Assert.Equal(10, results.Count);
        // ProductName and Revenue should be populated
        Assert.All(results, r =>
        {
            Assert.NotNull(r.ProductName);
            Assert.NotEqual(0m, r.Revenue);
        });
    }

    [Fact]
    public void SelectAll_ReturnsAllRows()
    {
        var results = _db.Connection.From<SalesRecord>().Select();
        Assert.Equal(10, results.Count);
    }

    // ==========================================
    // Async tests
    // ==========================================

    [Fact]
    public async Task SelectAsync_ReturnsResults()
    {
        var results = await _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .OrderByDescending(s => s.Revenue)
            .SelectAsync();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Revenue > 10000m));
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectTotal()
    {
        var count = await _db.Connection.From<SalesRecord>()
            .CountAsync();

        Assert.Equal(10, count);
    }
}
