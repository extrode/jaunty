using System.Data;
using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-083 and AUD-R35-084, both open since round 34.
/// <para>
/// (a) The three-argument <c>(connection, sql, CommandOptions&lt;T&gt;)</c> overload - options with no
/// parameters object - was called by no test on any of the four <c>QueryFirst</c> family members;
/// every existing call site uses the four-argument form, and <c>ParametersNullGuardTests</c>'
/// reflection sweep requires a non-optional <c>object parameters</c> at position 2, which these
/// overloads lack. What coverage there was asserted only <c>product.ProductId == 1</c> after
/// passing <c>WithTimeout(30)</c> - a value the query returns whether or not any option reached the
/// <see cref="IDbCommand"/>.
/// </para>
/// <para>
/// (b) The <c>connection is not DbConnection</c> guard - <c>"Async connection requires a
/// DbConnection or its subclass"</c> - appears in all eight overloads across
/// <c>QueryFirstAsync.cs</c> and <c>QueryFirstOrDefaultAsync.cs</c> and no test reached it; every
/// call site passes a real <see cref="DbConnection"/>, and the only
/// <see cref="IDbConnectionWrapper"/> coverage exercises the arity-2 multi-entity copies of the
/// guard in a different file.
/// </para>
/// </summary>
public class QueryFirstOptionPlumbingTests
{
    internal sealed class Row
    {
        public long Id { get; set; }
    }

    private const string Sql = "SELECT 1 AS Id";

    private static RecordingDbConnection Open()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        return new RecordingDbConnection(inner);
    }

    // ------------------------------------------------------------------
    // (a) the three-argument overload, asserted against the command rather than the row.
    // ------------------------------------------------------------------

    [Fact]
    public void QueryFirst_WithOptionsOnly_AssignsTheTimeout()
    {
        using var connection = Open();

        Row row = connection.QueryFirst<Row>(Sql, CommandOptions<Row>.WithTimeout(37));

        Assert.Equal(1, row.Id);
        Assert.Equal(37, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryFirst_WithOptionsOnly_AssignsTheTransaction()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        Row row = connection.QueryFirst<Row>(Sql, CommandOptions<Row>.WithTransaction(transaction));

        Assert.Equal(1, row.Id);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public async Task QueryFirstAsync_WithOptionsOnly_AssignsTheTimeout()
    {
        using var connection = Open();

        Row row = await connection.QueryFirstAsync<Row>(Sql, CommandOptions<Row>.WithTimeout(38));

        Assert.Equal(1, row.Id);
        Assert.Equal(38, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QueryFirstAsync_WithOptionsOnly_AssignsTheTransaction()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        Row row = await connection.QueryFirstAsync<Row>(Sql, CommandOptions<Row>.WithTransaction(transaction));

        Assert.Equal(1, row.Id);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public void QueryFirstOrDefault_WithOptionsOnly_AssignsTheTimeout()
    {
        using var connection = Open();

        Row? row = connection.QueryFirstOrDefault<Row>(Sql, CommandOptions<Row>.WithTimeout(39));

        Assert.Equal(1, row!.Id);
        Assert.Equal(39, connection.ExecutedTimeout);
    }

    [Fact]
    public void QueryFirstOrDefault_WithOptionsOnly_AssignsTheTransaction()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        Row? row = connection.QueryFirstOrDefault<Row>(Sql, CommandOptions<Row>.WithTransaction(transaction));

        Assert.Equal(1, row!.Id);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_WithOptionsOnly_AssignsTheTimeout()
    {
        using var connection = Open();

        Row? row = await connection.QueryFirstOrDefaultAsync<Row>(Sql, CommandOptions<Row>.WithTimeout(40));

        Assert.Equal(1, row!.Id);
        Assert.Equal(40, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_WithOptionsOnly_AssignsTheTransaction()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        Row? row = await connection.QueryFirstOrDefaultAsync<Row>(Sql, CommandOptions<Row>.WithTransaction(transaction));

        Assert.Equal(1, row!.Id);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public void QueryFirst_WithOptionsOnly_CarriesTheMapper()
    {
        using var connection = Open();

        Row row = connection.QueryFirst(Sql, CommandOptions<Row>.WithMapper(r => new Row { Id = 99 }));

        Assert.Equal(99, row.Id);
    }

    [Fact]
    public async Task QueryFirstAsync_WithOptionsOnly_CarriesTheMapper()
    {
        using var connection = Open();

        Row row = await connection.QueryFirstAsync(Sql, CommandOptions<Row>.WithMapper(r => new Row { Id = 98 }));

        Assert.Equal(98, row.Id);
    }

    // ------------------------------------------------------------------
    // (b) the non-DbConnection guard on all eight async overloads.
    // ------------------------------------------------------------------

    private static IDbConnection OpenWrapped()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        return new IDbConnectionWrapper(inner);
    }

    private static async Task AssertRequiresDbConnection(Func<IDbConnection, Task> call)
    {
        IDbConnection connection = OpenWrapped();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => call(connection));
        Assert.Contains("DbConnection", ex.Message);
    }

    [Fact]
    public Task QueryFirstAsync_Sql_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstAsync<Row>(Sql).AsTask());

    [Fact]
    public Task QueryFirstAsync_SqlAndParameters_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstAsync<Row>(Sql, new { }).AsTask());

    [Fact]
    public Task QueryFirstAsync_SqlAndOptions_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstAsync<Row>(Sql, CommandOptions<Row>.WithTimeout(30)).AsTask());

    [Fact]
    public Task QueryFirstAsync_SqlParametersAndOptions_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstAsync<Row>(Sql, new { }, CommandOptions<Row>.WithTimeout(30)).AsTask());

    [Fact]
    public Task QueryFirstOrDefaultAsync_Sql_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstOrDefaultAsync<Row>(Sql).AsTask());

    [Fact]
    public Task QueryFirstOrDefaultAsync_SqlAndParameters_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstOrDefaultAsync<Row>(Sql, new { }).AsTask());

    [Fact]
    public Task QueryFirstOrDefaultAsync_SqlAndOptions_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstOrDefaultAsync<Row>(Sql, CommandOptions<Row>.WithTimeout(30)).AsTask());

    [Fact]
    public Task QueryFirstOrDefaultAsync_SqlParametersAndOptions_RequiresADbConnection()
        => AssertRequiresDbConnection(c => c.QueryFirstOrDefaultAsync<Row>(Sql, new { }, CommandOptions<Row>.WithTimeout(30)).AsTask());

    // ------------------------------------------------------------------
    // Control: a real DbConnection goes through all four of those shapes.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ARealDbConnection_StillRunsEveryAsyncShape()
    {
        using var connection = Open();

        Assert.Equal(1, (await connection.QueryFirstAsync<Row>(Sql)).Id);
        Assert.Equal(1, (await connection.QueryFirstAsync<Row>(Sql, new { })).Id);
        Assert.Equal(1, (await connection.QueryFirstOrDefaultAsync<Row>(Sql))!.Id);
        Assert.Equal(1, (await connection.QueryFirstOrDefaultAsync<Row>(Sql, new { }))!.Id);
    }
}
