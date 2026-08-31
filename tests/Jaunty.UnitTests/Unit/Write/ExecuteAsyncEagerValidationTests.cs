using System.Data;
using System.Data.Common;

using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R25 (B2-1): the four <c>ExecuteAsync</c> overloads and both <c>ExecuteBatchAsync</c>
/// overloads performed no argument validation at all - they went straight to the
/// <c>connection is not DbConnection</c> type test.
///
/// <para>
/// Every sync sibling validates eagerly (<c>Execute</c> checks connection/sql/whitespace-sql and,
/// on the parameterised overloads, parameters; <c>ExecuteBatch</c> checks connection/sql/
/// whitespace-sql/parameterSets), and so does every other async entry point in the library -
/// <c>QueryAsync</c>, <c>QueryScalarAsync</c>, <c>GetAllAsync</c>, <c>QueryMultipleAsync</c>,
/// <c>InsertAsync</c>, <c>BulkInsertAsync</c> - which is the pattern the <c>*EagerValidationTests</c>
/// family already pins for the read side.
/// </para>
///
/// <para>Three consequences, one per region below:</para>
/// <list type="number">
/// <item><description>
/// A null connection reported <c>InvalidOperationException("Async connection requires a DbConnection
/// or its subclass")</c> instead of <c>ArgumentNullException("connection")</c>, because the type test
/// ran before any null check.
/// </description></item>
/// <item><description>
/// For <c>ExecuteAsync</c> the sql checks existed only inside <c>ExecuteNonQueryCoreAsync</c>, so
/// they surfaced as a faulted task on await rather than a synchronous throw at the call site.
/// </description></item>
/// <item><description>
/// <c>ExecuteBatchAsync</c> had no validation on any layer: <c>ExecuteBatchCoreAsync</c> does not
/// validate either, so a null <c>parameterSets</c> reached <c>foreach</c> and threw a bare
/// <see cref="NullReferenceException"/>, and a null or blank sql was assigned straight to
/// <c>command.CommandText</c>.
/// </description></item>
/// </list>
///
/// <para>
/// These overloads are not declared <c>async</c>, so the validation throws synchronously and
/// <c>Assert.Throws(() =&gt; ...)</c> without an await is itself the eagerness assertion.
/// </para>
/// </summary>
public class ExecuteAsyncEagerValidationTests
{
    private const string Sql = "UPDATE products SET price = 1";

    // A stub rather than a real provider connection: every call under test throws before the
    // connection is touched, and System.Data.SQLite's constructor needs a native interop library
    // that is unavailable on some platforms, which would make these tests fail for a reason
    // unrelated to what they assert.
    private static StubDbConnection Unopened() => new();

    // ---------------------------------------------------------------------------
    // (1) Null connection: ArgumentNullException, not the DbConnection type-test message
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecuteAsync_NullConnection_ThrowsArgumentNullExceptionNotInvalidOperation()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() => nullConnection!.ExecuteAsync(Sql));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithParameters_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() => nullConnection!.ExecuteAsync(Sql, new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithOptions_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() => nullConnection!.ExecuteAsync(Sql, new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithParametersAndOptions_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.ExecuteAsync(Sql, new { Id = 1 }, new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteBatchAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.ExecuteBatchAsync(Sql, [new { Id = 1 }]));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteBatchAsync_WithOptions_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.ExecuteBatchAsync(Sql, [new { Id = 1 }], new CommandOptions()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_NonDbConnection_StillThrowsInvalidOperationException()
    {
        // Ordering matters both ways: adding the null checks must not swallow the type test that
        // tells a caller their IDbConnection implementation cannot do async.
        var ex = Assert.Throws<InvalidOperationException>(() => new NonDbConnection().ExecuteAsync(Sql));

        Assert.Contains("DbConnection", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // (2) sql: thrown at the call site, not deferred into the returned ValueTask
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecuteAsync_NullSql_ThrowsEagerly()
    {
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() => connection.ExecuteAsync(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  \t\n  ")]
    public void ExecuteAsync_BlankSql_ThrowsEagerly(string sql)
    {
        using var connection = Unopened();

        var ex = Assert.ThrowsAny<ArgumentException>(() => connection.ExecuteAsync(sql));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithParameters_NullSql_ThrowsEagerly()
    {
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() => connection.ExecuteAsync(null!, new { Id = 1 }));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithOptions_BlankSql_ThrowsEagerly()
    {
        using var connection = Unopened();

        var ex = Assert.ThrowsAny<ArgumentException>(() => connection.ExecuteAsync("   ", new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithParametersAndOptions_BlankSql_ThrowsEagerly()
    {
        using var connection = Unopened();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.ExecuteAsync("   ", new { Id = 1 }, new CommandOptions()));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_NullParameters_ThrowsEagerly()
    {
        // The sync Execute(connection, sql, object parameters) overload checks this; its async
        // counterpart did not.
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() => connection.ExecuteAsync(Sql, (object)null!));

        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void ExecuteAsync_WithOptions_NullParameters_ThrowsEagerly()
    {
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.ExecuteAsync(Sql, null!, new CommandOptions()));

        Assert.Equal("parameters", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // (3) ExecuteBatchAsync had no validation on any layer
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecuteBatchAsync_NullParameterSets_ThrowsArgumentNullExceptionNotNullReference()
    {
        // Previously reached "foreach (var parameters in parameterSets)" inside the core and threw
        // a bare NullReferenceException - after opening the connection.
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() => connection.ExecuteBatchAsync(Sql, null!));

        Assert.Equal("parameterSets", ex.ParamName);
    }

    [Fact]
    public void ExecuteBatchAsync_WithOptions_NullParameterSets_ThrowsArgumentNullException()
    {
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.ExecuteBatchAsync(Sql, null!, new CommandOptions()));

        Assert.Equal("parameterSets", ex.ParamName);
    }

    [Fact]
    public void ExecuteBatchAsync_NullSql_ThrowsEagerly()
    {
        // Previously assigned straight to command.CommandText.
        using var connection = Unopened();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.ExecuteBatchAsync(null!, [new { Id = 1 }]));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void ExecuteBatchAsync_BlankSql_ThrowsEagerly()
    {
        using var connection = Unopened();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            connection.ExecuteBatchAsync("   ", [new { Id = 1 }]));

        Assert.Equal("sql", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // Sync/async parity - the point of the finding
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecuteAsync_AndExecute_RejectTheSameBadSqlTheSameWay()
    {
        using var connection = Unopened();

        var fromSync = Assert.ThrowsAny<ArgumentException>(() => connection.Execute("   "));
        var fromAsync = Assert.ThrowsAny<ArgumentException>(() => connection.ExecuteAsync("   "));

        Assert.Equal(fromSync.GetType(), fromAsync.GetType());
        Assert.Equal(fromSync.ParamName, fromAsync.ParamName);
    }

    [Fact]
    public void ExecuteBatchAsync_AndExecuteBatch_RejectNullParameterSetsTheSameWay()
    {
        using var connection = Unopened();

        var fromSync = Assert.Throws<ArgumentNullException>(() => connection.ExecuteBatch(Sql, null!));
        var fromAsync = Assert.Throws<ArgumentNullException>(() => connection.ExecuteBatchAsync(Sql, null!));

        Assert.Equal(fromSync.ParamName, fromAsync.ParamName);
    }

    // ---------------------------------------------------------------------------

    /// <summary>A DbConnection that satisfies the async type test and nothing else.</summary>
    // ConnectionString is [AllowNull] on DbConnection; the attribute is not public on net472, so
    // the override is a plain non-nullable string and the mismatch is silenced rather than forked.
#pragma warning disable CS8765
    private sealed class StubDbConnection : DbConnection
    {
        public override string ConnectionString { get => ""; set { } }
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() => throw new NotSupportedException("no test here should reach Open");
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException("no test here should reach CreateCommand");
    }
#pragma warning restore CS8765

    /// <summary>An IDbConnection that is deliberately not a DbConnection, so async is impossible.</summary>
    private sealed class NonDbConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
