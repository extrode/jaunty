using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-085, open since round 34. Eleven of the arity-2 multi-entity overloads had no functional
/// test: five of the twelve <c>MultiEntityCommandOptions&lt;T1, T2&gt;</c> shapes
/// (<c>QueryFirst(sql, parameters, options)</c>, <c>QueryFirstOrDefault(sql, options)</c>,
/// <c>QuerySingle(sql, parameters, options)</c>, <c>QuerySingleOrDefault(sql, options)</c>,
/// <c>QueryStream(sql, parameters, options)</c>), and all six
/// <c>(sql, parameters, CommandOptions&lt;(T1, T2)&gt;)</c> shapes - the mapper-capable half of the
/// pair, which the existing custom-mapper tests reach only through the two-argument form.
/// <para>
/// Each case asserts against the command via <see cref="RecordingDbConnection"/> as well as against
/// the row, so it fails if the overload stops forwarding its options rather than only if it stops
/// returning data - the vacuous shape these tests exist to avoid.
/// </para>
/// </summary>
public class MultiEntityArity2OverloadCoverageTests
{
    internal sealed class Left
    {
        public long LeftId { get; set; }
    }

    internal sealed class Right
    {
        public long RightId { get; set; }
    }

    private const string Sql = "SELECT 1 AS LeftId, 2 AS RightId";
    private const string Filtered = "SELECT 1 AS LeftId, 2 AS RightId WHERE 1 = @One";

    private static RecordingDbConnection Open()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        return new RecordingDbConnection(inner);
    }

    // ------------------------------------------------------------------
    // The five untested MultiEntityCommandOptions<T1, T2> shapes.
    // ------------------------------------------------------------------

    [Fact]
    public void QueryFirst_ParametersAndMultiEntityOptions()
    {
        using var connection = Open();

        (Left left, Right right) = connection.QueryFirst<Left, Right>(
            Filtered, new { One = 1 }, new MultiEntityCommandOptions<Left, Right>(commandTimeout: 41));

        Assert.Equal(1, left.LeftId);
        Assert.Equal(2, right.RightId);
        Assert.Equal(41, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryFirstOrDefault_MultiEntityOptions()
    {
        using var connection = Open();

        (Left, Right)? row = connection.QueryFirstOrDefault<Left, Right>(
            Sql, new MultiEntityCommandOptions<Left, Right>(commandTimeout: 42));

        Assert.NotNull(row);
        Assert.Equal(1, row!.Value.Item1.LeftId);
        Assert.Equal(42, connection.ExecutedTimeout);
    }

    [Fact]
    public void QuerySingle_ParametersAndMultiEntityOptions()
    {
        using var connection = Open();

        (Left left, Right right) = connection.QuerySingle<Left, Right>(
            Filtered, new { One = 1 }, new MultiEntityCommandOptions<Left, Right>(commandTimeout: 43));

        Assert.Equal(1, left.LeftId);
        Assert.Equal(2, right.RightId);
        Assert.Equal(43, connection.ExecutedTimeout);
    }

    [Fact]
    public void QuerySingleOrDefault_MultiEntityOptions()
    {
        using var connection = Open();

        (Left, Right)? row = connection.QuerySingleOrDefault<Left, Right>(
            Sql, new MultiEntityCommandOptions<Left, Right>(commandTimeout: 44));

        Assert.NotNull(row);
        Assert.Equal(2, row!.Value.Item2.RightId);
        Assert.Equal(44, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryStream_ParametersAndMultiEntityOptions()
    {
        using var connection = Open();

        List<(Left, Right)> rows = [.. connection.QueryStream<Left, Right>(
            Filtered, new { One = 1 }, new MultiEntityCommandOptions<Left, Right>(commandTimeout: 45))];

        Assert.Single(rows);
        Assert.Equal(1, rows[0].Item1.LeftId);
        Assert.Equal(45, connection.ExecutedTimeout);
    }

    // ------------------------------------------------------------------
    // The six untested (sql, parameters, CommandOptions<(T1, T2)>) shapes. The mapper is the point:
    // it is the half of the pair CommandOptions carries and MultiEntityCommandOptions does not.
    // ------------------------------------------------------------------

    private static CommandOptions<(Left, Right)> Mapper(int timeout) => new(
        mapper: _ => (new Left { LeftId = 90 }, new Right { RightId = 91 }),
        commandTimeout: timeout);

    [Fact]
    public void Query_ParametersAndCommandOptions()
    {
        using var connection = Open();

        List<(Left, Right)> rows = connection.Query<Left, Right>(Filtered, new { One = 1 }, Mapper(51));

        Assert.Equal(90, Assert.Single(rows).Item1.LeftId);
        Assert.Equal(51, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryFirst_ParametersAndCommandOptions()
    {
        using var connection = Open();

        (Left left, Right right) = connection.QueryFirst<Left, Right>(Filtered, new { One = 1 }, Mapper(52));

        Assert.Equal(90, left.LeftId);
        Assert.Equal(91, right.RightId);
        Assert.Equal(52, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryFirstOrDefault_ParametersAndCommandOptions()
    {
        using var connection = Open();

        (Left, Right)? row = connection.QueryFirstOrDefault<Left, Right>(Filtered, new { One = 1 }, Mapper(53));

        Assert.NotNull(row);
        Assert.Equal(90, row!.Value.Item1.LeftId);
        Assert.Equal(53, connection.ExecutedTimeout);
    }

    [Fact]
    public void QuerySingle_ParametersAndCommandOptions()
    {
        using var connection = Open();

        (Left left, Right right) = connection.QuerySingle<Left, Right>(Filtered, new { One = 1 }, Mapper(54));

        Assert.Equal(90, left.LeftId);
        Assert.Equal(54, connection.ExecutedTimeout);
    }

    [Fact]
    public void QuerySingleOrDefault_ParametersAndCommandOptions()
    {
        using var connection = Open();

        (Left, Right)? row = connection.QuerySingleOrDefault<Left, Right>(Filtered, new { One = 1 }, Mapper(55));

        Assert.NotNull(row);
        Assert.Equal(91, row!.Value.Item2.RightId);
        Assert.Equal(55, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryStream_ParametersAndCommandOptions()
    {
        using var connection = Open();

        List<(Left, Right)> rows = [.. connection.QueryStream<Left, Right>(Filtered, new { One = 1 }, Mapper(56))];

        Assert.Equal(90, Assert.Single(rows).Item1.LeftId);
        Assert.Equal(56, connection.ExecutedTimeout);
    }

    // ------------------------------------------------------------------
    // Controls: the parameters really are bound, and the transaction shape works on these overloads
    // too - so a green run above cannot be explained by the WHERE clause being ignored.
    // ------------------------------------------------------------------

    [Fact]
    public void TheParametersAreBound_NotIgnored()
    {
        using var connection = Open();

        List<(Left, Right)> rows = [.. connection.QueryStream<Left, Right>(
            Filtered, new { One = 0 }, new MultiEntityCommandOptions<Left, Right>(commandTimeout: 46))];

        Assert.Empty(rows);
    }

    [Fact]
    public void ATransactionReachesTheCommand_OnTheseOverloadsToo()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        _ = connection.QueryFirst<Left, Right>(
            Filtered, new { One = 1 }, new MultiEntityCommandOptions<Left, Right>(transaction: transaction));

        Assert.Same(transaction, connection.ExecutedTransaction);
    }
}
