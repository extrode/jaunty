using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// Unit tests for <see cref="InterceptorPipeline"/>.
/// Tests interceptor ordering, short-circuit execution, and exception handling.
/// </summary>
public class InterceptorPipelineTests
{
    #region Helpers

    private static IDbConnection CreateMockConnection()
    {
        return new TestDbConnection();
    }

    private class TestInterceptor : ICommandInterceptor
    {
        public List<string> ExecCalls { get; } = new();
        public List<string> ExecutedCalls { get; } = new();
        public List<string> FailedCalls { get; } = new();
        public bool ShouldThrowOnExecuting { get; set; }
        public bool ShouldThrowOnExecuted { get; set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            ExecCalls.Add(context.CommandText);
            if (ShouldThrowOnExecuting)
                throw new InvalidOperationException("Test exception from OnCommandExecuting");
            return new ValueTask();
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            ExecutedCalls.Add($"{context.CommandText}:{context.Elapsed.TotalMilliseconds}ms");
            if (ShouldThrowOnExecuted)
                throw new InvalidOperationException("Test exception from OnCommandExecuted");
            return new ValueTask();
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            FailedCalls.Add($"{context.CommandText}:{exception.Message}");
            return new ValueTask();
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullInterceptors_CreatesEmptyPipeline()
    {
        var pipeline = new InterceptorPipeline(null!);
        Assert.False(pipeline.HasInterceptors);
    }

    [Fact]
    public void Constructor_WithEmptyEnumerable_CreatesEmptyPipeline()
    {
        var pipeline = new InterceptorPipeline(Enumerable.Empty<ICommandInterceptor>());
        Assert.False(pipeline.HasInterceptors);
    }

    [Fact]
    public void Constructor_WithInterceptors_HasInterceptors()
    {
        var interceptors = new[] { new TestInterceptor(), new TestInterceptor() };
        var pipeline = new InterceptorPipeline(interceptors);
        Assert.True(pipeline.HasInterceptors);
    }

    #endregion

    #region ExecuteWithInterceptionAsync Tests - No Interceptors

    [Fact]
    public async Task ExecuteWithInterceptionAsync_NoInterceptors_ExecutesFuncDirectly()
    {
        // Arrange
        var pipeline = new InterceptorPipeline(Enumerable.Empty<ICommandInterceptor>());
        bool executed = false;

        // Act
        await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            () =>
            {
                executed = true;
                return new ValueTask<int>(42);
            },
            CancellationToken.None);

        // Assert
        Assert.True(executed);
    }

    #endregion

    #region ExecuteWithInterceptionAsync Tests - With Interceptors

    [Fact]
    public async Task ExecuteWithInterceptionAsync_WithInterceptors_InvokesInOrder()
    {
        // Arrange
        var interceptor1 = new TestInterceptor();
        var interceptor2 = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor1, interceptor2 });

        // Act
        await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            () => new ValueTask<int>(42),
            CancellationToken.None);

        // Assert
        Assert.Equal("SELECT 1", interceptor1.ExecCalls[0]);
        Assert.Equal("SELECT 1", interceptor2.ExecCalls[0]);
        Assert.Contains("SELECT 1:", interceptor1.ExecutedCalls[0]);
        Assert.Contains("SELECT 1:", interceptor2.ExecutedCalls[0]);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_InvokesInterceptorsInRegistrationOrder()
    {
        // Arrange
        var executionOrder = new List<int>();
        var pipeline = new InterceptorPipeline(new[]
        {
            new SimpleInterceptor(() => executionOrder.Add(1)),
            new SimpleInterceptor(() => executionOrder.Add(2)),
            new SimpleInterceptor(() => executionOrder.Add(3))
        });

        // Act
        await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            () => new ValueTask<int>(42),
            CancellationToken.None);

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_PassesElapsedTimeToExecuted()
    {
        // Arrange
        var interceptor = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor });

        // Act
        await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            async () =>
            {
                await Task.Delay(50);
                return 42;
            },
            CancellationToken.None);

        // Assert
        Assert.Single(interceptor.ExecutedCalls);
        // Parse the elapsed time from the format "SELECT 1:XXXms" and verify it's at least 50ms
        var call = interceptor.ExecutedCalls[0];
        var timePart = call.Split(':')[1].Replace("ms", "");
        Assert.True(double.TryParse(timePart, out var elapsedMs) && elapsedMs >= 50,
            $"Expected elapsed time >= 50ms, but got {elapsedMs}ms");
    }

    #endregion

    #region Short-Circuit Tests

    [Fact]
    public async Task ExecuteWithInterceptionAsync_ExceptionInExecuting_ShortCircuitsExecution()
    {
        // Arrange
        var interceptor1 = new TestInterceptor { ShouldThrowOnExecuting = true };
        var interceptor2 = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor1, interceptor2 });
        bool executed = false;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteWithInterceptionAsync(
                "SELECT 1",
                null,
                CreateMockConnection(),
                CommandType.Text,
                () =>
                {
                    executed = true;
                    return new ValueTask<int>(42);
                },
                CancellationToken.None));

        // Assert - first interceptor's OnCommandExecuting was called, second was not
        Assert.Single(interceptor1.ExecCalls);
        Assert.Empty(interceptor2.ExecCalls);
        Assert.False(executed);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_ExceptionInExecuting_CallsFailedOnAllInterceptors()
    {
        // Arrange
        var interceptor1 = new TestInterceptor { ShouldThrowOnExecuting = true };
        var interceptor2 = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor1, interceptor2 });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteWithInterceptionAsync(
                "SELECT 1",
                null,
                CreateMockConnection(),
                CommandType.Text,
                () => new ValueTask<int>(42),
                CancellationToken.None));

        // Assert - both interceptors' OnCommandFailed should be called
        Assert.NotNull(exception);
        Assert.Equal("Test exception from OnCommandExecuting", exception.Message);
        Assert.NotEmpty(interceptor1.ExecCalls);
        Assert.NotEmpty(interceptor1.FailedCalls);
        Assert.NotEmpty(interceptor2.FailedCalls);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_InterceptorThrowsNonInvalidOperationException_PropagatesUnchanged()
    {
        // Arrange - InvokeExecutingAsync does not catch or wrap interceptor exceptions, so
        // whatever type the interceptor throws must propagate unchanged, not be coerced to
        // InvalidOperationException.
        var pipeline = new InterceptorPipeline(new ICommandInterceptor[] { new TypedThrowingInterceptor() });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await pipeline.ExecuteWithInterceptionAsync(
                "SELECT 1",
                null,
                CreateMockConnection(),
                CommandType.Text,
                () => new ValueTask<int>(42),
                CancellationToken.None));

        Assert.Equal("Non-InvalidOperationException from interceptor", exception.Message);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task ExecuteWithInterceptionAsync_ExceptionInExecuted_DoesNotMaskResult()
    {
        // Arrange
        var interceptor = new TestInterceptor { ShouldThrowOnExecuted = true };
        var pipeline = new InterceptorPipeline(new[] { interceptor });

        // Act
        var result = await pipeline.ExecuteWithInterceptionAsync(
            "SELECT 1",
            null,
            CreateMockConnection(),
            CommandType.Text,
            () => new ValueTask<int>(42),
            CancellationToken.None);

        // Assert - the exception from executed should not propagate
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_ExceptionInExecute_CallsFailed()
    {
        // Arrange
        var interceptor = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteWithInterceptionAsync(
                "SELECT 1",
                null,
                CreateMockConnection(),
                CommandType.Text,
                () => throw new InvalidOperationException("Execute failed"),
                CancellationToken.None));

        // Assert
        Assert.NotEmpty(interceptor.FailedCalls);
        Assert.Contains("Execute failed", interceptor.FailedCalls[0]);
    }

    [Fact]
    public async Task ExecuteWithInterceptionAsync_ExceptionInFailed_Swallowed()
    {
        // Arrange
        var failingInterceptor = new FailingInterceptor();
        var pipeline = new InterceptorPipeline(new[] { failingInterceptor });

        // Act - should not throw even though OnCommandFailed throws
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteWithInterceptionAsync(
                "SELECT 1",
                null,
                CreateMockConnection(),
                CommandType.Text,
                () => throw new InvalidOperationException("Original exception"),
                CancellationToken.None));

        // The original exception should propagate, not the one from OnCommandFailed (both are
        // InvalidOperationException, so the message must be checked to actually distinguish them).
        Assert.Equal("Original exception", ex.Message);
    }

    #endregion

    #region Non-Generic ExecuteWithInterceptionAsync Tests

    [Fact]
    public async Task ExecuteWithInterceptionAsync_NonGeneric_InvokesInterceptors()
    {
        // Arrange
        var interceptor = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor });
        bool executed = false;

        // Act
        await pipeline.ExecuteWithInterceptionAsync(
            "UPDATE Users SET Name = 'Test'",
            null,
            CreateMockConnection(),
            CommandType.Text,
            () =>
            {
                executed = true;
                return new ValueTask();
            },
            CancellationToken.None);

        // Assert
        Assert.True(executed);
        Assert.Single(interceptor.ExecCalls);
        Assert.Single(interceptor.ExecutedCalls);
    }

    #endregion

    #region GetInterceptors Tests

    [Fact]
    public void GetInterceptors_ReturnsRegisteredInterceptors()
    {
        // Arrange
        var interceptor1 = new TestInterceptor();
        var interceptor2 = new TestInterceptor();
        var pipeline = new InterceptorPipeline(new[] { interceptor1, interceptor2 });

        // Act
        var interceptors = pipeline.GetInterceptors().ToList();

        // Assert
        Assert.Equal(2, interceptors.Count);
        Assert.Contains(interceptor1, interceptors);
        Assert.Contains(interceptor2, interceptors);
    }

    #endregion

    #region Helper Classes

    private class SimpleInterceptor : ICommandInterceptor
    {
        private readonly Action _onExecuting;

        public SimpleInterceptor(Action onExecuting)
        {
            _onExecuting = onExecuting;
        }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            _onExecuting();
            return new ValueTask();
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
            => new ValueTask();

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
            => new ValueTask();
    }

    private class FailingInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
            => new ValueTask();

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
            => new ValueTask();

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Interceptor failed");
    }

    private class TypedThrowingInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
            => throw new ArgumentException("Non-InvalidOperationException from interceptor");

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
            => new ValueTask();

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
            => new ValueTask();
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
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
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
