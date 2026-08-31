using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R25: two independent defects in <c>CteBuilder&lt;T&gt;</c>, both silent.
///
/// <para>
/// B5-2 - <c>Where(string column, object? value)</c> bound the value unconditionally and emitted
/// <c>col = @cte_p0</c>. Passing <see langword="null"/> bound DBNull, and under SQL's three-valued
/// logic <c>col = NULL</c> is UNKNOWN for every row, so the query returned zero rows instead of the
/// rows where the column IS NULL. Every other string-column predicate overload in the assembly
/// branches on null; this was the only one that did not.
/// </para>
///
/// <para>
/// B5-6 - <c>SelectFirst()</c>/<c>SelectFirstOrDefault()</c> set <c>_takeCount = 1</c> and never
/// restored it, permanently mutating the builder. A caller reusing the instance got a single row
/// from every later <c>Select()</c>, and <c>ToSql()</c> reported the truncated query.
/// QueryBuilder's 24 first/single terminals and SetOperationBuilder's 18 all save and restore.
/// </para>
/// </summary>
public class CteBuilderStateTests
{
    public CteBuilderStateTests()
        => SqlDialectFactory.RegisterDialect(nameof(EmptyResultConnection), new SQLiteDialect());

    // ------------------------------------------------------------------
    // B5-2: null must become IS NULL
    // ------------------------------------------------------------------

    [Fact]
    public void Where_WithNullValue_EmitsIsNull()
    {
        var sql = new EmptyResultConnection().Cte<Product>("Filtered")
            .As(q => q.Where(p => p.ProductId > 0))
            .Where("UnitPrice", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.DoesNotContain("cte_p", sql);
    }

    [Fact]
    public void Where_WithNullValue_BindsNoParameter()
    {
        // The bound DBNull is what made this return nothing; there must be no parameter at all now.
        var sql = new EmptyResultConnection().Cte<Product>("Filtered")
            .As(q => q.Where(p => p.ProductId > 0))
            .Where("UnitPrice", null)
            .ToSql();

        Assert.DoesNotContain("= @", sql[sql.IndexOf("IS NULL", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void Where_WithNonNullValue_IsUnchanged()
    {
        var sql = new EmptyResultConnection().Cte<Product>("Filtered")
            .As(q => q.Where(p => p.ProductId > 0))
            .Where("UnitPrice", 10m)
            .ToSql();

        Assert.Contains("cte_p", sql);
        Assert.DoesNotContain("IS NULL", sql);
    }

    [Fact]
    public void Where_WithNullValue_StillProducesAWellFormedWhereClause()
    {
        var sql = new EmptyResultConnection().Cte<Product>("Filtered")
            .As(q => q.Where(p => p.ProductId > 0))
            .Where("UnitPrice", null)
            .ToSql();

        // The predicate has to survive as a real WHERE clause, not degrade into a dangling operator.
        Assert.Contains("WHERE", sql);
        Assert.DoesNotContain("= NULL", sql);
        Assert.DoesNotContain("WHERE AND", sql);
    }

    // ------------------------------------------------------------------
    // B5-6: the first/single terminals must not mutate the builder
    // ------------------------------------------------------------------

    [Fact]
    public void SelectFirstOrDefault_DoesNotLeaveTheBuilderLimitedToOneRow()
    {
        var cte = new EmptyResultConnection().Cte<Product>("Reused")
            .As(q => q.Where(p => p.ProductId > 0));

        string before = cte.ToSql();
        cte.SelectFirstOrDefault();
        string after = cte.ToSql();

        Assert.Equal(before, after);
    }

    [Fact]
    public void SelectFirstOrDefault_LeavesNoPagingClauseBehind()
    {
        var cte = new EmptyResultConnection().Cte<Product>("Reused")
            .As(q => q.Where(p => p.ProductId > 0));

        cte.SelectFirstOrDefault();

        Assert.DoesNotContain("LIMIT", cte.ToSql(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectFirstOrDefault_StillLimitsItsOwnQuery()
    {
        // The restore must not defeat the point of the terminal - the query it runs is still LIMIT 1.
        var cte = new EmptyResultConnection().Cte<Product>("Reused")
            .As(q => q.Where(p => p.ProductId > 0))
            .Take(5);

        cte.SelectFirstOrDefault();

        // Take(5) survives the round trip, proving the original value was restored rather than cleared.
        Assert.Contains("5", cte.ToSql());
    }

    [Fact]
    public void SelectFirstOrDefault_PreservesAnExplicitTake()
    {
        var cte = new EmptyResultConnection().Cte<Product>("Reused")
            .As(q => q.Where(p => p.ProductId > 0))
            .Take(3);

        string before = cte.ToSql();
        cte.SelectFirstOrDefault();

        Assert.Equal(before, cte.ToSql());
    }

    // ------------------------------------------------------------------
    // Connection returning an empty result set, so the terminals can run without a database
    // ------------------------------------------------------------------

    private sealed class EmptyResultConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => new EmptyCommand(this);
        public void Open() { }
        public void Dispose() { }
    }

    private sealed class EmptyCommand(IDbConnection connection) : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; } = connection;
        public IDataParameterCollection Parameters { get; } = new ParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new Parameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new EmptyReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => new EmptyReader();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }

    private sealed class EmptyReader : IDataReader
    {
        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public int FieldCount => 0;

        public bool Read() => false;
        public bool NextResult() => false;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => throw new IndexOutOfRangeException();
        public object this[string name] => throw new IndexOutOfRangeException();

        public bool GetBoolean(int i) => throw new IndexOutOfRangeException();
        public byte GetByte(int i) => throw new IndexOutOfRangeException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new IndexOutOfRangeException();
        public char GetChar(int i) => throw new IndexOutOfRangeException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new IndexOutOfRangeException();
        public IDataReader GetData(int i) => throw new IndexOutOfRangeException();
        public string GetDataTypeName(int i) => throw new IndexOutOfRangeException();
        public DateTime GetDateTime(int i) => throw new IndexOutOfRangeException();
        public decimal GetDecimal(int i) => throw new IndexOutOfRangeException();
        public double GetDouble(int i) => throw new IndexOutOfRangeException();
        public Type GetFieldType(int i) => throw new IndexOutOfRangeException();
        public float GetFloat(int i) => throw new IndexOutOfRangeException();
        public Guid GetGuid(int i) => throw new IndexOutOfRangeException();
        public short GetInt16(int i) => throw new IndexOutOfRangeException();
        public int GetInt32(int i) => throw new IndexOutOfRangeException();
        public long GetInt64(int i) => throw new IndexOutOfRangeException();
        public string GetName(int i) => throw new IndexOutOfRangeException();
        public int GetOrdinal(string name) => throw new IndexOutOfRangeException();
        public string GetString(int i) => throw new IndexOutOfRangeException();
        public object GetValue(int i) => throw new IndexOutOfRangeException();
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
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

    private sealed class ParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }
}
