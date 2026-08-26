using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Execute;

/// <summary>
/// AUD-R26 (batch 2). Two findings against <c>ExecuteBatch</c>:
///
/// <para>
/// <strong>The silent null skip.</strong> Both cores did <c>if (parameters is null) continue;</c>,
/// so the caller got a lower cumulative row count with nothing to distinguish "one of your sets was
/// null and I ignored it" from "that statement affected no rows" - a strictly larger silent drop
/// than the two the parameter layer already refuses to make in its own comments.
/// </para>
///
/// <para>
/// <strong>The comment that described an optimisation the code did not perform.</strong> The
/// <c>Prepare()</c> comment claimed the loop mirrored <c>BulkInsertLoop</c>, while the loop cleared
/// the parameter collection and fully rebound on every set - so <c>Prepare()</c> was called on a
/// command whose parameters were torn down on the next iteration, and a batch of N sets with M
/// parameters allocated N x M provider parameter objects on the API whose entire purpose is
/// high-volume repetition.
/// </para>
/// </summary>
public class ExecuteBatchContractTests
{
    // A class, not a record: this project also targets net472, where positional records need
    // IsExternalInit, which the framework does not define.
    private sealed class Row(int id, string name, double price)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public double Price { get; } = price;
    }

    private const string InsertSql = "INSERT INTO t (id, name, price) VALUES (@Id, @Name, @Price)";

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE t (id INTEGER, name TEXT, price REAL);";
        seed.ExecuteNonQuery();
        return connection;
    }

    private static int CountRows(SqliteConnection connection)
    {
        using SqliteCommand count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM t";
        return Convert.ToInt32(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------
    // A null set is refused, not skipped
    // ------------------------------------------------------------------

    [Fact]
    public void ANullParameterSet_Throws()
    {
        using SqliteConnection connection = Seed();

        object?[] sets = [new Row(1, "a", 1.0), null, new Row(3, "c", 3.0)];

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => connection.ExecuteBatch(InsertSql, sets!));

        Assert.Equal("parameterSets", ex.ParamName);
    }

    /// <summary>
    /// The index is the point of the message - a batch of ten thousand sets is useless to debug
    /// without it.
    /// </summary>
    [Fact]
    public void TheMessage_NamesTheIndexOfTheOffendingSet()
    {
        using SqliteConnection connection = Seed();

        object?[] sets = [new Row(1, "a", 1.0), new Row(2, "b", 2.0), null];

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => connection.ExecuteBatch(InsertSql, sets!));

        Assert.Contains("index 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANullParameterSetAsync_Throws()
    {
        using SqliteConnection connection = Seed();

        object?[] sets = [new Row(1, "a", 1.0), null];

        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await connection.ExecuteBatchAsync(
                InsertSql, sets!, TestContext.Current.CancellationToken));

        Assert.Equal("parameterSets", ex.ParamName);
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sets before the null one have already executed. That is not a regression - it was true of
    /// the skip too, and <c>ExecuteBatch</c> makes no atomicity promise without a transaction - but
    /// it is worth pinning so nobody reads the throw as a rollback.
    /// </summary>
    [Fact]
    public void TheSetsBeforeTheNullOne_HaveAlreadyExecuted()
    {
        using SqliteConnection connection = Seed();

        object?[] sets = [new Row(1, "a", 1.0), new Row(2, "b", 2.0), null];

        Assert.Throws<ArgumentException>(() => connection.ExecuteBatch(InsertSql, sets!));
        Assert.Equal(2, CountRows(connection));
    }

    // ------------------------------------------------------------------
    // In-place rebinding, and the correctness it must not cost
    // ------------------------------------------------------------------

    [Fact]
    public void EverySet_IsExecutedWithItsOwnValues()
    {
        using SqliteConnection connection = Seed();

        List<object> sets = [.. Enumerable.Range(0, 50).Select(i => (object)new Row(i, "n" + i, i * 1.5))];

        Assert.Equal(50, connection.ExecuteBatch(InsertSql, sets));

        List<IDictionary<string, object?>> rows =
            [.. connection.QueryPartialList("SELECT id, name, price FROM t ORDER BY id")];

        Assert.Equal(50, rows.Count);
        Assert.Equal(0L, rows[0]["id"]);
        Assert.Equal("n49", rows[49]["name"]);
        Assert.Equal(49 * 1.5, rows[49]["price"]);
    }

    /// <summary>
    /// Reusing parameter objects across sets of different runtime types would leave a parameter
    /// holding a DbType the provider inferred from a previous set's value - the templates
    /// deliberately carry no DbType. The loop therefore rebinds from scratch whenever the type
    /// changes, and this is the test that says so.
    /// </summary>
    [Fact]
    public void SetsOfDifferentTypes_AreEachBoundCorrectly()
    {
        using SqliteConnection connection = Seed();

        List<object> sets =
        [
            new Row(1, "record", 1.5),
            new { Id = 2, Name = "anonymous", Price = 2.5 },
            new Row(3, "record again", 3.5),
            new Dictionary<string, object?> { ["Id"] = 4, ["Name"] = "dictionary", ["Price"] = 4.5 },
            new Row(5, "record once more", 5.5),
        ];

        Assert.Equal(5, connection.ExecuteBatch(InsertSql, sets));

        List<IDictionary<string, object?>> rows =
            [.. connection.QueryPartialList("SELECT id, name, price FROM t ORDER BY id")];

        Assert.Equal(["record", "anonymous", "record again", "dictionary", "record once more"],
            rows.Select(r => (string)r["name"]!));
        Assert.Equal([1.5, 2.5, 3.5, 4.5, 5.5], rows.Select(r => (double)r["price"]!));
    }

    /// <summary>
    /// Alternating types on every single set is the worst case for the rebind guard - it must fall
    /// back every time and still be correct.
    /// </summary>
    [Fact]
    public void AlternatingTypes_AreAllBoundCorrectly()
    {
        using SqliteConnection connection = Seed();

        List<object> sets = [];
        for (int i = 0; i < 20; i++)
        {
            sets.Add(i % 2 == 0
                ? new Row(i, "r" + i, i)
                : new { Id = i, Name = "a" + i, Price = (double)i });
        }

        Assert.Equal(20, connection.ExecuteBatch(InsertSql, sets));

        List<IDictionary<string, object?>> rows =
            [.. connection.QueryPartialList("SELECT id, name FROM t ORDER BY id")];

        Assert.Equal(20, rows.Count);
        Assert.Equal("r0", rows[0]["name"]);
        Assert.Equal("a19", rows[19]["name"]);
    }

    [Fact]
    public async Task EverySetAsync_IsExecutedWithItsOwnValues()
    {
        using SqliteConnection connection = Seed();

        List<object> sets = [.. Enumerable.Range(0, 30).Select(i => (object)new Row(i, "n" + i, i))];

        Assert.Equal(30, await connection.ExecuteBatchAsync(
            InsertSql, sets, TestContext.Current.CancellationToken));

        Assert.Equal(30, CountRows(connection));
    }

    /// <summary>
    /// A null value inside a set is a value, not a missing set, and must still bind as NULL - the
    /// in-place path assigns <c>DBNull.Value</c> exactly as the rebuild path did.
    /// </summary>
    [Fact]
    public void ANullValueWithinASet_BindsAsNull()
    {
        using SqliteConnection connection = Seed();

        List<object> sets =
        [
            new { Id = 1, Name = (string?)"present", Price = 1.0 },
            new { Id = 2, Name = (string?)null, Price = 2.0 },
            new { Id = 3, Name = (string?)"also present", Price = 3.0 },
        ];

        Assert.Equal(3, connection.ExecuteBatch(InsertSql, sets));

        List<IDictionary<string, object?>> rows =
            [.. connection.QueryPartialList("SELECT id, name FROM t ORDER BY id")];

        Assert.Equal("present", rows[0]["name"]);
        Assert.Null(rows[1]["name"]);
        Assert.Equal("also present", rows[2]["name"]);
    }

    [Fact]
    public void AnEmptySequence_StillReturnsZero()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(0, connection.ExecuteBatch(InsertSql, []));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The reason the rebind exists. Measured over 5,000 three-parameter sets against
    /// Microsoft.Data.Sqlite: 1,328 bytes per set before, 1,136 after - 192 bytes per set, about
    /// 14%. Modest, because most of a batch's allocation is the provider executing the statement,
    /// not Jaunty binding it; the assertion is deliberately a wide bound rather than the measured
    /// number, since the rest of the figure is the provider's and not ours to pin.
    /// </summary>
    [Fact]
    public void TheRebind_ReducesAllocationPerSet()
    {
        using SqliteConnection connection = Seed();

        const int count = 2_000;
        List<object> sets = [.. Enumerable.Range(0, count).Select(i => (object)new Row(i, "n" + i, i))];

        connection.ExecuteBatch(InsertSql, sets.Take(100).ToList());   // warm the template cache

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        connection.ExecuteBatch(InsertSql, sets);
        long perSet = (GC.GetAllocatedBytesForCurrentThread() - before) / count;

        Assert.True(perSet < 1_300, $"Expected under 1,300 bytes per set with the rebind in place; measured {perSet}.");
    }
#endif
}
