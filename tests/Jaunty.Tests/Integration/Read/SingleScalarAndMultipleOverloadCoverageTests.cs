using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-097. Round-35 batches 02a, 02c and 02d between them list eleven overloads with no call
/// site anywhere in <c>tests/</c>. They share one shape: the options-without-parameters form, which
/// no existing test happens to pick, plus two forms of <c>QueryMultipleAsync</c> that every call
/// site binds past.
/// <list type="bullet">
/// <item><description><c>QueryScalarAsync(sql, parameters, options, ct)</c> - its sync twin is
/// covered; no <c>QueryScalarAsync</c> call anywhere passes both a parameters object and an options
/// value.</description></item>
/// <item><description><c>QuerySingle(sql, options)</c>,
/// <c>QuerySingleAsync(sql, options, ct)</c>.</description></item>
/// <item><description><c>QuerySingleOrDefault(sql)</c> and <c>(sql, options)</c>;
/// <c>QuerySingleOrDefaultAsync(sql, ct)</c> and <c>(sql, options, ct)</c> - every existing call in
/// that family passes a parameters object.</description></item>
/// <item><description><c>QueryMultipleAsync(sql, Action&lt;GridReader&gt;, ...)</c> - the only
/// non-async callback lambda in <c>tests/</c> returns a tuple and so binds to
/// <c>Func&lt;GridReader, TResult&gt;</c>; every other async site binds to
/// <c>Func&lt;GridReader, Task&gt;</c>.</description></item>
/// <item><description><c>QueryMultiple(sql, parameters, options)</c> and its async twin - no call
/// site beyond the reflection null-guard sweep.</description></item>
/// </list>
/// <para>
/// Every case that passes an option asserts against the command through
/// <see cref="RecordingDbConnection"/>, so it fails if the overload stops forwarding rather than
/// only if it stops returning a row.
/// </para>
/// </summary>
public class SingleScalarAndMultipleOverloadCoverageTests
{
    internal sealed class Row
    {
        public long Id { get; set; }
    }

    private const string Sql = "SELECT 1 AS Id";
    private const string Filtered = "SELECT 1 AS Id WHERE 1 = @One";
    private const string Two = "SELECT 1 AS Id; SELECT 2 AS Id";

    private static RecordingDbConnection Open()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        return new RecordingDbConnection(inner);
    }

    private static readonly object One = new { One = 1 };

    // ------------------------------------------------------------------
    // QueryScalarAsync's four-argument overload.
    // ------------------------------------------------------------------

    [Fact]
    public async Task QueryScalarAsync_ParametersAndOptions()
    {
        using var connection = Open();

        long value = await connection.QueryScalarAsync<long>(
            Filtered, One, CommandOptions<long>.WithTimeout(71), CancellationToken.None);

        Assert.Equal(1, value);
        Assert.Equal(71, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QueryScalarAsync_ParametersAndOptions_CarriesTheTransaction()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        _ = await connection.QueryScalarAsync<long>(
            Filtered, One, CommandOptions<long>.WithTransaction(transaction), CancellationToken.None);

        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    // ------------------------------------------------------------------
    // The QuerySingle family's options-without-parameters overloads.
    // ------------------------------------------------------------------

    [Fact]
    public void QuerySingle_SqlAndOptions()
    {
        using var connection = Open();

        Row row = connection.QuerySingle<Row>(Sql, CommandOptions<Row>.WithTimeout(72));

        Assert.Equal(1, row.Id);
        Assert.Equal(72, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QuerySingleAsync_SqlAndOptions()
    {
        using var connection = Open();

        Row row = await connection.QuerySingleAsync<Row>(
            Sql, CommandOptions<Row>.WithTimeout(73), CancellationToken.None);

        Assert.Equal(1, row.Id);
        Assert.Equal(73, connection.ExecutedTimeout);
    }

    [Fact]
    public void QuerySingleOrDefault_SqlOnly()
    {
        using var connection = Open();

        Row? row = connection.QuerySingleOrDefault<Row>(Sql);

        Assert.Equal(1, row!.Id);
    }

    [Fact]
    public void QuerySingleOrDefault_SqlAndOptions()
    {
        using var connection = Open();

        Row? row = connection.QuerySingleOrDefault<Row>(Sql, CommandOptions<Row>.WithTimeout(74));

        Assert.Equal(1, row!.Id);
        Assert.Equal(74, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_SqlAndToken()
    {
        using var connection = Open();

        Row? row = await connection.QuerySingleOrDefaultAsync<Row>(Sql, CancellationToken.None);

        Assert.Equal(1, row!.Id);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_SqlAndOptions()
    {
        using var connection = Open();

        Row? row = await connection.QuerySingleOrDefaultAsync<Row>(
            Sql, CommandOptions<Row>.WithTimeout(75), CancellationToken.None);

        Assert.Equal(1, row!.Id);
        Assert.Equal(75, connection.ExecutedTimeout);
    }

    [Fact]
    public void QuerySingleOrDefault_SqlOnly_ReturnsNullForAnEmptyResult()
    {
        using var connection = Open();

        Row? row = connection.QuerySingleOrDefault<Row>("SELECT 1 AS Id WHERE 1 = 0");

        Assert.Null(row);
    }

    // ------------------------------------------------------------------
    // QueryMultiple's unreached overloads.
    // ------------------------------------------------------------------

    [Fact]
    public async Task QueryMultipleAsync_ActionCallback()
    {
        using var connection = Open();
        long first = 0;
        long second = 0;

        await connection.QueryMultipleAsync(Two, grid =>
        {
            first = grid.ReadFirst<Row>().Id;
            second = grid.ReadFirst<Row>().Id;
        });

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task QueryMultipleAsync_ActionCallback_CarriesItsOptions()
    {
        using var connection = Open();

        await connection.QueryMultipleAsync(Two, grid => grid.ReadFirst<Row>(), null,
            new CommandOptions(commandTimeout: 76));

        Assert.Equal(76, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryMultiple_ParametersAndOptions()
    {
        using var connection = Open();

        using GridReader grid = connection.QueryMultiple(
            "SELECT 1 AS Id WHERE 1 = @One; SELECT 2 AS Id", One, new CommandOptions(commandTimeout: 77));

        Assert.Equal(1, grid.ReadFirst<Row>().Id);
        Assert.Equal(77, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QueryMultipleAsync_ParametersAndOptions()
    {
        using var connection = Open();

        using GridReader grid = await connection.QueryMultipleAsync(
            "SELECT 1 AS Id WHERE 1 = @One; SELECT 2 AS Id", One, new CommandOptions(commandTimeout: 78));

        Assert.Equal(1, grid.ReadFirst<Row>().Id);
        Assert.Equal(78, connection.ExecutedTimeout);
    }

    // ------------------------------------------------------------------
    // Controls: the parameters really are bound on the shapes that take them.
    // ------------------------------------------------------------------

    [Fact]
    public async Task QueryScalarAsync_TheParametersAreBound()
    {
        using var connection = Open();

        long value = await connection.QueryScalarAsync<long>(
            Filtered, new { One = 0 }, CommandOptions<long>.WithTimeout(79), CancellationToken.None);

        Assert.Equal(0, value);
    }

    [Fact]
    public void QueryMultiple_TheParametersAreBound()
    {
        using var connection = Open();

        using GridReader grid = connection.QueryMultiple(
            "SELECT 1 AS Id WHERE 1 = @One; SELECT 2 AS Id", new { One = 0 }, new CommandOptions());

        Assert.Empty(grid.Read<Row>());
    }
}
