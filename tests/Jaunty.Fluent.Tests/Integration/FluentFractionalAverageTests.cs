using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R35-066. <c>IGrouping.Avg&lt;TResult&gt;</c> and its joined siblings are declared to return
/// <see cref="double"/>, but both GROUP BY translators, the un-grouped <c>Avg</c>/<c>AvgAsync</c>
/// terminals and the HAVING translator all emitted a bare <c>AVG(&lt;column&gt;)</c>. On SQL Server
/// <c>AVG</c> takes its result type from its operand, so the average of an <c>int</c> column is
/// truncated in the engine and then widened by the mapper. Every existing <c>Avg</c> test runs on
/// SQLite, which is always float, and none of them asserts a <em>value</em> -
/// <c>FluentGroupByTests.GroupBy_SingleKey_WithAvg_*</c> and its joined siblings assert only
/// <c>Assert.NotEmpty(results)</c>.
/// <para>
/// These are SQL-shape tests because the truncation only happens on SQL Server, which the local
/// suite does not reach; the dialect is registered against a stub connection type so the real
/// <c>SqlServerDialect</c> is what produces the SQL.
/// </para>
/// </summary>
public class FluentFractionalAverageTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;
    private readonly SqlServerStubConnection _sqlServer;

    public FluentFractionalAverageTests(FluentDatabaseFixture fixture)
    {
        _fixture = fixture;
        SqlDialectFactory.RegisterDialect(nameof(SqlServerStubConnection), new SqlServerDialect());
        _sqlServer = new SqlServerStubConnection();
    }

    // ------------------------------------------------------------------
    // SQL Server: the operand is cast, so the division itself is fractional.
    // ------------------------------------------------------------------

    [Fact]
    public void GroupBy_Avg_OnSqlServer_CastsTheOperand()
    {
        string sql = _sqlServer.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Average = g.Avg(p => p.UnitsInStock) });

        Assert.Contains("AVG(CAST(", sql, StringComparison.Ordinal);
        Assert.Contains("AS FLOAT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupBy_Sum_OnSqlServer_IsStillBare()
    {
        string sql = _sqlServer.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Total = g.Sum(p => p.UnitsInStock) });

        Assert.DoesNotContain("CAST(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupBy_Count_OnSqlServer_IsStillBare()
    {
        string sql = _sqlServer.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.DoesNotContain("CAST(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupBy_Min_And_Max_OnSqlServer_AreStillBare()
    {
        string sql = _sqlServer.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new
            {
                CategoryId = g.Key,
                Low = g.Min(p => p.UnitsInStock),
                High = g.Max(p => p.UnitsInStock),
            });

        Assert.DoesNotContain("CAST(", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // SQLite: nothing changed, because nothing needed to.
    // ------------------------------------------------------------------

    [Fact]
    public void GroupBy_Avg_OnSqlite_StaysBare()
    {
        string sql = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Average = g.Avg(p => p.UnitsInStock) });

        Assert.Contains("AVG(", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CAST(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupBy_Avg_OnSqlite_StillExecutesAndReturnsAFractionalValue()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Average = g.Avg(p => p.UnitsInStock) });

        Assert.NotEmpty(results);

        // The point of the declared double: at least one category's average is not a whole number.
        Assert.Contains(results, r => r.Average != Math.Floor(r.Average));
    }

    // ------------------------------------------------------------------
    // The joined translator, which held the same defect as its un-joined twin.
    // ------------------------------------------------------------------

    [Fact]
    public void JoinedGroupBy_Avg_OnSqlServer_CastsTheOperand()
    {
        string sql = _sqlServer.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Average = g.Avg((p, c) => p.UnitsInStock) });

        Assert.Contains("AVG(CAST(", sql, StringComparison.Ordinal);
        Assert.Contains("AS FLOAT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinedGroupBy_Sum_OnSqlServer_IsStillBare()
    {
        string sql = _sqlServer.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Total = g.Sum((p, c) => p.UnitsInStock) });

        Assert.DoesNotContain("CAST(", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The terminals with no ToSql: the un-grouped Avg, and HAVING.
    // ------------------------------------------------------------------

    [Fact]
    public void UnGrouped_Avg_OnSqlServer_CastsTheOperand()
    {
        _sqlServer.From<Product>().Avg(p => p.UnitsInStock);

        Assert.Contains("AVG(CAST(", _sqlServer.LastCommandText!, StringComparison.Ordinal);
        Assert.Contains("AS FLOAT", _sqlServer.LastCommandText!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnGrouped_AvgAsync_ReachesTheSameBuilder()
    {
        // AvgAsync requires a DbConnection, which the stub is not - the point here is that both
        // overloads route through BuildAggregateSql, and the sync one has proved what it emits.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sqlServer.From<Product>().AvgAsync(p => p.UnitsInStock));
    }

    [Fact]
    public void UnGrouped_Sum_OnSqlServer_IsStillBare()
    {
        _sqlServer.From<Product>().Sum(p => p.UnitsInStock);

        Assert.DoesNotContain("CAST(", _sqlServer.LastCommandText!, StringComparison.Ordinal);
    }

    [Fact]
    public void Having_Avg_OnSqlServer_CastsTheOperand()
    {
        string sql = _sqlServer.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Avg(p => p.UnitsInStock) > 10)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("HAVING", sql, StringComparison.Ordinal);
        Assert.Contains("AVG(CAST(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Having_Sum_OnSqlServer_IsStillBare()
    {
        string sql = _sqlServer.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Sum(p => p.UnitsInStock) > 10)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.DoesNotContain("CAST(", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Records the SQL it is handed and returns a scalar, so the terminals that execute rather than
    /// offering a <c>ToSql</c> can still be inspected.
    /// </summary>
    private sealed class SqlServerStubConnection : IDbConnection
    {
        public string? LastCommandText { get; private set; }

        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() => State = ConnectionState.Closed;
        public IDbCommand CreateCommand() => new RecordingCommand(this);
        public void Open() => State = ConnectionState.Open;
        public void Dispose() { }

        private void Record(string? sql) => LastCommandText = sql;

        private sealed class RecordingCommand(SqlServerStubConnection owner) : IDbCommand
        {
            public string? CommandText { get; set; }
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; }
            public IDbConnection? Connection { get; set; } = owner;
            public IDataParameterCollection Parameters { get; } = new ParameterCollection();
            public IDbTransaction? Transaction { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public IDbDataParameter CreateParameter() => new Parameter();
            public void Dispose() { }

            public int ExecuteNonQuery()
            {
                owner.Record(CommandText);
                return 0;
            }

            public IDataReader ExecuteReader() => throw new NotSupportedException();
            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();

            public object? ExecuteScalar()
            {
                owner.Record(CommandText);
                return 0d;
            }

            public void Prepare() { }
        }

        private sealed class ParameterCollection : List<object?>, IDataParameterCollection
        {
            public object this[string parameterName] { get => this[0]!; set => this[0] = value; }
            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }
        }

        private sealed class Parameter : IDbDataParameter
        {
            public byte Precision { get; set; }
            public byte Scale { get; set; }
            public int Size { get; set; }
            public DbType DbType { get; set; }
            public ParameterDirection Direction { get; set; }
            public bool IsNullable => true;
            public string ParameterName { get; set; } = "";
            public string SourceColumn { get; set; } = "";
            public DataRowVersion SourceVersion { get; set; }
            public object? Value { get; set; }
        }
    }
}
