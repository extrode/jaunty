using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Integration tests for querying TSV files via Jaunty's fluent API.
/// </summary>
public class TsvQueryTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public TsvQueryTests()
    {
        var tsvPath = Path.Combine(DataDir, "tsv", "sales.tsv");
        var options = new FlatFileOptions();
        options.AddTsv<SalesRecord>(tsvPath);
        _db = new DuckDb(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Query_Tsv_ReturnsAllRows()
    {
        var results = _db.Connection.From<SalesRecord>().Select();
        Assert.Equal(5, results.Count); // TSV has 5 rows
    }

    [Fact]
    public void Where_Tsv_FiltersCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Revenue > 10000m));
    }

    [Fact]
    public void OrderBy_Tsv_OrdersCorrectly()
    {
        var results = _db.Connection.From<SalesRecord>()
            .OrderBy(s => s.Revenue)
            .Select();

        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Revenue <= results[i].Revenue);
    }

    [Fact]
    public void Tsv_ColumnMapping_Works()
    {
        // Verify [Column("product_name")] and [Column("region")] map correctly with TSV
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.ProductName == "Widget A")
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Widget A", r.ProductName));
    }

    [Fact]
    public void Tsv_AllColumns_MaterializedCorrectly()
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
    }

    [Fact]
    public void Tsv_Where_OrderBy_Take_Pipeline()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Quantity > 50)
            .OrderByDescending(s => s.Revenue)
            .Take(2)
            .Select();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Quantity > 50));
        Assert.True(results[0].Revenue >= results[1].Revenue);
    }

    [Fact]
    public void Tsv_Count_ReturnsCorrectTotal()
    {
        var count = _db.Connection.From<SalesRecord>().Count();
        Assert.Equal(5, count);
    }
}
