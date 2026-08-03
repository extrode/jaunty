using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection.BulkCopy;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// AUD-R35-215 (the rollback must not mask the failure that caused it), AUD-R35-216 (the async
/// copy path drives the transaction and connection asynchronously), AUD-R35-217, AUD-R35-218 and
/// AUD-R35-219 (what each provider's <c>IsSupported</c> promises). Server-free: the transaction
/// tests wrap a real in-memory SQLite connection, the rest read the providers' own reflection.
/// </summary>
public class BulkCopyProviderLifecycleTests
{
    private static SQLiteConnection OpenInner()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateWidgets(connection);
        return connection;
    }

    private static void CreateWidgets(DbConnection connection)
    {
        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE IF NOT EXISTS widgets (value INTEGER)";
        create.ExecuteNonQuery();
    }

    private static int Copy(TransactionSpyConnection connection, string table = "widgets")
    {
        using var reader = new IntSequenceReader(rowCount: 4);
        return new MySqlBulkCopyProvider().CopyToServer(
            connection, null, table, reader, new BulkCopyOptions { BatchSize = 2 });
    }

    private static async Task<int> CopyAsync(TransactionSpyConnection connection, string table = "widgets")
    {
        using var reader = new IntSequenceReader(rowCount: 4);
        return await new MySqlBulkCopyProvider().CopyToServerAsync(
            connection, null, table, reader, new BulkCopyOptions { BatchSize = 2 }, CancellationToken.None);
    }

    // ------------------------------------------------------------------
    // AUD-R35-215: a throwing rollback must not replace the real failure
    // ------------------------------------------------------------------

    [Fact]
    public void CopyToServer_WhenCommitAndRollbackBothThrow_TheCommitFailureSurvives()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner) { ThrowOnCommit = true, ThrowOnRollback = true };

        var ex = Assert.Throws<InvalidOperationException>(() => Copy(connection));

        Assert.Equal("commit boom", ex.Message);
    }

    [Fact]
    public async Task CopyToServerAsync_WhenCommitAndRollbackBothThrow_TheCommitFailureSurvives()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner) { ThrowOnCommit = true, ThrowOnRollback = true };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CopyAsync(connection));

        Assert.Equal("commit boom", ex.Message);
    }

    [Fact]
    public void CopyToServer_WhenTheCopyFails_RollsBackAndReportsTheCopyFailure()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner);

        Assert.ThrowsAny<Exception>(() => Copy(connection, "no_such_table"));

        Assert.Contains("Rollback", connection.Calls);
        Assert.DoesNotContain("Commit", connection.Calls);
    }

    [Fact]
    public void CopyToServer_WhenOnlyTheRollbackThrows_TheCopyFailureSurvives()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner) { ThrowOnRollback = true };

        var ex = Assert.ThrowsAny<Exception>(() => Copy(connection, "no_such_table"));

        Assert.DoesNotContain("rollback boom", ex.Message);
    }

    [Fact]
    public void CopyToServer_WhenNothingThrows_CommitsAndInsertsEveryRow()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner);

        Assert.Equal(4, Copy(connection));
        Assert.Contains("Commit", connection.Calls);
        Assert.DoesNotContain("Rollback", connection.Calls);
    }

    // ------------------------------------------------------------------
    // AUD-R35-216: the async path uses the async members
    // ------------------------------------------------------------------

    [Fact]
    public void CopyToServer_UsesTheSynchronousTransactionMembers()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner);

        Copy(connection);

        Assert.Contains("BeginTransaction", connection.Calls);
        Assert.Contains("Commit", connection.Calls);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task CopyToServerAsync_BeginsAndCommitsAsynchronously()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner);

        Assert.Equal(4, await CopyAsync(connection));

        Assert.Contains("BeginTransactionAsync", connection.Calls);
        Assert.Contains("CommitAsync", connection.Calls);
        Assert.DoesNotContain("BeginTransaction", connection.Calls);
        Assert.DoesNotContain("Commit", connection.Calls);
    }

    [Fact]
    public async Task CopyToServerAsync_RollsBackAsynchronously()
    {
        using var inner = OpenInner();
        var connection = new TransactionSpyConnection(inner);

        await Assert.ThrowsAnyAsync<Exception>(() => CopyAsync(connection, "no_such_table"));

        Assert.Contains("RollbackAsync", connection.Calls);
        Assert.DoesNotContain("Rollback", connection.Calls);
    }

    [Fact]
    public async Task CopyToServerAsync_WhenItOpenedTheConnection_ClosesItAsynchronously()
    {
        using var inner = OpenInner();
        inner.Close();
        var connection = new TransactionSpyConnection(inner);

        Assert.Equal(4, await CopyAsync(connection));

        Assert.Contains("CloseAsync", connection.Calls);
        Assert.DoesNotContain("Close", connection.Calls);
    }
#endif

    [Fact]
    public void CopyToServer_WhenItOpenedTheConnection_ClosesIt()
    {
        using var inner = OpenInner();
        inner.Close();
        var connection = new TransactionSpyConnection(inner);

        Assert.Equal(4, Copy(connection));

        Assert.Contains("Close", connection.Calls);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    // ------------------------------------------------------------------
    // AUD-R35-217 / 218 / 219: what IsSupported promises
    // ------------------------------------------------------------------

    [Fact]
    public void MySqlProvider_IsSupported_IsAlwaysTrue()
    {
        Assert.True(new MySqlBulkCopyProvider().IsSupported);
    }

    [Fact]
    public void SqlServerProvider_IsSupported_ReportsOnEveryMemberTheCopyPathsRequire()
    {
        Assert.True(Resolved<SqlServerBulkCopyProvider>("SqlBulkCopyType"));
        Assert.True(Resolved<SqlServerBulkCopyProvider>("SqlBulkCopyOptionsType"));
        Assert.True(Resolved<SqlServerBulkCopyProvider>("SqlConnectionType"));
        Assert.True(Resolved<SqlServerBulkCopyProvider>("WriteToServerMethod"));
        Assert.True(Resolved<SqlServerBulkCopyProvider>("WriteToServerAsyncMethod"));

        Assert.True(new SqlServerBulkCopyProvider().IsSupported);
    }

    [Fact]
    public void SqlServerProvider_IsSupported_TracksTheReflectedMembersRatherThanTheTypeAlone()
    {
        MethodInfo write = typeof(BulkCopyProviderLifecycleTests).GetMethod(nameof(Resolved), BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.True(SqlServerBulkCopyProvider.AreMembersResolved(typeof(int), typeof(int), typeof(int), write, write));
        Assert.True(SqlServerBulkCopyProvider.AreMembersResolved(typeof(int), typeof(int), typeof(int), write, null));
        Assert.True(SqlServerBulkCopyProvider.AreMembersResolved(typeof(int), typeof(int), typeof(int), null, write));

        Assert.False(SqlServerBulkCopyProvider.AreMembersResolved(null, typeof(int), typeof(int), write, write));
        Assert.False(SqlServerBulkCopyProvider.AreMembersResolved(typeof(int), null, typeof(int), write, write));
        Assert.False(SqlServerBulkCopyProvider.AreMembersResolved(typeof(int), typeof(int), null, write, write));
        Assert.False(SqlServerBulkCopyProvider.AreMembersResolved(typeof(int), typeof(int), typeof(int), null, null));
    }

    [Fact]
    public void PostgreSqlProvider_ResolvesEveryMemberItsGuardNamesIncludingBeginBinaryImport()
    {
        Assert.True(Resolved<PostgreSqlBulkCopyProvider>("BeginBinaryImportMethod"));
        Assert.True(Resolved<PostgreSqlBulkCopyProvider>("StartRowMethod"));
        Assert.True(Resolved<PostgreSqlBulkCopyProvider>("WriteGenericMethod"));
        Assert.True(Resolved<PostgreSqlBulkCopyProvider>("CompleteMethod"));
        Assert.True(Resolved<PostgreSqlBulkCopyProvider>("WriteNullMethod"));

        Assert.True(new PostgreSqlBulkCopyProvider().IsSupported);
    }

    [Fact]
    public void PostgreSqlProvider_CopyToServer_ANonNpgsqlConnection_IsReportedAsSuch()
    {
        using var inner = OpenInner();
        using var reader = new IntSequenceReader(rowCount: 1);

        var ex = Assert.Throws<ArgumentException>(() => new PostgreSqlBulkCopyProvider()
            .CopyToServer(inner, null, "widgets", reader, new BulkCopyOptions()));

        Assert.Contains("NpgsqlConnection", ex.Message);
    }

    private static bool Resolved<TProvider>(string fieldName)
    {
        FieldInfo? field = typeof(TProvider).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return field!.GetValue(null) != null;
    }
}

/// <summary>
/// Wraps a real open <see cref="DbConnection"/> and records which transaction and connection
/// members the provider reaches for, optionally making <c>Commit</c> or <c>Rollback</c> throw.
/// </summary>
internal sealed class TransactionSpyConnection : DbConnection
{
    private readonly DbConnection _inner;

    public TransactionSpyConnection(DbConnection inner) => _inner = inner;

    public List<string> Calls { get; } = new();

    public bool ThrowOnCommit { get; set; }

    public bool ThrowOnRollback { get; set; }

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

    public override void Open()
    {
        Calls.Add("Open");
        _inner.Open();
        EnsureTable();
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        Calls.Add("OpenAsync");
        await _inner.OpenAsync(cancellationToken).ConfigureAwait(false);
        EnsureTable();
    }

    public override void Close()
    {
        Calls.Add("Close");
        _inner.Close();
    }

#if NET8_0_OR_GREATER
    public override async Task CloseAsync()
    {
        Calls.Add("CloseAsync");
        await _inner.CloseAsync().ConfigureAwait(false);
    }

    protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        Calls.Add("BeginTransactionAsync");
        DbTransaction real = await _inner.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
        return new TransactionSpy(this, real);
    }
#endif

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        Calls.Add("BeginTransaction");
        return new TransactionSpy(this, _inner.BeginTransaction(isolationLevel));
    }

    protected override DbCommand CreateDbCommand() => new UnwrappingCommand(_inner.CreateCommand());

    private void EnsureTable()
    {
        using var create = _inner.CreateCommand();
        create.CommandText = "CREATE TABLE IF NOT EXISTS widgets (value INTEGER)";
        create.ExecuteNonQuery();
    }
}

/// <summary>
/// Records which of <c>Commit</c>/<c>CommitAsync</c>/<c>Rollback</c>/<c>RollbackAsync</c> was
/// entered, and throws from either pair when the owning connection asks it to.
/// </summary>
internal sealed class TransactionSpy : DbTransaction
{
    private readonly TransactionSpyConnection _owner;

    public TransactionSpy(TransactionSpyConnection owner, DbTransaction inner)
    {
        _owner = owner;
        Inner = inner;
    }

    public DbTransaction Inner { get; }

    public override IsolationLevel IsolationLevel => Inner.IsolationLevel;

    protected override DbConnection? DbConnection => _owner;

    public override void Commit()
    {
        _owner.Calls.Add("Commit");
        if (_owner.ThrowOnCommit)
            throw new InvalidOperationException("commit boom");
        Inner.Commit();
    }

    public override void Rollback()
    {
        _owner.Calls.Add("Rollback");
        if (_owner.ThrowOnRollback)
            throw new InvalidOperationException("rollback boom");
        Inner.Rollback();
    }

#if NET8_0_OR_GREATER
    public override Task CommitAsync(CancellationToken cancellationToken)
    {
        _owner.Calls.Add("CommitAsync");
        if (_owner.ThrowOnCommit)
            throw new InvalidOperationException("commit boom");
        return Inner.CommitAsync(cancellationToken);
    }

    public override Task RollbackAsync(CancellationToken cancellationToken)
    {
        _owner.Calls.Add("RollbackAsync");
        if (_owner.ThrowOnRollback)
            throw new InvalidOperationException("rollback boom");
        return Inner.RollbackAsync(cancellationToken);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Inner.Dispose();

        base.Dispose(disposing);
    }
}

/// <summary>
/// Forwards to a real command, translating an assigned <see cref="TransactionSpy"/> back into the
/// underlying provider transaction the real command will accept.
/// </summary>
internal sealed class UnwrappingCommand : DbCommand
{
    private readonly DbCommand _inner;
    private DbTransaction? _assigned;

    public UnwrappingCommand(DbCommand inner) => _inner = inner;

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
        set { }
    }

    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _assigned;
        set
        {
            _assigned = value;
            _inner.Transaction = value is TransactionSpy spy ? spy.Inner : value;
        }
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
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
