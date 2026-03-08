using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// Tests for newline-delimited JSON (NDJSON) file source.
/// Covers T036 (NDJSON format).
/// </summary>
public class NdjsonQueryTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private readonly DuckDb _db;

    public NdjsonQueryTests()
    {
        var ndjsonPath = Path.Combine(DataDir, "json", "customers.ndjson");
        var options = new FlatFileOptions();
        options.AddJson<CustomerProfile>(ndjsonPath, json =>
        {
            json.JsonFormat = JsonFileFormat.NewlineDelimited;
        });
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void Ndjson_Select_ReturnsAllRows()
    {
        var results = _db.Connection.From<CustomerProfile>().Select();
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Ndjson_Count_ReturnsCorrectTotal()
    {
        var count = _db.Connection.From<CustomerProfile>().Count();
        Assert.Equal(3, count);
    }

    [Fact]
    public void Ndjson_Where_FiltersCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.Name == "Bob Johnson")
            .Select();

        Assert.Single(results);
        Assert.Equal("bob@example.com", results[0].Email);
    }

    [Fact]
    public void Ndjson_OrderBy_OrdersCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .OrderBy(c => c.CustomerId)
            .Select();

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0].CustomerId);
        Assert.Equal(2, results[1].CustomerId);
        Assert.Equal(3, results[2].CustomerId);
    }

    [Fact]
    public void Ndjson_NullField_HandledCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId == 2)
            .Select();

        Assert.Single(results);
        Assert.Null(results[0].Phone);
    }

    [Fact]
    public void Ndjson_DateTimeColumn_MapsCorrectly()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId == 3)
            .Select();

        Assert.Single(results);
        Assert.Equal(new DateTime(2024, 3, 10, 9, 0, 0), results[0].CreatedAt);
    }

    [Fact]
    public void Ndjson_Where_OrderBy_Take_Pipeline()
    {
        var results = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId >= 2)
            .OrderByDescending(c => c.CustomerId)
            .Take(1)
            .Select();

        Assert.Single(results);
        Assert.Equal(3, results[0].CustomerId);
    }
}