using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Interceptors;
using Jaunty.StoredProcedure;

using Xunit;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// AUD-R25 (B2-2): the <c>SpParameters</c> overloads of the stored-procedure API built and ran
/// their commands by hand and invoked <b>neither</b> <c>JauntyConfig.Logger</c> nor the registered
/// <c>ICommandInterceptor</c> pipeline.
///
/// <para>
/// The identically-named <c>object? parameters</c> overloads delegate to <c>Query&lt;T&gt;</c> /
/// <c>QueryFirst</c> / <c>QueryFirstOrDefault</c> / <c>QueryScalar</c> / <c>ExecuteNonQueryCore</c>
/// and the async equivalents, all of which route through <c>ExecuteReader</c> / <c>QueryCore</c> /
/// <c>ExecuteNonQueryCore</c> and therefore do invoke both. So
/// <c>connection.ExecuteStoredProcedure&lt;T&gt;("sp_x", new { id = 1 })</c> was logged and
/// intercepted while <c>connection.ExecuteStoredProcedure&lt;T&gt;("sp_x", spParams)</c> was
/// invisible to both - same method name, same public surface, opposite observability, with nothing
/// in the XML docs saying so.
/// </para>
///
/// <para>
/// This is not the documented streaming exception: these are fully buffered, non-lazy calls that
/// can be wrapped exactly like the buffered <c>Query*</c> paths. And it silently excluded precisely
/// the stored-procedure calls that use output and return parameters - typically the ones an audit
/// trail most needs.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class SpParametersObservabilityTests : IDisposable
{
    private const string Procedure = "usp_UpdateInventory";

    private readonly Action<string, object?>? _originalLogger = JauntyConfig.Logger;
    private readonly List<(string Sql, object? Parameters)> _logged = [];
    private readonly RecordingInterceptor _interceptor = new();

    public SpParametersObservabilityTests()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = (sql, parameters) => _logged.Add((sql, parameters));
        JauntyConfig.AddInterceptor(_interceptor);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = _originalLogger;
    }

    // ---------------------------------------------------------------------------
    // Interception
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecuteStoredProcedure_WithSpParameters_IsIntercepted()
    {
        var connection = new StubDbConnection();

        connection.ExecuteStoredProcedure<Row>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_interceptor.Executing).CommandText);
        Assert.Single(_interceptor.Executed);
        Assert.Empty(_interceptor.Failed);
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_WithSpParameters_IsIntercepted()
    {
        var connection = new StubDbConnection { ScalarResult = 7 };

        connection.ExecuteStoredProcedureScalar<int>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_interceptor.Executing).CommandText);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public void ExecuteStoredProcedureNonQuery_WithSpParameters_IsIntercepted()
    {
        var connection = new StubDbConnection { RowsAffected = 3 };

        int rows = connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters());

        Assert.Equal(3, rows);
        Assert.Equal(Procedure, Assert.Single(_interceptor.Executing).CommandText);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task ExecuteStoredProcedureAsync_WithSpParameters_IsIntercepted()
    {
        var connection = new StubDbConnection();

        await connection.ExecuteStoredProcedureAsync<Row>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_interceptor.Executing).CommandText);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_WithSpParameters_IsIntercepted()
    {
        var connection = new StubDbConnection { ScalarResult = 7 };

        await connection.ExecuteStoredProcedureScalarAsync<int>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_interceptor.Executing).CommandText);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task ExecuteStoredProcedureNonQueryAsync_WithSpParameters_IsIntercepted()
    {
        var connection = new StubDbConnection { RowsAffected = 5 };

        int rows = await connection.ExecuteStoredProcedureNonQueryAsync(Procedure, NewParameters());

        Assert.Equal(5, rows);
        Assert.Equal(Procedure, Assert.Single(_interceptor.Executing).CommandText);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public void ExecuteStoredProcedureNonQuery_WhenTheCommandThrows_ReportsFailureToInterceptors()
    {
        // The failure half of the lifecycle - an audit trail that only records successes is not one.
        var connection = new StubDbConnection { ThrowOnExecute = new InvalidOperationException("boom") };

        Assert.Throws<InvalidOperationException>(() =>
            connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters()));

        Assert.Single(_interceptor.Executing);
        Assert.Empty(_interceptor.Executed);
        Assert.Single(_interceptor.Failed);
    }

    [Fact]
    public void Interception_PassesTheSpParametersAsTheCommandParameters()
    {
        var connection = new StubDbConnection { RowsAffected = 1 };
        SpParameters parameters = NewParameters();

        connection.ExecuteStoredProcedureNonQuery(Procedure, parameters);

        Assert.Same(parameters, Assert.Single(_interceptor.Executing).Parameters);
    }

    [Fact]
    public void Interception_ReportsStoredProcedureAsTheCommandType()
    {
        var connection = new StubDbConnection { RowsAffected = 1 };

        connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters());

        Assert.Equal(CommandType.StoredProcedure, Assert.Single(_interceptor.Executing).CommandType);
    }

    [Fact]
    public void NullSpParameters_AreStillIntercepted()
    {
        // A null literal binds to the SpParameters overload, and the wrapper substitutes an empty
        // SpParameters. That substitution must happen before interception, not after, so an
        // interceptor never sees a null it cannot distinguish from "no parameters object".
        var connection = new StubDbConnection { RowsAffected = 1 };

        connection.ExecuteStoredProcedureNonQuery(Procedure, null);

        CommandContext context = Assert.Single(_interceptor.Executing);
        Assert.NotNull(context.Parameters);
        Assert.IsType<SpParameters>(context.Parameters);
    }

    // ---------------------------------------------------------------------------
    // Logging
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecuteStoredProcedure_WithSpParameters_IsLogged()
    {
        var connection = new StubDbConnection();

        connection.ExecuteStoredProcedure<Row>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_WithSpParameters_IsLogged()
    {
        var connection = new StubDbConnection { ScalarResult = 1 };

        connection.ExecuteStoredProcedureScalar<int>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public void ExecuteStoredProcedureNonQuery_WithSpParameters_IsLogged()
    {
        var connection = new StubDbConnection { RowsAffected = 1 };

        connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public async Task ExecuteStoredProcedureAsync_WithSpParameters_IsLogged()
    {
        var connection = new StubDbConnection();

        await connection.ExecuteStoredProcedureAsync<Row>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_WithSpParameters_IsLogged()
    {
        var connection = new StubDbConnection { ScalarResult = 1 };

        await connection.ExecuteStoredProcedureScalarAsync<int>(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public async Task ExecuteStoredProcedureNonQueryAsync_WithSpParameters_IsLogged()
    {
        var connection = new StubDbConnection { RowsAffected = 1 };

        await connection.ExecuteStoredProcedureNonQueryAsync(Procedure, NewParameters());

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public void Logging_HappensBeforeExecution()
    {
        // Logging after execution means a procedure that throws is never logged - the whole point
        // of the callback for diagnosing a bad call.
        var connection = new StubDbConnection { ThrowOnExecute = new InvalidOperationException("boom") };

        Assert.Throws<InvalidOperationException>(() =>
            connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters()));

        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    [Fact]
    public void Logging_PassesTheSpParametersThrough()
    {
        var connection = new StubDbConnection { RowsAffected = 1 };
        SpParameters parameters = NewParameters();

        connection.ExecuteStoredProcedureNonQuery(Procedure, parameters);

        Assert.Same(parameters, Assert.Single(_logged).Parameters);
    }

    // ---------------------------------------------------------------------------
    // Behaviour that must survive the restructure
    // ---------------------------------------------------------------------------

    [Fact]
    public void OutputParameterValues_AreStillReadBack()
    {
        // The whole reason these overloads exist. Wrapping them in the pipeline must not skip the
        // ReadOutputParameters step that runs after execution.
        var connection = new StubDbConnection { RowsAffected = 1, OutputValue = 99 };
        SpParameters parameters = NewParameters();

        connection.ExecuteStoredProcedureNonQuery(Procedure, parameters);

        Assert.Equal(99, parameters.Get<int>("Total"));
    }

    [Fact]
    public async Task OutputParameterValues_AreStillReadBackAsync()
    {
        var connection = new StubDbConnection { RowsAffected = 1, OutputValue = 42 };
        SpParameters parameters = NewParameters();

        await connection.ExecuteStoredProcedureNonQueryAsync(Procedure, parameters);

        Assert.Equal(42, parameters.Get<int>("Total"));
    }

    [Fact]
    public void ClosedConnection_IsStillOpenedAndClosedAgain()
    {
        var connection = new StubDbConnection { RowsAffected = 1 };

        connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters());

        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void WithNoPipelineRegistered_TheCallStillWorksAndStillLogs()
    {
        // The wrapper's "pipeline is null" fast path.
        JauntyConfig.ClearInterceptors();
        var connection = new StubDbConnection { RowsAffected = 2 };

        int rows = connection.ExecuteStoredProcedureNonQuery(Procedure, NewParameters());

        Assert.Equal(2, rows);
        Assert.Equal(Procedure, Assert.Single(_logged).Sql);
    }

    // ---------------------------------------------------------------------------

    private static SpParameters NewParameters()
    {
        var parameters = new SpParameters();
        parameters.AddInput("ProductId", 1);
        parameters.AddOutput("Total", DbType.Int32);
        return parameters;
    }

    public class Row
    {
        public int Id { get; set; }
    }

    private sealed class RecordingInterceptor : ICommandInterceptor
    {
        public List<CommandContext> Executing { get; } = [];
        public List<CommandContext> Executed { get; } = [];
        public List<CommandContext> Failed { get; } = [];

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executing.Add(context);
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed.Add(context);
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            Failed.Add(context);
            return default;
        }
    }

    // ---------------------------------------------------------------------------
    // A DbConnection that records what it was asked to do and returns canned results, so these
    // tests exercise the observability wiring without needing a database.
    // ---------------------------------------------------------------------------

#pragma warning disable CS8765 // DbConnection.ConnectionString is [AllowNull]; the attribute is not public on net472.
    private sealed class StubDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public int OpenCount { get; private set; }
        public object? ScalarResult { get; set; }
        public int RowsAffected { get; set; }
        public object? OutputValue { get; set; }
        public Exception? ThrowOnExecute { get; set; }

        public override string ConnectionString { get => ""; set { } }
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open()
        {
            OpenCount++;
            _state = ConnectionState.Open;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new StubDbCommand(this);
    }

    private sealed class StubDbCommand(StubDbConnection owner) : DbCommand
    {
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = owner;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new StubParameterCollection();

        public override void Cancel() { }
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new StubParameter();

        public override int ExecuteNonQuery()
        {
            Fail();
            PublishOutputs();
            return owner.RowsAffected;
        }

        public override object? ExecuteScalar()
        {
            Fail();
            PublishOutputs();
            return owner.ScalarResult;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Fail();
            PublishOutputs();
            return new EmptyDbReader();
        }

        private void Fail()
        {
            if (owner.ThrowOnExecute is not null)
                throw owner.ThrowOnExecute;
        }

        // A real provider writes output-parameter values back onto the command's parameters when
        // the command completes; ReadOutputParameters then copies them into the SpParameters.
        private void PublishOutputs()
        {
            if (owner.OutputValue is null)
                return;

            foreach (DbParameter parameter in DbParameterCollection)
            {
                if (parameter.Direction is ParameterDirection.Output or ParameterDirection.InputOutput or ParameterDirection.ReturnValue)
                    parameter.Value = owner.OutputValue;
            }
        }
    }
#pragma warning restore CS8765

    private sealed class StubParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    private sealed class StubParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => _items;

        public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (object v in values) Add(v); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) =>
            _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }

    private sealed class EmptyDbReader : DbDataReader
    {
        public override int Depth => 0;
        public override int FieldCount => 1;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override bool Read() => false;
        public override bool NextResult() => false;

        public override object this[int ordinal] => 0;
        public override object this[string name] => 0;

        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => "int";
        public override DateTime GetDateTime(int ordinal) => default;
        public override decimal GetDecimal(int ordinal) => 0;
        public override double GetDouble(int ordinal) => 0;
        public override Type GetFieldType(int ordinal) => typeof(int);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => default;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override string GetName(int ordinal) => "Id";
        public override int GetOrdinal(string name) => 0;
        public override string GetString(int ordinal) => "";
        public override object GetValue(int ordinal) => 0;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => false;
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }
}
