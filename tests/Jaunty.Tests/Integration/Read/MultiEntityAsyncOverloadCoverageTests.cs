using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-088 (async arity 2) and AUD-R35-089 (async arity 3), both carried from round 33. Eleven
/// overloads had no call site anywhere in <c>tests/</c>: at arity 2 the bare
/// <c>QueryAsync(sql, parameters, ct)</c>, <c>QueryAsync(sql, parameters, CommandOptions, ct)</c>
/// and four <c>MultiEntityCommandOptions</c> shapes the options file happens to skip while covering
/// their <c>CommandOptions</c> siblings; at arity 3 the bare <c>(sql, parameters, ct)</c> form of
/// four families plus <c>QuerySingleOrDefaultAsync(sql, ct)</c>, which was reached only through its
/// options overloads.
/// <para>
/// Each asserts against the command via <see cref="RecordingDbConnection"/> wherever an option is
/// passed, so the case fails if the overload stops forwarding rather than only if it stops
/// returning rows.
/// </para>
/// </summary>
public class MultiEntityAsyncOverloadCoverageTests
{
    internal sealed class First
    {
        public long FirstId { get; set; }
    }

    internal sealed class Second
    {
        public long SecondId { get; set; }
    }

    internal sealed class Third
    {
        public long ThirdId { get; set; }
    }

    private const string Two = "SELECT 1 AS FirstId, 2 AS SecondId WHERE 1 = @One";
    private const string Three = "SELECT 1 AS FirstId, 2 AS SecondId, 3 AS ThirdId WHERE 1 = @One";
    private const string ThreeNoParams = "SELECT 1 AS FirstId, 2 AS SecondId, 3 AS ThirdId";

    private static RecordingDbConnection Open()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        return new RecordingDbConnection(inner);
    }

    private static readonly object One = new { One = 1 };

    // ------------------------------------------------------------------
    // Arity 2.
    // ------------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_SqlParametersAndToken()
    {
        using var connection = Open();

        List<(First, Second)> rows = await connection.QueryAsync<First, Second>(Two, One, CancellationToken.None);

        Assert.Equal(1, Assert.Single(rows).Item1.FirstId);
    }

    [Fact]
    public async Task QueryAsync_SqlParametersCommandOptionsAndToken()
    {
        using var connection = Open();

        List<(First, Second)> rows = await connection.QueryAsync<First, Second>(
            Two, One, new CommandOptions<(First, Second)>(commandTimeout: 61), CancellationToken.None);

        Assert.Single(rows);
        Assert.Equal(61, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QueryFirstAsync_SqlParametersAndMultiEntityOptions()
    {
        using var connection = Open();

        (First first, Second second) = await connection.QueryFirstAsync<First, Second>(
            Two, One, new MultiEntityCommandOptions<First, Second>(commandTimeout: 62), CancellationToken.None);

        Assert.Equal(1, first.FirstId);
        Assert.Equal(2, second.SecondId);
        Assert.Equal(62, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_SqlAndMultiEntityOptions()
    {
        using var connection = Open();

        (First, Second)? row = await connection.QueryFirstOrDefaultAsync<First, Second>(
            "SELECT 1 AS FirstId, 2 AS SecondId",
            new MultiEntityCommandOptions<First, Second>(commandTimeout: 63),
            CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(63, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QuerySingleAsync_SqlParametersAndMultiEntityOptions()
    {
        using var connection = Open();

        (First first, Second _) = await connection.QuerySingleAsync<First, Second>(
            Two, One, new MultiEntityCommandOptions<First, Second>(commandTimeout: 64), CancellationToken.None);

        Assert.Equal(1, first.FirstId);
        Assert.Equal(64, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_SqlAndMultiEntityOptions()
    {
        using var connection = Open();

        (First, Second)? row = await connection.QuerySingleOrDefaultAsync<First, Second>(
            "SELECT 1 AS FirstId, 2 AS SecondId",
            new MultiEntityCommandOptions<First, Second>(commandTimeout: 65),
            CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(65, connection.ExecutedTimeout);
    }

    // ------------------------------------------------------------------
    // Arity 3.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Arity3_QueryAsync_SqlParametersAndToken()
    {
        using var connection = Open();

        List<(First, Second, Third)> rows =
            await connection.QueryAsync<First, Second, Third>(Three, One, CancellationToken.None);

        Assert.Equal(3, Assert.Single(rows).Item3.ThirdId);
    }

    [Fact]
    public async Task Arity3_QueryFirstOrDefaultAsync_SqlParametersAndToken()
    {
        using var connection = Open();

        (First, Second, Third)? row =
            await connection.QueryFirstOrDefaultAsync<First, Second, Third>(Three, One, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(3, row!.Value.Item3.ThirdId);
    }

    [Fact]
    public async Task Arity3_QuerySingleAsync_SqlParametersAndToken()
    {
        using var connection = Open();

        (First first, Second _, Third third) =
            await connection.QuerySingleAsync<First, Second, Third>(Three, One, CancellationToken.None);

        Assert.Equal(1, first.FirstId);
        Assert.Equal(3, third.ThirdId);
    }

    [Fact]
    public async Task Arity3_QuerySingleOrDefaultAsync_SqlParametersAndToken()
    {
        using var connection = Open();

        (First, Second, Third)? row =
            await connection.QuerySingleOrDefaultAsync<First, Second, Third>(Three, One, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(2, row!.Value.Item2.SecondId);
    }

    [Fact]
    public async Task Arity3_QuerySingleOrDefaultAsync_SqlAndToken()
    {
        using var connection = Open();

        (First, Second, Third)? row =
            await connection.QuerySingleOrDefaultAsync<First, Second, Third>(ThreeNoParams, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(1, row!.Value.Item1.FirstId);
    }

    // ------------------------------------------------------------------
    // Controls: the parameters really are bound on the shapes that take them.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Arity2_TheParametersAreBound()
    {
        using var connection = Open();

        List<(First, Second)> rows =
            await connection.QueryAsync<First, Second>(Two, new { One = 0 }, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Arity3_TheParametersAreBound()
    {
        using var connection = Open();

        List<(First, Second, Third)> rows =
            await connection.QueryAsync<First, Second, Third>(Three, new { One = 0 }, CancellationToken.None);

        Assert.Empty(rows);
    }
}
