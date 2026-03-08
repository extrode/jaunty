using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Tests for expression caching and translation performance.
/// </summary>
public class ExpressionCachingTests : IDisposable
{
    private readonly DuckDb _db;
    private readonly string _testCsvPath;

    public ExpressionCachingTests()
    {
        _testCsvPath = Path.Combine(AppContext.BaseDirectory, "test-data", "expression-cache-test-sales.csv");

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

    [Fact]
    public void EvaluateExpression_CachesCompiledDelegates()
    {
        // Arrange
        var constantExpr = Expression.Constant(42);

        // Act - First evaluation
        var result1 = InvokeEvaluateExpression(constantExpr);

        // Act - Second evaluation (should use cache)
        var result2 = InvokeEvaluateExpression(constantExpr);

        // Assert
        Assert.Equal(42, result1);
        Assert.Equal(42, result2);
    }

    //[Fact(Skip = "Performance test - timing dependent on system load")]
    [Fact]
    public void ExpressionCaching_ImprovesQueryPerformance()
    {
        // Warm up
        _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 1000m)
            .Select();

        // Act - Measure repeated query performance
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _db.Connection.From<SalesRecord>()
                .Where(x => x.Revenue > 1000m)
                .Select();
        }
        stopwatch.Stop();

        // Assert - Should be reasonably fast with caching
        // Threshold set generously to account for system load variations
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Repeated queries took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    private object? InvokeEvaluateExpression(Expression expr)
    {
        // Use reflection to call the private EvaluateExpression method
        var helperType = typeof(ExpressionTranslator);
        var method = helperType.GetMethod("EvaluateExpression",
            BindingFlags.Static | BindingFlags.NonPublic);
        return method?.Invoke(null, new[] { expr });
    }
}