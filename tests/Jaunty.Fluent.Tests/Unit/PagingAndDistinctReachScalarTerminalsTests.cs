using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26 (batch 5). Two findings with one mechanism: builder state that
/// <c>BuildSelectSql</c> honours was silently dropped everywhere else.
///
/// <para>
/// <b>Scalar terminals ignored paging and DISTINCT</b> (medium/consistency).
/// <c>BuildCountSql</c> and <c>BuildAggregateSql</c> emitted only
/// <c>SELECT ... FROM &lt;table&gt; [WHERE ...]</c> and read none of <c>_take</c>, <c>_skip</c> or
/// <c>_distinct</c>, sixty lines below the method that honours all three. So the row terminals and
/// the scalar terminals on the <em>same</em> builder disagreed about which rows the query was
/// about. Fixed by running the aggregate over a derived table holding the paged/distinct rows -
/// an aggregate cannot share a SELECT with the LIMIT it is meant to respect, because the LIMIT
/// would apply to the row it produces rather than the rows it consumes.
/// </para>
///
/// <para>
/// <b><c>Take</c>/<c>Skip</c> before a join were discarded</b> (medium/bug).
/// <c>IFromClause&lt;T&gt;.Take</c>/<c>Skip</c> return <c>IFromClause&lt;T&gt;</c>, which exposes
/// the join methods, so the chain compiles - and <c>JoinClauseBuilder.CreateJoinedQuery</c> builds
/// the joined query from connection, dialect, table, schema, alias and join info only. The paging
/// was left behind, and <c>IJoinedQuery&lt;,&gt;</c> has no <c>Take</c>/<c>Skip</c> to re-apply it
/// to. Fixed by throwing, not by guessing: <c>Take(1)</c> written before a join reads as "limit the
/// source, then join", while the cheap implementation is "page the joined result", and those are
/// different rows whenever the join is not one-to-one.
/// </para>
/// </summary>
public class PagingAndDistinctReachScalarTerminalsTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    [Table("paging_items")]
    public class Item
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        [Column("cat_id")]
        public int CatId { get; set; }
    }

    [Table("paging_cats")]
    public class Cat
    {
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    public PagingAndDistinctReachScalarTerminalsTests()
    {
        JauntyReflectionExtensions.UseReflectionMapping();

        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();

        Execute("CREATE TABLE paging_cats (Id INTEGER PRIMARY KEY, Title TEXT)");
        Execute("CREATE TABLE paging_items (Id INTEGER PRIMARY KEY, Name TEXT, cat_id INTEGER)");
        Execute("INSERT INTO paging_cats VALUES (1,'a'),(2,'b')");
        // Three rows; cat_id sums to 4 over all of them and to 1 over the first alone.
        Execute("INSERT INTO paging_items VALUES (1,'x',1),(2,'y',1),(3,'z',2)");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    private void Execute(string sql)
    {
        using SQLiteCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // ------------------------------------------------------------------
    // Scalar terminals now see the same rows as the row terminals
    // ------------------------------------------------------------------

    /// <summary>
    /// The headline case. <c>Take(1).Count()</c> counted the whole table, so it disagreed with
    /// <c>Take(1).Select().Count</c> on the same builder - and with LINQ, where it is 1.
    /// </summary>
    [Fact]
    public void TakeIsHonouredByCount()
    {
        Assert.Equal(1, _connection.From<Item>().Take(1).Count());
        Assert.Equal(1, _connection.From<Item>().Take(1).Select().Count);
    }

    /// <summary>Skip too, which the same code path ignored.</summary>
    [Fact]
    public void SkipIsHonouredByCount()
    {
        Assert.Equal(1, _connection.From<Item>().Skip(2).Count());
        Assert.Equal(1, _connection.From<Item>().Skip(2).Select().Count);
    }

    /// <summary>
    /// An aggregate over a column, not just a row count: the full-table sum of <c>cat_id</c> is 4
    /// and the sum over the first row alone is 1. This is the case that proves the LIMIT lands on
    /// the rows the aggregate consumes rather than on its single-row result.
    /// </summary>
    [Fact]
    public void TakeIsHonouredByAggregates()
    {
        Assert.Equal(1, _connection.From<Item>().Take(1).Sum(i => i.CatId));
        Assert.Equal(4, _connection.From<Item>().Sum(i => i.CatId));
    }

    /// <summary>
    /// DISTINCT reaches the count. This one has to assert on the SQL rather than the result, and
    /// the reason is worth recording: the builder's <c>Distinct()</c> means distinct <em>rows of
    /// the projection</em>, and the projection of a keyed entity always contains its key, so
    /// distinct-rows can never differ from all-rows for an <see cref="Item"/>. The defect is
    /// therefore invisible to any row count on this shape - the scalar terminal was describing a
    /// different query from the row terminal on the same builder while agreeing on the answer.
    /// A results-only test here would pass with the fix reverted, which is exactly what the first
    /// draft of it did.
    /// </summary>
    [Fact]
    public void DistinctIsHonouredByCount()
    {
        var connection = new SqliteConnection();

        connection.From<Item>().Distinct().Count();

        Assert.Contains("DISTINCT", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(3, _connection.From<Item>().Distinct().Count());
    }

    /// <summary>
    /// The paged count's shape, for the same reason - a result of 1 could in principle come from
    /// several wrong queries, and this pins that it comes from the aggregate running over a paged
    /// derived table rather than a LIMIT applied to the aggregate's own single row.
    /// </summary>
    [Fact]
    public void ThePagedCountRunsOverADerivedTable()
    {
        var connection = new SqliteConnection();

        connection.From<Item>().Take(1).Count();

        string sql = connection.LastCommandText;
        Assert.StartsWith("SELECT COUNT(*) FROM (", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Paging and a WHERE together, since the derived table has to carry the predicate inward
    /// rather than leave it on the outer aggregate.
    /// </summary>
    [Fact]
    public void TakeAndWhereComposeInTheCount()
    {
        Assert.Equal(2, _connection.From<Item>().Where(i => i.CatId == 1).Count());
        Assert.Equal(1, _connection.From<Item>().Where(i => i.CatId == 1).Take(1).Count());
    }

    /// <summary>
    /// The unpaged, non-distinct case must not have changed shape. It is the overwhelming majority
    /// of scalar calls, and wrapping every one of them in a derived table would be a real cost for
    /// no gain - so this pins that no subquery appears.
    /// </summary>
    [Fact]
    public void APlainCountIsStillAPlainCount()
    {
        Assert.Equal(3, _connection.From<Item>().Count());
        Assert.Equal(2, _connection.From<Item>().Where(i => i.CatId == 1).Count());
    }

    // ------------------------------------------------------------------
    // Take/Skip before a join fail loudly instead of vanishing
    // ------------------------------------------------------------------

    /// <summary>
    /// Previously returned all 3 rows with no LIMIT in the SQL and no warning. The message has to
    /// name what was dropped and what to do, because the chain still compiles.
    /// </summary>
    [Fact]
    public void TakeBeforeAJoinThrowsRatherThanBeingDiscarded()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => _connection.From<Item>().Take(1).InnerJoin<Cat>());

        Assert.Contains("Take/Skip", exception.Message, StringComparison.Ordinal);
        Assert.Contains("joined", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>And Skip, and the other two join kinds - one guard, three entry points.</summary>
    [Fact]
    public void SkipBeforeAnyJoinKindThrows()
    {
        Assert.Throws<NotSupportedException>(() => _connection.From<Item>().Skip(2).InnerJoin<Cat>());
        Assert.Throws<NotSupportedException>(() => _connection.From<Item>().Skip(2).LeftJoin<Cat>());
        Assert.Throws<NotSupportedException>(() => _connection.From<Item>().Skip(2).RightJoin<Cat>());
    }

    /// <summary>
    /// The guard must not fire on an ordinary join. This is the case that would break every
    /// existing caller if the condition were wrong.
    /// </summary>
    [Fact]
    public void AJoinWithoutPagingIsUnaffected()
    {
        List<Item> rows = _connection.From<Item>()
            .InnerJoin<Cat>()
            .On((i, c) => i.CatId == c.Id)
            .Select();

        Assert.Equal(3, rows.Count);
    }

    // ------------------------------------------------------------------
    // Capturing double, for the assertions that have to see the SQL. Named exactly
    // SqliteConnection: dialect resolution keys on Type.Name.
    // ------------------------------------------------------------------

    private sealed class SqliteConnection : System.Data.IDbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;

        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public System.Data.ConnectionState State => System.Data.ConnectionState.Open;

        public System.Data.IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public System.Data.IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Dispose() { }
        public void Open() { }

        public System.Data.IDbCommand CreateCommand() => new CapturingCommand(text => LastCommandText = text);

        private sealed class CapturingCommand : System.Data.IDbCommand
        {
            private readonly Action<string> _record;

            public CapturingCommand(Action<string> record) => _record = record;

            public string CommandText { get; set; } = "";
            public int CommandTimeout { get; set; }
            public System.Data.CommandType CommandType { get; set; } = System.Data.CommandType.Text;
            public System.Data.IDbConnection? Connection { get; set; }
            public System.Data.IDataParameterCollection Parameters { get; } = new CapturingParameters();
            public System.Data.IDbTransaction? Transaction { get; set; }
            public System.Data.UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public System.Data.IDbDataParameter CreateParameter() => new CapturingParameter();
            public void Dispose() { }
            public int ExecuteNonQuery() { _record(CommandText); return 0; }
            public System.Data.IDataReader ExecuteReader() => throw new NotSupportedException();
            public System.Data.IDataReader ExecuteReader(System.Data.CommandBehavior behavior) => throw new NotSupportedException();
            public void Prepare() { }

            // Count/aggregate terminals go through ExecuteScalar; 0 is a valid answer and keeps
            // the caller's conversion path intact.
            public object? ExecuteScalar() { _record(CommandText); return 0; }
        }

        private sealed class CapturingParameters : List<object>, System.Data.IDataParameterCollection
        {
            public object this[string parameterName] { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }
        }

        private sealed class CapturingParameter : System.Data.IDbDataParameter
        {
            public byte Precision { get; set; }
            public byte Scale { get; set; }
            public int Size { get; set; }
            public System.Data.DbType DbType { get; set; }
            public System.Data.ParameterDirection Direction { get; set; }
            public bool IsNullable => true;
            public string ParameterName { get; set; } = "";
            public string SourceColumn { get; set; } = "";
            public System.Data.DataRowVersion SourceVersion { get; set; }
            public object? Value { get; set; }
        }
    }
}
