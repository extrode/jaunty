using System.Data;
using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Integration tests for QueryPartialListAsync - returns raw rows as column-name-keyed dictionaries.
/// AUD-R11 batch-01: this method had zero tests of any kind.
/// </summary>
public class QueryPartialListAsyncTests : IClassFixture<DialectFixture>
{
    private static SQLiteConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE apartial_list_widgets (id INTEGER PRIMARY KEY, name TEXT, price REAL);
            INSERT INTO apartial_list_widgets VALUES (1, 'Widget A', 9.99);
            INSERT INTO apartial_list_widgets VALUES (2, 'Widget B', 19.99);
            INSERT INTO apartial_list_widgets VALUES (3, NULL, 29.99);");
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_Sql_ReturnsAllRowsAsDictionaries(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryPartialListAsync("SELECT id, name, price FROM apartial_list_widgets ORDER BY id");

        Assert.Equal(3, results.Count);
        Assert.Equal(1L, results[0]["id"]);
        Assert.Equal("Widget A", results[0]["name"]);
        Assert.Equal(9.99, results[0]["price"]);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_Sql_CoercesDbNullToNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryPartialListAsync("SELECT id, name FROM apartial_list_widgets ORDER BY id");

        Assert.Null(results[2]["name"]);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_Sql_HonorsCancellationToken(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var cts = new CancellationTokenSource();

        var results = await connection.QueryPartialListAsync(
            "SELECT id, name, price FROM apartial_list_widgets ORDER BY id", cts.Token);

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_SqlAndParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryPartialListAsync(
            "SELECT id, name, price FROM apartial_list_widgets WHERE id = @Id",
            new { Id = 2 });

        Assert.Single(results);
        Assert.Equal("Widget B", results[0]["name"]);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_SqlAndParameters_ReturnsEmptyListWhenNoMatch(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryPartialListAsync(
            "SELECT id, name, price FROM apartial_list_widgets WHERE id = @Id",
            new { Id = -999 });

        Assert.Empty(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_SqlAndCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE apartial_list_widgets SET name = 'TXN-SENTINEL' WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions(transaction: txn);
        var results = await connection.QueryPartialListAsync(
            "SELECT id, name FROM apartial_list_widgets WHERE id = 1", options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0]["name"]);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_SqlParametersAndCommandOptions_FiltersRowsUnderTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE apartial_list_widgets SET price = 100.00 WHERE id = 2";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions(transaction: txn);
        var results = await connection.QueryPartialListAsync(
            "SELECT id, name, price FROM apartial_list_widgets WHERE id = @Id",
            new { Id = 2 },
            options);

        Assert.Single(results);
        Assert.Equal(100.00, results[0]["price"]);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryPartialListAsync_SqlParametersAndCommandOptions_ReturnsEmptyListWhenNoMatch(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();
        var options = new CommandOptions(transaction: txn);

        var results = await connection.QueryPartialListAsync(
            "SELECT id, name, price FROM apartial_list_widgets WHERE id = @Id",
            new { Id = -999 },
            options);

        Assert.Empty(results);

        txn.Rollback();
    }

    // AUD-R12: QueryCoreListDirectAsync never assigned command.CommandType from options.CommandType,
    // so CommandOptions.AsStoredProcedure() was silently ignored and the command always executed
    // as CommandType.Text. RecordingDbConnection intercepts the CommandType setter (SQLite itself
    // rejects CommandType.StoredProcedure) so the test can observe what Jaunty actually applied.
    [Fact]
    public async Task QueryPartialListAsync_SqlAndCommandOptions_AppliesStoredProcedureCommandType()
    {
        using var inner = new SQLiteConnection("Data Source=:memory:");
        using var connection = new RecordingDbConnection(inner);
        connection.Open();
        Execute(connection, "CREATE TABLE recording_widgets (id INTEGER PRIMARY KEY)");

        var options = new CommandOptions(commandType: CommandType.StoredProcedure);
        var results = await connection.QueryPartialListAsync("SELECT id FROM recording_widgets", options);

        Assert.Empty(results);
        Assert.NotNull(connection.LastCommand);
        Assert.Equal(CommandType.StoredProcedure, connection.LastCommand!.AppliedCommandType);
    }
}

/// <summary>
/// A real <see cref="DbConnection"/> wrapper whose commands record the <see cref="CommandType"/>
/// Jaunty applies without forwarding it to the inner SQLite command (which rejects
/// <see cref="CommandType.StoredProcedure"/>), so tests can observe propagation without needing a
/// server that actually supports stored procedures.
/// </summary>
internal sealed class RecordingDbConnection : DbConnection
{
    private readonly DbConnection _inner;

    public RecordingDbConnection(DbConnection inner) => _inner = inner;

    public RecordingDbCommand? LastCommand { get; private set; }

#pragma warning disable CS8765
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

    protected override DbCommand CreateDbCommand() => LastCommand = new RecordingDbCommand(_inner.CreateCommand(), this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}

internal sealed class RecordingDbCommand : DbCommand
{
    private readonly DbCommand _inner;
    private readonly RecordingDbConnection _owner;

    public RecordingDbCommand(DbCommand inner, RecordingDbConnection owner)
    {
        _inner = inner;
        _owner = owner;
    }

    public CommandType AppliedCommandType { get; private set; } = CommandType.Text;

#pragma warning disable CS8765
    public override string CommandText
    {
        get => _inner.CommandText;
        set => _inner.CommandText = value;
    }
#pragma warning restore CS8765

    public override int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set => _inner.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => _inner.CommandType;
        set => AppliedCommandType = value;
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
        get => _owner;
        set { /* fixed to the owning wrapper */ }
    }

    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _inner.Transaction;
        set => _inner.Transaction = value;
    }

    public override void Cancel() => _inner.Cancel();
    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();
    public override void Prepare() { }
    public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
    public override object? ExecuteScalar() => _inner.ExecuteScalar();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _inner.ExecuteReader(behavior);

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
        _inner.ExecuteReaderAsync(behavior, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
