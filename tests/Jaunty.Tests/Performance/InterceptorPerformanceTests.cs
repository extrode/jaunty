using Jaunty.Diagnostics;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Performance;

/// <summary>
/// Performance tests for interceptor overhead.
/// </summary>
/// <remarks>
/// These tests verify that the interceptor pipeline adds minimal overhead
/// to command execution. Target: less than 5 microseconds per no-op interceptor.
///
/// Note: ValueTask completes synchronously for no-op interceptors, so we can
/// measure overhead without async/await contamination.
/// </remarks>
public class InterceptorPerformanceTests
{
    private const int WarmupIterations = 100;
    private const int TestIterations = 1000;
    private const double MaxOverheadPerInterceptorMicroseconds = 5.0;

    #region No-Op Interceptor Tests

    [Fact]
    public void InterceptorPipeline_WithNoOpInterceptor_HasMinimalOverhead()
    {
        // Wall-clock threshold: a useful canary on a developer machine,
        // pure noise on shared CI runners.
        if (Environment.GetEnvironmentVariable("CI") == "true")
            Assert.Skip("Wall-clock performance thresholds are unreliable on shared CI runners.");

        // Arrange
        var noOpInterceptor = new NoOpInterceptor();
        var pipeline = new InterceptorPipeline([noOpInterceptor]);
        var connection = new TestDbConnection();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            _ = pipeline.InvokeExecutingAsync("SELECT 1", null, connection, CommandType.Text, CancellationToken.None);
        }

        // Act - measure overhead
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < TestIterations; i++)
        {
            _ = pipeline.InvokeExecutingAsync("SELECT 1", null, connection, CommandType.Text, CancellationToken.None);
        }
        stopwatch.Stop();

        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        var overheadPerCall = (totalMs * 1000) / TestIterations; // Convert to microseconds

        // Assert - overhead should be under threshold
        Assert.True(
            overheadPerCall < MaxOverheadPerInterceptorMicroseconds,
            $"Interceptor overhead too high: {overheadPerCall:F2}μs (max: {MaxOverheadPerInterceptorMicroseconds}μs)");
    }

    [Fact]
    public async Task InterceptorPipeline_WithMultipleNoOpInterceptors_HasAcceptableOverhead()
    {
        // Wall-clock threshold: a useful canary on a developer machine,
        // pure noise on shared CI runners.
        if (Environment.GetEnvironmentVariable("CI") == "true")
            Assert.Skip("Wall-clock performance thresholds are unreliable on shared CI runners.");

        // Arrange - 5 no-op interceptors
        var interceptors = new List<ICommandInterceptor>
        {
            new NoOpInterceptor(),
            new NoOpInterceptor(),
            new NoOpInterceptor(),
            new NoOpInterceptor(),
            new NoOpInterceptor()
        };
        var pipeline = new InterceptorPipeline(interceptors);
        var connection = new TestDbConnection();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await pipeline.InvokeExecutingAsync("SELECT 1", null, connection, CommandType.Text, CancellationToken.None);
        }

        // Act - measure overhead
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < TestIterations; i++)
        {
            await pipeline.InvokeExecutingAsync("SELECT 1", null, connection, CommandType.Text, CancellationToken.None);
        }
        stopwatch.Stop();

        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        var overheadPerCall = (totalMs * 1000) / TestIterations; // Convert to microseconds
        var overheadPerInterceptor = overheadPerCall / 5;

        // Assert - overhead per interceptor should be under threshold
        Assert.True(
            overheadPerInterceptor < MaxOverheadPerInterceptorMicroseconds,
            $"Per-interceptor overhead too high: {overheadPerInterceptor:F2}μs (max: {MaxOverheadPerInterceptorMicroseconds}μs)");
    }

    #endregion

    #region AuditInterceptor Tests

    [Fact]
    public async Task AuditInterceptor_Overhead_IsAcceptable()
    {
        // Wall-clock threshold: a useful canary on a developer machine,
        // pure noise on shared CI runners.
        if (Environment.GetEnvironmentVariable("CI") == "true")
            Assert.Skip("Wall-clock performance thresholds are unreliable on shared CI runners.");

        // Arrange
        var auditInterceptor = new AuditInterceptor();
        var pipeline = new InterceptorPipeline(new[] { auditInterceptor });
        var connection = new TestDbConnection();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await pipeline.InvokeExecutingAsync("SELECT 1", null, connection, CommandType.Text, CancellationToken.None);
        }

        // Act - measure overhead
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < TestIterations; i++)
        {
            await pipeline.InvokeExecutingAsync("SELECT 1", null, connection, CommandType.Text, CancellationToken.None);
        }
        stopwatch.Stop();

        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        var overheadPerCall = (totalMs * 1000) / TestIterations; // Convert to microseconds

        // Assert - slightly higher threshold for AuditInterceptor due to record creation
        Assert.True(
            overheadPerCall < (MaxOverheadPerInterceptorMicroseconds * 2),
            $"AuditInterceptor overhead too high: {overheadPerCall:F2}μs (max: {MaxOverheadPerInterceptorMicroseconds * 2}μs)");
    }

    #endregion

    #region DiagnosticListener Tests

    [Fact]
    public void DiagnosticListener_Overhead_IsAcceptable()
    {
        // Wall-clock threshold: a useful canary on a developer machine,
        // pure noise on shared CI runners.
        if (Environment.GetEnvironmentVariable("CI") == "true")
            Assert.Skip("Wall-clock performance thresholds are unreliable on shared CI runners.");

        // Arrange - use singleton which is always enabled
        var connection = new TestDbConnection();
        var context = new CommandContext("SELECT 1", null, connection, CommandType.Text);

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            JauntyDiagnosticListener.Instance.WriteCommandExecuting(context);
        }

        // Act - measure overhead
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < TestIterations; i++)
        {
            JauntyDiagnosticListener.Instance.WriteCommandExecuting(context);
        }
        stopwatch.Stop();

        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        var overheadPerCall = (totalMs * 1000) / TestIterations; // Convert to microseconds

        // Assert - DiagnosticSource has some overhead but should still be fast
        Assert.True(
            overheadPerCall < (MaxOverheadPerInterceptorMicroseconds * 3),
            $"DiagnosticListener overhead too high: {overheadPerCall:F2}μs (max: {MaxOverheadPerInterceptorMicroseconds * 3}μs)");
    }

    #endregion

    #region Helper Classes

    private class NoOpInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            return new ValueTask();
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            return new ValueTask();
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            return new ValueTask();
        }
    }

    private class TestDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "Data Source=:memory:";
        public int ConnectionTimeout => 15;
        public string Database => "TestDb";
        public string DataSource => "InMemory";
        public IDbTransaction? Transaction { get; set; }
        public ConnectionState State => ConnectionState.Open;

        public IDbCommand CreateCommand() => new TestDbCommand(this);
        public IDbTransaction BeginTransaction() => new TestDbTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => new TestDbTransaction();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Open() { }
        public void Dispose() { }
    }

    private class TestDbCommand : IDbCommand
    {
        private readonly TestDbConnection _connection;

        public TestDbCommand(TestDbConnection connection)
        {
            _connection = connection;
        }

        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new TestParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.Both;

        public void Cancel() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new TestDbDataReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => new TestDbDataReader();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
        public void Dispose() { }

        public IDbDataParameter CreateParameter() => new TestDbParameter();
    }

    private class TestDbParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public bool IsNullable => false;
        public string ParameterName { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    private class TestParameterCollection : IDataParameterCollection
    {
        private readonly List<object> _parameters = new();

        public int Count => _parameters.Count;
        public object SyncRoot => this;
        public bool IsSynchronized => false;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;

        public int Add(object value) { _parameters.Add(value); return _parameters.Count - 1; }
        public bool Contains(object value) => _parameters.Contains(value);
        public bool Contains(string parameterName) => false;
        public void Clear() => _parameters.Clear();
        public int IndexOf(object value) => _parameters.IndexOf(value);
        public int IndexOf(string parameterName) => -1;
        public void Insert(int index, object value) => _parameters.Insert(index, value);
        public void Remove(object value) => _parameters.Remove(value);
        public void RemoveAt(int index) => _parameters.RemoveAt(index);
        public void RemoveAt(string parameterName) { }
        public object this[int index] { get => _parameters[index]; set => _parameters[index] = value; }
        public object this[string parameterName] { get => _parameters[0]; set => _parameters[0] = value; }
        public void CopyTo(Array array, int index) => ((System.Collections.IList)_parameters).CopyTo(array, index);
        public System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();
    }

    private class TestDbTransaction : IDbTransaction
    {
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public IDbConnection? Connection { get; set; }
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }

    private class TestDbDataReader : IDataReader
    {
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;

        public void Close() { }
        public void Dispose() { }
        public DataTable GetSchemaTable() => new DataTable();
        public bool NextResult() => false;
        public bool Read() => false;
        public int GetOrdinal(string name) => 0;
        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldoffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => this;
        public string GetDataTypeName(int i) => string.Empty;
        public DateTime GetDateTime(int i) => DateTime.MinValue;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public Type GetFieldType(int i) => typeof(string);
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => 0;
        public string GetName(int i) => string.Empty;
        public int FieldCount => 0;
        public string GetString(int i) => string.Empty;
        public object GetValue(int i) => string.Empty;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
        public object this[int i] => string.Empty;
        public object this[string name] => string.Empty;
    }

    #endregion
}
