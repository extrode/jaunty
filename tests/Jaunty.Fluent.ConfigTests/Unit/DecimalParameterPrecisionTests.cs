using System.Collections;
using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26 (batch 5, medium/bug). <c>JoinedQueryBuilder</c> and <c>GroupedQueryBuilder</c> each
/// carried a local <c>NormalizeForBinding</c> that coerced <b>every</b> <see cref="decimal"/>
/// parameter to <see cref="double"/> before binding - <c>value is decimal d ? (double)d : value</c>
/// - unconditionally and on every dialect, on the strength of one provider's behaviour. The
/// conversion now goes through <c>DecimalParameterBinding</c>, which asks the dialect.
///
/// <para>
/// The finding proposed removing it outright, on the grounds that the provider bug it cites could
/// not be reproduced. Removing it turns four <c>GroupBy</c>/<c>Having</c> integration tests red, so
/// the bug is real; what the re-measurement missed is <em>when</em> it fires. Both SQLite providers
/// bind a <see cref="decimal"/> as TEXT. Against a column with <em>numeric affinity</em>, SQLite
/// applies that affinity to the TEXT operand and converts it, so <c>WHERE price = @p</c> matches -
/// that is the case the audit probed, and it is why the coercion looked unnecessary. Against a
/// <em>bare expression</em> there is no affinity to apply, and TEXT sorts above every number, so
/// <c>HAVING SUM(price) &gt; @p</c> matches no group and <c>&lt; @p</c> matches every group,
/// whatever the values are. Not only aggregates - <c>WHERE price * 1 &gt; @p</c> fails the same way,
/// though the fluent translator emits no arithmetic, so HAVING is the only route to it from here.
/// </para>
///
/// <para>
/// So the finding was half right, and it is the half these tests pin. The conversion belongs on
/// SQLite and nowhere else: <see cref="double"/> carries 15-17 significant digits against
/// <see cref="decimal"/>'s 28-29, and a SQL Server <c>DECIMAL(19,4)</c> and a PostgreSQL
/// <c>NUMERIC</c> comparison were being downgraded to binary floating point to work around
/// something attributed to SQLite. <c>DecimalBindingDialectTests</c> pins which dialects ask for it;
/// these pin that the two builders honour the answer.
/// </para>
///
/// <para>
/// The two builders' <c>BindParameters</c> do not cover every terminal on those builders, which is
/// what let the defect survive unexamined: a joined <c>Select()</c> hands its parameters to the core
/// <c>QueryPartial</c> and was never coerced, while <c>SelectBoth()</c> and the projection forms
/// bind through <c>BindParameters</c> and were. Each test below is pinned to a terminal that reaches
/// the changed code - verified by making <c>BindParameters</c> throw and confirming the test saw it.
/// </para>
///
/// <para>
/// These tests assert on the value the builder binds rather than on rows returned. Row-counting
/// through SQLite runs into two provider behaviours with nothing to do with this defect that would
/// make such a test lie about what it proves: <c>SQLiteDataReader.GetValue</c> on an INTEGER column
/// declared <c>DECIMAL</c> returns 9007199254740990 for a stored 9007199254740993 - where
/// <c>GetDecimal</c> on the same column returns it exactly - and the TEXT-binding behaviour above.
/// Both are recorded separately. The bound value is what this fix changes, so it is what these
/// measure.
/// </para>
/// </summary>
public class DecimalParameterPrecisionTests : IDisposable
{
    /// <summary>
    /// 28 significant digits - inside <see cref="decimal"/>'s range and far past
    /// <see cref="double"/>'s 15-17, so a coerced copy is detectably not the original.
    /// </summary>
    private const decimal HighPrecision = 1234567890123456789012345.678m;

    public DecimalParameterPrecisionTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Table("precise_invoice")]
    public class PreciseInvoice
    {
        [Key]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
    }

    [Table("precise_customer")]
    public class PreciseCustomer
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // ------------------------------------------------------------------
    // The dialects that bind a decimal exactly keep it
    // ------------------------------------------------------------------

    /// <summary>
    /// The joined path on SQL Server - a dialect the cited workaround never claimed to need it, and
    /// one with a native <c>DECIMAL</c> type it was silently discarding.
    ///
    /// <para>
    /// <c>SelectBoth</c> rather than <c>Select</c>: the plain <c>Select()</c> delegates to core
    /// <c>QueryPartial</c>, which never coerced, so it would have passed with the defect still in
    /// place and proved nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void TheJoinedPathBindsADecimalAsADecimalOnSqlServer()
    {
        var connection = new SqlConnection();

        connection.From<PreciseInvoice>()
            .InnerJoin<PreciseCustomer>()
            .On((i, c) => i.CustomerId == c.Id)
            .Where((i, c) => i.Amount == HighPrecision)
            .SelectBoth();

        object? bound = Assert.Single(connection.BoundValues, v => v is decimal or double);
        Assert.IsType<decimal>(bound);
        Assert.Equal(HighPrecision, (decimal)bound!);
    }

    /// <summary>
    /// The grouped path binds through its own copy of the same helper, so it gets its own test.
    /// </summary>
    [Fact]
    public void TheGroupedPathBindsADecimalAsADecimalOnSqlServer()
    {
        var connection = new SqlConnection();

        connection.From<PreciseInvoice>()
            .GroupBy(i => i.CustomerId)
            .Having(g => g.Sum(i => i.Amount) > HighPrecision)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(i => i.Amount) });

        object? bound = Assert.Single(connection.BoundValues, v => v is decimal or double);
        Assert.IsType<decimal>(bound);
        Assert.Equal(HighPrecision, (decimal)bound!);
    }

    /// <summary>
    /// The asymmetry the finding names, in its sharpest form: the <em>same</em> joined builder with
    /// the same WHERE bound two different values depending on which terminal was called.
    /// <c>Select()</c> binds through <c>ParameterCollection</c> and never coerced; <c>SelectBoth()</c>
    /// binds through <c>JoinedQueryBuilder.BindParameters</c> and did. Changing how you consumed the
    /// results changed which rows you got. On a dialect that needs no conversion the two must agree.
    /// </summary>
    [Fact]
    public void TheTwoJoinedTerminalsBindTheSameValueOnSqlServer()
    {
        var plainConnection = new SqlConnection();
        plainConnection.From<PreciseInvoice>()
            .InnerJoin<PreciseCustomer>()
            .On((i, c) => i.CustomerId == c.Id)
            .Where((i, c) => i.Amount == HighPrecision)
            .Select();

        var joinedConnection = new SqlConnection();
        joinedConnection.From<PreciseInvoice>()
            .InnerJoin<PreciseCustomer>()
            .On((i, c) => i.CustomerId == c.Id)
            .Where((i, c) => i.Amount == HighPrecision)
            .SelectBoth();

        object? plain = Assert.Single(plainConnection.BoundValues, v => v is decimal or double);
        object? joined = Assert.Single(joinedConnection.BoundValues, v => v is decimal or double);

        Assert.Equal(plain, joined);
    }

    // ------------------------------------------------------------------
    // SQLite still gets the conversion
    // ------------------------------------------------------------------

    /// <summary>
    /// The half of the old behaviour that has to survive. Without this the four
    /// <c>FluentGroupByTests</c>/<c>FluentGroupByJoinTests</c> HAVING cases go red, and they go red
    /// by returning wrong rows rather than by throwing - the failure this whole finding is about.
    /// Pinned at unit level too, because those four run against a real database and would not say
    /// why.
    /// </summary>
    [Fact]
    public void TheGroupedPathStillConvertsADecimalOnSqlite()
    {
        var connection = new SqliteConnection();

        connection.From<PreciseInvoice>()
            .GroupBy(i => i.CustomerId)
            .Having(g => g.Sum(i => i.Amount) > 150m)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(i => i.Amount) });

        object? bound = Assert.Single(connection.BoundValues, v => v is decimal or double);
        Assert.IsType<double>(bound);
        Assert.Equal(150.0d, (double)bound!);
    }

    /// <summary>
    /// And the joined path, whose parameters are compared against columns rather than aggregates.
    /// It is converted too: <c>BindParameters</c> binds a flat name/value list and cannot tell which
    /// side of a comparison a value will land on, and the same builder feeds
    /// <c>GroupedJoinedQueryBuilder</c>'s HAVING clauses. Converting uniformly per dialect is the
    /// decision being recorded here, not an oversight - on SQLite it costs exactness past 2^53 on a
    /// column comparison, which is the narrower of the two failures.
    ///
    /// <para>
    /// That cost is real and observable, so it is stated rather than implied: against SQLite, a
    /// column holding 9007199254740993 is matched by <c>Select()</c> and missed by
    /// <c>SelectBoth()</c> for the same <c>Where</c>. It is not new - the coercion this replaced was
    /// unconditional and did the same - but this path is the inexact one of the two, and neither
    /// scoping the conversion to HAVING parameters nor casting in the generated SQL was in scope
    /// here. Both are recorded under AUD-R26-050 for round 27.
    /// </para>
    /// </summary>
    [Fact]
    public void TheJoinedPathStillConvertsADecimalOnSqlite()
    {
        var connection = new SqliteConnection();

        connection.From<PreciseInvoice>()
            .InnerJoin<PreciseCustomer>()
            .On((i, c) => i.CustomerId == c.Id)
            .Where((i, c) => i.Amount == 150m)
            .SelectBoth();

        object? bound = Assert.Single(connection.BoundValues, v => v is decimal or double);
        Assert.IsType<double>(bound);
    }

    // ------------------------------------------------------------------
    // Capturing test doubles. Each has to be named exactly as the provider's own connection type:
    // dialect resolution keys on Type.Name.
    // ------------------------------------------------------------------

    private sealed class SqlConnection : CapturingConnection { }

    private sealed class SqliteConnection : CapturingConnection { }

    private abstract class CapturingConnection : IDbConnection
    {
        public List<object?> BoundValues { get; } = new();

        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Dispose() { }
        public void Open() { }

        public IDbCommand CreateCommand() => new CapturingCommand(this, BoundValues);

        private sealed class CapturingCommand : IDbCommand
        {
            public CapturingCommand(IDbConnection connection, List<object?> bound)
            {
                Connection = connection;
                Parameters = new CapturingParameters(bound);
            }

            public string CommandText { get; set; } = "";
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; } = CommandType.Text;
            public IDbConnection? Connection { get; set; }
            public IDataParameterCollection Parameters { get; }
            public IDbTransaction? Transaction { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public IDbDataParameter CreateParameter() => new CapturingParameter();
            public void Dispose() { }
            public int ExecuteNonQuery() => 0;
            public object? ExecuteScalar() => null;
            public void Prepare() { }

            // An empty result set: these tests are about what goes in, not what comes back.
            public IDataReader ExecuteReader() => new EmptyReader();
            public IDataReader ExecuteReader(CommandBehavior behavior) => new EmptyReader();
        }

        /// <summary>Records each parameter's value as it is added, in order.</summary>
        private sealed class CapturingParameters : ArrayList, IDataParameterCollection
        {
            private readonly List<object?> _bound;

            public CapturingParameters(List<object?> bound) => _bound = bound;

            public override int Add(object? value)
            {
                if (value is IDataParameter parameter)
                    _bound.Add(parameter.Value);

                return base.Add(value);
            }

            public object this[string parameterName] { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }
        }

        private sealed class CapturingParameter : IDbDataParameter
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

        private sealed class EmptyReader : IDataReader
        {
            public int Depth => 0;
            public bool IsClosed => false;
            public int RecordsAffected => 0;
            public int FieldCount => 0;

            public void Close() { }
            public void Dispose() { }
            public DataTable? GetSchemaTable() => null;
            public bool NextResult() => false;
            public bool Read() => false;

            public object this[int i] => throw new NotSupportedException();
            public object this[string name] => throw new NotSupportedException();
            public bool GetBoolean(int i) => throw new NotSupportedException();
            public byte GetByte(int i) => throw new NotSupportedException();
            public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public char GetChar(int i) => throw new NotSupportedException();
            public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public IDataReader GetData(int i) => throw new NotSupportedException();
            public string GetDataTypeName(int i) => throw new NotSupportedException();
            public DateTime GetDateTime(int i) => throw new NotSupportedException();
            public decimal GetDecimal(int i) => throw new NotSupportedException();
            public double GetDouble(int i) => throw new NotSupportedException();
            public Type GetFieldType(int i) => throw new NotSupportedException();
            public float GetFloat(int i) => throw new NotSupportedException();
            public Guid GetGuid(int i) => throw new NotSupportedException();
            public short GetInt16(int i) => throw new NotSupportedException();
            public int GetInt32(int i) => throw new NotSupportedException();
            public long GetInt64(int i) => throw new NotSupportedException();
            public string GetName(int i) => throw new NotSupportedException();
            public int GetOrdinal(string name) => throw new NotSupportedException();
            public string GetString(int i) => throw new NotSupportedException();
            public object GetValue(int i) => throw new NotSupportedException();
            public int GetValues(object[] values) => throw new NotSupportedException();
            public bool IsDBNull(int i) => true;
        }
    }
}
