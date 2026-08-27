#if NET8_0_OR_GREATER
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Write;
using Jaunty.Tests.Entities;
using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Unit;

/// <summary>
/// Pinned allocation budgets for the paths that run on every call. A budget is a measured
/// actual plus roughly 50% headroom, not an aspiration: it fails when a change starts
/// allocating on a path that did not, which no behavioural test would notice.
///
/// <para>
/// The harness follows <c>TypedKeyGuardTests.Measure</c>, including its lesson: warm up first,
/// because a cold call measures the JIT rather than the workload, and never let the measured
/// body be something the JIT can prove dead - .NET 10's escape analysis elides those outright
/// and a budget over an elided allocation asserts nothing.
/// </para>
/// </summary>
[Trait("Category", "AllocationBudget")]
public class AllocationBudgetTests
{
    private const int Iterations = 10_000;

    private static long MeasurePerCall(Action body)
    {
        for (int i = 0; i < 1_000; i++)
            body();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < Iterations; i++)
            body();

        return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
    }

    private static void AssertWithinBudget(string what, long perCall, long budget)
    {
        Assert.True(
            perCall <= budget,
            $"{what}: {perCall} bytes/call exceeds the pinned budget of {budget}.");
    }

    [Fact]
    public void ExtractParameterNames_EmptySql_AllocatesNothing()
    {
        long perCall = MeasurePerCall(static () => Sink(SqlParameterParser.ExtractParameterNames("")));

        AssertWithinBudget("ExtractParameterNames(empty)", perCall, 0);
    }

    [Fact]
    public void ExtractParameterNames_WhitespaceSql_AllocatesNothing()
    {
        long perCall = MeasurePerCall(static () => Sink(SqlParameterParser.ExtractParameterNames("   ")));

        AssertWithinBudget("ExtractParameterNames(whitespace)", perCall, 0);
    }

    [Fact]
    public void ExtractParameterNames_SqlWithoutParameters_StaysWithinBudget()
    {
        const string sql = "SELECT id, name, value FROM get_test ORDER BY id";

        long perCall = MeasurePerCall(static () => Sink(SqlParameterParser.ExtractParameterNames(sql)));

        AssertWithinBudget("ExtractParameterNames(no parameters)", perCall, 136);
    }

    [Fact]
    public void ExtractParameterNames_SqlWithThreeParameters_StaysWithinBudget()
    {
        const string sql =
            "SELECT id, name, value FROM get_test WHERE id = @id AND name = @name AND value > @value";

        long perCall = MeasurePerCall(static () => Sink(SqlParameterParser.ExtractParameterNames(sql)));

        AssertWithinBudget("ExtractParameterNames(3 parameters)", perCall, 280);
    }

    [Fact]
    public void ExtractParameterNames_ParametersInsideLiteralsAndComments_StaysWithinBudget()
    {
        const string sql =
            "SELECT '@notaparam', id -- @alsonot\n FROM get_test /* @nope */ WHERE id = @id";

        long perCall = MeasurePerCall(static () => Sink(SqlParameterParser.ExtractParameterNames(sql)));

        AssertWithinBudget("ExtractParameterNames(literals and comments)", perCall, 200);
    }

    [Fact]
    public void CrudSqlCache_WarmCacheHit_AllocatesNothing()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        long perCall = MeasurePerCall(() => Sink(CrudSqlCache.GetSql<GetTestEntity>(connection)));

        AssertWithinBudget("CrudSqlCache.GetSql (warm hit)", perCall, 0);
    }

    private static readonly object?[] _sink = new object?[1];

    private static void Sink(object? value) => _sink[0] = value;
}
#endif
