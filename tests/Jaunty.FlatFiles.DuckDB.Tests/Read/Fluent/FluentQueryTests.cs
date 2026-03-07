using System.Text;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read.Fluent;

/// <summary>
/// Tests for the Fluent Query API (Query, QueryAsync, From, Where, OrderBy, etc.).
/// </summary>
public class FluentQueryTests : IDisposable
{
    private readonly DuckDb _db;
    private readonly string _testCsvPath;

    public FluentQueryTests()
    {
        _testCsvPath = Path.Combine(AppContext.BaseDirectory, "test-data", "fluent-query-test-sales.csv");

        // Ensure test data directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_testCsvPath)!);

        // Create test CSV
        CreateTestCsv();

        // Create database
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(_testCsvPath);
        _db = new DuckDb(options);
    }

    private void CreateTestCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine("id,product_name,revenue,quantity,date,region");
        csv.AppendLine("1,Widget A,1000.50,10,2024-01-15,North");
        csv.AppendLine("2,Widget B,2500.00,25,2024-02-20,South");
        csv.AppendLine("3,Gadget X,5000.75,5,2024-03-10,East");
        csv.AppendLine("4,Gadget Y,7500.00,15,2024-04-05,West");
        csv.AppendLine("5,Tool Z,3000.25,30,2024-05-12,Central");
        File.WriteAllText(_testCsvPath, csv.ToString());
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testCsvPath))
            File.Delete(_testCsvPath);
    }

    // ==========================================
    // Query<T>() Tests
    // ==========================================

    [Fact]
    public void Query_ReturnsFluentQueryBuilder()
    {
        // Act
        var query = _db.Connection.From<SalesRecord>();

        // Assert
        Assert.NotNull(query);
        Assert.IsAssignableFrom<IFromClause<SalesRecord>>(query);
    }

    [Fact]
    public void Query_WithSelect_ReturnsAllRows()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>().Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_WithWhere_FilterResults()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 3000m)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count); // Gadget X, Gadget Y, Tool Z
        Assert.All(results, r => Assert.True(r.Revenue > 3000m));
    }

    [Fact]
    public void Query_WithOrderBy_SortsResults()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .OrderByDescending(x => x.Revenue)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
        }
    }

    [Fact]
    public void Query_WithTake_LimitsResults()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .Take(3)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Query_WithSkipAndTake_PaginatesResults()
    {
        // Act
        var allResults = _db.Connection.From<SalesRecord>().Select();
        var page2 = _db.Connection.From<SalesRecord>()
            .Skip(2)
            .Take(2)
            .Select();

        // Assert
        Assert.NotNull(page2);
        Assert.Equal(2, page2.Count);
        Assert.Equal(allResults[2].Id, page2[0].Id);
        Assert.Equal(allResults[3].Id, page2[1].Id);
    }

    [Fact]
    public void Query_WithCompoundWhere_AppliesBothConditions()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 2000m && x.Quantity > 10)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count); // Widget B, Gadget Y, Tool Z
    }

    // ==========================================
    // QueryAsync<T>() Tests
    // ==========================================

    [Fact]
    public async Task QueryAsync_WithoutParameters_ExecutesRawSql()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>("SELECT * FROM sales");

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task QueryAsync_WithWhereClause_FiltersResults()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue > $1",
            new[] { (Name: "$1", Value: (object?)3000.0m) });

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Revenue > 3000m));
    }

    [Fact]
    public async Task QueryAsync_WithMultipleParameters_BindsCorrectly()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue > $1 AND quantity < $2",
            new[]
            {
                (Name: "$1", Value: (object?)2000.0m),
                (Name: "$2", Value: (object?)20)
            });

        // Assert
        Assert.NotNull(results);
        // Gadget X (5000.75, 5) and Gadget Y (7500, 15)
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task QueryAsync_WithOrderBy_SortsResults()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales ORDER BY revenue DESC");

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
        }
    }

    [Fact]
    public async Task QueryAsync_EmptyResult_ReturnsEmptyList()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue > 1000000");

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    // ==========================================
    // Integration Tests
    // ==========================================

    [Fact]
    public void FullQueryPipeline_EndToEnd()
    {
        // Act - Complex query with multiple operations
        var results = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 2000m)
            .OrderByDescending(x => x.Revenue)
            .Take(3)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.Equal("Gadget Y", results[0].ProductName); // Highest revenue > 2000
        Assert.Equal("Gadget X", results[1].ProductName);
        Assert.Equal("Tool Z", results[2].ProductName);
    }

    [Fact]
    public async Task QueryAsync_Integration_WithParameters()
    {
        // Arrange
        var minRevenue = 2500.0m;

        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue >= $1 ORDER BY revenue DESC",
            new[] { (Name: "$1", Value: (object?)minRevenue) });

        // Assert
        Assert.NotNull(results);
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.True(r.Revenue >= minRevenue));
    }
}
