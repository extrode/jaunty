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

    // AUD-R7: this test's name and premise ("...CachesCompiledDelegates") were wrong.
    // ExpressionTranslator.EvaluateExpression deliberately does NOT cache compiled delegates -
    // its own source comment explains why: a string-keyed cache would return a stale delegate
    // for closure-captured variables (e.g. `x => x.Age > someLocalVar`), since two calls with
    // different captured values stringify identically. There is nothing to inspect a cache for.
    // What's actually worth regression-testing here is that repeated evaluation of the same
    // expression instance recompiles correctly and consistently each time.
    [Fact]
    public void EvaluateExpression_ConstantExpression_EvaluatesConsistentlyAcrossRepeatedCalls()
    {
        // Arrange
        var constantExpr = Expression.Constant(42);

        // Act
        var result1 = InvokeEvaluateExpression(constantExpr);
        var result2 = InvokeEvaluateExpression(constantExpr);

        // Assert
        Assert.Equal(42, result1);
        Assert.Equal(42, result2);
    }

    // AUD-R35-008: this was a wall-clock threshold - 100 queries under 1500ms - already skipped on
    // CI as "pure noise on shared runners" and intermittently red locally under a parallel run. A
    // timing budget is not an assertion about behaviour, and there is no cache here to assert
    // against (see the AUD-R7 note above). What repetition can actually establish is that repeated
    // translation of the same expression stays correct: same SQL, same rows, every iteration.
    [Fact]
    public void RepeatedTranslationOfTheSameExpressionIsStable()
    {
        string firstSql = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 1000m)
            .ToSql();

        List<SalesRecord> first = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 1000m)
            .Select()
            .ToList();

        Assert.NotEmpty(first);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(firstSql, _db.Connection.From<SalesRecord>()
                .Where(x => x.Revenue > 1000m)
                .ToSql());

            Assert.Equal(
                first.Select(r => r.Id),
                _db.Connection.From<SalesRecord>()
                    .Where(x => x.Revenue > 1000m)
                    .Select()
                    .Select(r => r.Id));
        }
    }

    // The closure case the AUD-R7 note names: two chains that stringify identically but capture
    // different values must not collapse into one another.
    [Fact]
    public void ADifferentCapturedValueTranslatesToADifferentResult()
    {
        decimal threshold = 1000m;
        List<SalesRecord> low = _db.Connection.From<SalesRecord>().Where(x => x.Revenue > threshold).Select().ToList();

        threshold = 6000m;
        List<SalesRecord> high = _db.Connection.From<SalesRecord>().Where(x => x.Revenue > threshold).Select().ToList();

        Assert.NotEqual(low.Count, high.Count);
        Assert.All(high, r => Assert.True(r.Revenue > 6000m));
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