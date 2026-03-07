using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// Tests for JSON array file source: registration, querying with fluent API.
/// Covers T028 (JsonFileSource) and T035 (JSON array format).
/// </summary>
public class JsonQueryTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private readonly DuckDb _db;

    public JsonQueryTests()
    {
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");
        var options = new FlatFileOptions();
        options.AddJson<CustomerProfile>(jsonPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ==========================================
    // T028 — JSON array registration & query
    // ==========================================

    [Fact]
    public void Json_Select_ReturnsAllRows()
    {
        var results = _db.Connection.From<CustomerProfile>().Select();
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Json_Count_ReturnsCorrectTotal()
    {
        var count = _db.Connection.From<CustomerProfile>().Count();
        Assert.Equal(3, count);
    }

    [Fact]
    public void Json_Where_StringEquals_FiltersCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.Name == "John Doe")
            .Select();

        Assert.Single(results);
        Assert.Equal("john@example.com", results[0].Email);
    }

    [Fact]
    public void Json_Where_IntEquals_FiltersCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId == 2)
            .Select();

        Assert.Single(results);
        Assert.Equal("Jane Smith", results[0].Name);
    }

    [Fact]
    public void Json_OrderBy_OrdersCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .OrderByDescending(c => c.CustomerId)
            .Select();

        Assert.Equal(3, results.Count);
        Assert.Equal(3, results[0].CustomerId);
        Assert.Equal(2, results[1].CustomerId);
        Assert.Equal(1, results[2].CustomerId);
    }

    [Fact]
    public void Json_Take_LimitsResults()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .OrderBy(c => c.CustomerId)
            .Take(2)
            .Select();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Json_NullField_HandledCorrectly()
    {
        // Customer 2 (Jane Smith) has Phone = null
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId == 2)
            .Select();

        Assert.Single(results);
        Assert.Null(results[0].Phone);
    }

    [Fact]
    public void Json_DateTimeColumn_MapsCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId == 1)
            .Select();

        Assert.Single(results);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), results[0].CreatedAt);
    }

    [Fact]
    public void Json_Where_OrderBy_Take_Pipeline()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId > 1)
            .OrderBy(c => c.Name)
            .Take(2)
            .Select();

        Assert.Equal(2, results.Count);
        // Bob Johnson comes before Jane Smith alphabetically
        Assert.Equal("Bob Johnson", results[0].Name);
        Assert.Equal("Jane Smith", results[1].Name);
    }

    // ==========================================
    // T030 — JsonFileOptions (format, depth)
    // ==========================================

    [Fact]
    public void Json_ExplicitArrayFormat_Works()
    {
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");
        var options = new FlatFileOptions();
        options.AddJson<CustomerProfile>(jsonPath, json =>
        {
            json.JsonFormat = JsonFileFormat.Array;
        });

        using var db = new DuckDb(options);
        var count = db.Connection.From<CustomerProfile>().Count();
        Assert.Equal(3, count);
    }

    [Fact]
    public void Json_MaxDepth_CanBeConfigured()
    {
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");
        var options = new FlatFileOptions();
        options.AddJson<CustomerProfile>(jsonPath, json =>
        {
            json.MaxDepth = 5;
        });

        using var db = new DuckDb(options);
        var count = db.Connection.From<CustomerProfile>().Count();
        Assert.Equal(3, count);
    }
}
