using System.Data;
using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection.BulkCopy;

using MySql.Data.MySqlClient;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// Integration tests for the rewritten MySqlBulkCopyProvider (PROD-120):
/// chunked multi-row INSERT, no LOAD DATA LOCAL INFILE / local_infile dependency.
/// Skipped dynamically when no local MariaDB/MySQL server is reachable.
/// </summary>
public class MySqlBulkCopyProviderTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_MARIADB")
        ?? "Server=localhost;Database=jauntybench;User=root;";

    private static MySqlConnection OpenOrSkip()
    {
        var conn = new MySqlConnection(ConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"MariaDB/MySQL not reachable: {ex.Message}");
        }
        return conn;
    }

    private static DataTable MakeTable(int rows)
    {
        var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("price", typeof(decimal));
        table.Columns.Add("stock", typeof(int));
        table.Columns.Add("discontinued", typeof(bool));
        for (int i = 0; i < rows; i++)
        {
            var row = table.NewRow();
            row["name"] = i % 97 == 0 ? (object)DBNull.Value : $"prod-{i}";
            row["price"] = 0.5m + i;
            row["stock"] = i % 250;
            row["discontinued"] = i % 3 == 0;
            table.Rows.Add(row);
        }
        return table;
    }

    private static void CreateTable(MySqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS `{name}`;
            CREATE TABLE `{name}` (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(64) NULL,
                price DECIMAL(18,2) NOT NULL,
                stock INT NOT NULL,
                discontinued TINYINT(1) NOT NULL)
            """;
        cmd.ExecuteNonQuery();
    }

    private static long Count(MySqlConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM `{table}`";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void CopyToServer_MultipleChunks_InsertsAllRowsAndValues()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_sync");

        // 4 columns -> 500 rows per chunk; 1200 rows = 2 full chunks + partial tail
        using var reader = MakeTable(1200).CreateDataReader();
        // project away nothing: DataTableReader exposes exactly the 4 data columns
        int inserted = new MySqlBulkCopyProvider().CopyToServer(
            conn, null, "bulk_mysql_sync", reader, new BulkCopyOptions());

        Assert.Equal(1200, inserted);
        Assert.Equal(1200, Count(conn, "bulk_mysql_sync"));

        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT name, price, stock, discontinued FROM `bulk_mysql_sync` ORDER BY id LIMIT 1 OFFSET 1";
            using var r = check.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal("prod-1", r.GetString(0));
            Assert.Equal(1.5m, r.GetDecimal(1));
            Assert.Equal(1, r.GetInt32(2));
            Assert.False(r.GetBoolean(3));
        }

        // NULL round-trip (every 97th row has a DBNull name)
        using var nullCheck = conn.CreateCommand();
        nullCheck.CommandText = "SELECT COUNT(*) FROM `bulk_mysql_sync` WHERE name IS NULL";
        Assert.Equal(1200 / 97 + 1, Convert.ToInt32(nullCheck.ExecuteScalar()));
    }

    [Fact]
    public async Task CopyToServerAsync_InsertsAllRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_async");

        using var reader = MakeTable(750).CreateDataReader();
        int inserted = await new MySqlBulkCopyProvider().CopyToServerAsync(
            conn, null, "bulk_mysql_async", reader, new BulkCopyOptions(), CancellationToken.None);

        Assert.Equal(750, inserted);
        Assert.Equal(750, Count(conn, "bulk_mysql_async"));
    }

    [Fact]
    public void CopyToServer_RespectsExternalTransaction_RollbackDiscardsRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_txn");

        using (var txn = conn.BeginTransaction())
        {
            using var reader = MakeTable(50).CreateDataReader();
            int inserted = new MySqlBulkCopyProvider().CopyToServer(
                conn, null, "bulk_mysql_txn", reader, new BulkCopyOptions { Transaction = txn });
            Assert.Equal(50, inserted);
            txn.Rollback();
        }

        Assert.Equal(0, Count(conn, "bulk_mysql_txn"));
    }

    [Fact]
    public async Task CopyToServerAsync_RespectsExternalTransaction_RollbackDiscardsRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_async_txn");

        using (var txn = conn.BeginTransaction())
        {
            using var reader = MakeTable(50).CreateDataReader();
            int inserted = await new MySqlBulkCopyProvider().CopyToServerAsync(
                conn, null, "bulk_mysql_async_txn", reader, new BulkCopyOptions { Transaction = txn }, CancellationToken.None);
            Assert.Equal(50, inserted);
            txn.Rollback();
        }

        Assert.Equal(0, Count(conn, "bulk_mysql_async_txn"));
    }

    [Fact]
    public void CopyToServer_InvalidTableName_ThrowsArgumentException()
    {
        // Regression test (AUD-R11): BuildChunkCommand used to interpolate tableName/columnName
        // via EscapeIdentifier (backtick-doubling) without validating them first, unlike
        // PostgreSqlBulkCopyProvider.BuildCopyCommand, which validates via SqlIdentifierValidator
        // before escaping. A backtick can't break out of MySQL's own backtick-doubling escape,
        // but this still closes the gap so both providers enforce the same identifier contract.
        using var conn = OpenOrSkip();

        using var reader = MakeTable(1).CreateDataReader();
        Assert.Throws<ArgumentException>(() => new MySqlBulkCopyProvider().CopyToServer(
            conn, null, "products; DROP TABLE users; --", reader, new BulkCopyOptions()));
    }

    [Fact]
    public void CopyToServer_InvalidSchemaName_ThrowsArgumentException()
    {
        using var conn = OpenOrSkip();

        using var reader = MakeTable(1).CreateDataReader();
        Assert.Throws<ArgumentException>(() => new MySqlBulkCopyProvider().CopyToServer(
            conn, "jauntybench; DROP TABLE users; --", "products", reader, new BulkCopyOptions()));
    }

    // R16: rowsPerChunk previously ignored options.BatchSize entirely, always chunking purely by
    // MaxParametersPerStatement / columnCount (2000 rows for this 1-column reader), so callers had
    // no way to shrink the per-statement row count (e.g. to bound statement/packet size or
    // transaction lock duration). Runs against a real in-memory SQLite connection (SQLite accepts
    // the provider's backtick-quoted multi-row INSERT syntax) wrapped in a spy that records every
    // command CopyToServer creates, so the test can observe exactly how many chunk commands were
    // built and how many parameters (i.e. rows) each one was bound with - not reachable from a
    // MySQL-only integration test that just checks the final row count.
    [Fact]
    public void CopyToServer_RespectsBatchSizeOption_ChunksRowsAccordingly()
    {
        using var inner = CreateSpyInnerConnection();
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 7);

        int inserted = new MySqlBulkCopyProvider().CopyToServer(connection, null, "widgets", reader, new BulkCopyOptions { BatchSize = 3 });

        Assert.Equal(7, inserted);
        // 7 rows at BatchSize=3 -> two full 3-row chunks bound onto the same reused command,
        // followed by a separate 1-row tail command.
        Assert.Equal(2, connection.CreatedCommands.Count);
        Assert.Equal(3, connection.CreatedCommands[0].ParameterCount);
        Assert.Equal(1, connection.CreatedCommands[1].ParameterCount);
    }

    // AUD-R35-067: BuildChunkCommand guarded the assignment with `if (options.Timeout > 0)`, so
    // Timeout = 0 - documented on BulkCopyOptions as "use 0 for no timeout" - never reached the
    // command and the chunk kept the ADO.NET default. SqlServerBulkCopyProvider assigns
    // unconditionally, so the same option value meant opposite things on the two providers.

    [Fact]
    public void CopyToServer_TimeoutZero_MeansNoTimeout_NotTheProviderDefault()
    {
        using var inner = CreateSpyInnerConnection();
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 2);

        new MySqlBulkCopyProvider().CopyToServer(
            connection, null, "widgets", reader, new BulkCopyOptions { Timeout = 0 });

        Assert.Equal(0, connection.CreatedCommands[0].AssignedTimeout);
    }

    [Fact]
    public async Task CopyToServerAsync_TimeoutZero_MeansNoTimeout_NotTheProviderDefault()
    {
        using var inner = CreateSpyInnerConnection();
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 2);

        await new MySqlBulkCopyProvider().CopyToServerAsync(
            connection, null, "widgets", reader, new BulkCopyOptions { Timeout = 0 }, CancellationToken.None);

        Assert.Equal(0, connection.CreatedCommands[0].AssignedTimeout);
    }

    [Fact]
    public void CopyToServer_APositiveTimeout_StillReachesTheCommand()
    {
        using var inner = CreateSpyInnerConnection();
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 2);

        new MySqlBulkCopyProvider().CopyToServer(
            connection, null, "widgets", reader, new BulkCopyOptions { Timeout = 77 });

        Assert.Equal(77, connection.CreatedCommands[0].AssignedTimeout);
    }

    /// <summary>
    /// A negative is the one value still skipped: CommandTimeout rejects it, and the option has no
    /// documented meaning for it.
    /// </summary>
    [Fact]
    public void CopyToServer_ANegativeTimeout_IsNotAssigned()
    {
        using var inner = CreateSpyInnerConnection();
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 2);

        new MySqlBulkCopyProvider().CopyToServer(
            connection, null, "widgets", reader, new BulkCopyOptions { Timeout = -1 });

        Assert.Null(connection.CreatedCommands[0].AssignedTimeout);
    }

    [Fact]
    public async Task CopyToServerAsync_RespectsBatchSizeOption_ChunksRowsAccordingly()
    {
        using var inner = CreateSpyInnerConnection();
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 7);

        int inserted = await new MySqlBulkCopyProvider().CopyToServerAsync(
            connection, null, "widgets", reader, new BulkCopyOptions { BatchSize = 3 }, CancellationToken.None);

        Assert.Equal(7, inserted);
        Assert.Equal(2, connection.CreatedCommands.Count);
        Assert.Equal(3, connection.CreatedCommands[0].ParameterCount);
        Assert.Equal(1, connection.CreatedCommands[1].ParameterCount);
    }

    // AUD-R18 batch-6: BuildChunkCommand used to take only a bare tableName, so INSERT INTO always
    // targeted the unqualified `table` - any entity mapped to a non-default schema silently landed
    // in the wrong place. Uses a real ATTACH'd SQLite database as the "schema" so the generated
    // `schema`.`table` command text is not just asserted but actually executes successfully.
    [Fact]
    public void CopyToServer_SchemaQualifiedTable_GeneratesSchemaQualifiedInsertAndInsertsAllRows()
    {
        using var inner = CreateSpyInnerConnection();
        using (var attach = inner.CreateCommand())
        {
            attach.CommandText = "ATTACH DATABASE ':memory:' AS myschema";
            attach.ExecuteNonQuery();
        }
        using (var create = inner.CreateCommand())
        {
            create.CommandText = "CREATE TABLE myschema.widgets (value INTEGER)";
            create.ExecuteNonQuery();
        }
        var connection = new SpyingDbConnection(inner);
        using var reader = new IntSequenceReader(rowCount: 7);

        int inserted = new MySqlBulkCopyProvider().CopyToServer(connection, "myschema", "widgets", reader, new BulkCopyOptions { BatchSize = 3 });

        Assert.Equal(7, inserted);
        Assert.Contains("`myschema`.`widgets`", connection.CreatedCommands[0].CommandText);

        using var count = inner.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM myschema.widgets";
        Assert.Equal(7L, Convert.ToInt64(count.ExecuteScalar()));
    }

    /// <summary>
    /// An open, real in-memory SQLite connection pre-loaded with the "widgets" table, for
    /// <see cref="SpyingDbConnection"/> to wrap. SQLite accepts the provider's backtick-quoted
    /// multi-row INSERT syntax and gives <see cref="SpyingDbCommand"/> a real
    /// <see cref="DbParameterCollection"/> and real ExecuteNonQuery row counts for free, instead of
    /// hand-rolling a fake ADO.NET provider.
    /// </summary>
    private static SQLiteConnection CreateSpyInnerConnection()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE widgets (value INTEGER)";
        create.ExecuteNonQuery();
        return connection;
    }
}

#region Minimal In-Memory Fakes For Direct BatchSize/Chunk-Size Observation

/// <summary>
/// Wraps a real, already-open <see cref="DbConnection"/> and records every
/// <see cref="SpyingDbCommand"/> <see cref="MySqlBulkCopyProvider"/> creates through it, so tests
/// can inspect exactly how many chunk commands were built and how many parameters (rows) each one
/// was bound with, while all actual SQL execution still runs for real against the inner connection.
/// </summary>
internal sealed class SpyingDbConnection : DbConnection
{
    private readonly DbConnection _inner;

    public SpyingDbConnection(DbConnection inner) => _inner = inner;

    public List<SpyingDbCommand> CreatedCommands { get; } = new();

#pragma warning disable CS8765 // base DbConnection.ConnectionString setter is [AllowNull]; not using the attribute here since its netstandard2.0 SDK polyfill is file-scoped and inaccessible outside its own file.
    public override string ConnectionString
    {
        get => _inner.ConnectionString;
        set => _inner.ConnectionString = value;
    }
#pragma warning restore CS8765

    public override string Database => _inner.Database;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override ConnectionState State => _inner.State;

    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
    public override void Close() => _inner.Close();
    public override void Open() => _inner.Open();

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);

    protected override DbCommand CreateDbCommand()
    {
        var command = new SpyingDbCommand(_inner.CreateCommand());
        CreatedCommands.Add(command);
        return command;
    }
}

/// <summary>
/// Wraps a real <see cref="DbCommand"/>, forwarding everything to it (including its real
/// <see cref="DbParameterCollection"/>) so tests can read back exactly how many parameters
/// <see cref="MySqlBulkCopyProvider"/> bound onto each chunk command.
/// </summary>
internal sealed class SpyingDbCommand : DbCommand
{
    private readonly DbCommand _inner;

    public SpyingDbCommand(DbCommand inner) => _inner = inner;

    /// <summary>
    /// Snapshotted on <see cref="Dispose(bool)"/> (the provider disposes each chunk command as
    /// soon as it's done with it), so tests can still read the parameter/row count of a command
    /// after <see cref="MySqlBulkCopyProvider"/> has already disposed it.
    /// </summary>
    public int ParameterCount => _disposedParameterCount ?? _inner.Parameters.Count;
    private int? _disposedParameterCount;

    private string? _disposedCommandText;

#pragma warning disable CS8765 // base DbCommand.CommandText setter is [AllowNull]; not using the attribute here since its netstandard2.0 SDK polyfill is file-scoped and inaccessible outside its own file.
    public override string CommandText
    {
        get => _disposedCommandText ?? _inner.CommandText;
        set => _inner.CommandText = value;
    }
#pragma warning restore CS8765

    /// <summary>
    /// AUD-R35-067: recorded on assignment, and never cleared, so a test can tell "the provider set
    /// it to 0" apart from "the provider never set it and the ADO.NET default happens to be 0".
    /// </summary>
    public int? AssignedTimeout { get; private set; }

    public override int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set
        {
            AssignedTimeout = value;
            _inner.CommandTimeout = value;
        }
    }

    public override CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    public override bool DesignTimeVisible
    {
        get => _inner.DesignTimeVisible;
        set => _inner.DesignTimeVisible = value;
    }

    public override UpdateRowSource UpdatedRowSource
    {
        get => _inner.UpdatedRowSource;
        set => _inner.UpdatedRowSource = value;
    }

    protected override DbConnection? DbConnection
    {
        get => _inner.Connection;
        set { /* fixed to the inner command's own connection */ }
    }

    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _inner.Transaction;
        set => _inner.Transaction = value;
    }

    public override void Cancel() => _inner.Cancel();
    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();
    public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => _inner.ExecuteNonQueryAsync(cancellationToken);
    public override object? ExecuteScalar() => _inner.ExecuteScalar();
    public override void Prepare() => _inner.Prepare();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _inner.ExecuteReader(behavior);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposedParameterCount = _inner.Parameters.Count;
            _disposedCommandText = _inner.CommandText;
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// An <see cref="IDataReader"/> yielding <c>rowCount</c> rows of a single int column, just enough
/// for <see cref="MySqlBulkCopyProvider"/> to drive its row-buffering/chunking loop.
/// </summary>
internal sealed class IntSequenceReader(int rowCount) : IDataReader
{
    private int _index = -1;

    public object this[int i] => _index;
    public object this[string name] => _index;
    public int Depth => 0;
    public bool IsClosed { get; private set; }
    public int RecordsAffected => 0;
    public int FieldCount => 1;

    public void Close() => IsClosed = true;
    public void Dispose() => IsClosed = true;
    public bool GetBoolean(int i) => throw new NotImplementedException();
    public byte GetByte(int i) => throw new NotImplementedException();
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
    public char GetChar(int i) => throw new NotImplementedException();
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
    public IDataReader GetData(int i) => throw new NotImplementedException();
    public string GetDataTypeName(int i) => throw new NotImplementedException();
    public DateTime GetDateTime(int i) => throw new NotImplementedException();
    public decimal GetDecimal(int i) => throw new NotImplementedException();
    public double GetDouble(int i) => throw new NotImplementedException();
    public Type GetFieldType(int i) => typeof(int);
    public float GetFloat(int i) => throw new NotImplementedException();
    public Guid GetGuid(int i) => throw new NotImplementedException();
    public short GetInt16(int i) => throw new NotImplementedException();
    public int GetInt32(int i) => _index;
    public long GetInt64(int i) => throw new NotImplementedException();
    public string GetName(int i) => "value";
    public int GetOrdinal(string name) => 0;
    public DataTable? GetSchemaTable() => null;
    public string GetString(int i) => throw new NotImplementedException();
    public object GetValue(int i) => _index;
    public int GetValues(object[] values) { values[0] = _index; return 1; }
    public bool IsDBNull(int i) => false;
    public bool NextResult() => false;
    public bool Read() => ++_index < rowCount;
}

#endregion
