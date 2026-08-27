using System.Data;
using Jaunty.Dialects;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// The failure paths of <see cref="SqlDialectFactory"/>. The 2026-08-27 mutation baseline put 30
/// NoCoverage mutants in SqlDialectFactory.cs, most of them on the unresolvable-connection
/// message (lines 87-92) and the null-dialect guard (line 105) — code that runs only when a
/// caller gets it wrong, which nothing exercised.
///
/// <para>
/// Neither case touches the registration dictionary: the throw happens before any write, so
/// these belong in the parallel assembly. The cache-invalidation branch of
/// <c>RegisterDialect</c> does mutate process-wide state and stays with the serial suite.
/// </para>
/// </summary>
public class SqlDialectFactoryGuardTests
{
    private sealed class UnknownConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }

    [Fact]
    public void GetDialect_ForAnUnrecognisedConnectionType_Throws()
    {
        using var connection = new UnknownConnection();

        Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(connection));
    }

    [Fact]
    public void GetDialect_ForAnUnrecognisedConnectionType_NamesTheTypeAndHowToRegisterIt()
    {
        using var connection = new UnknownConnection();

        var ex = Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(connection));

        Assert.Contains(nameof(UnknownConnection), ex.Message);
        Assert.Contains("RegisterDialect", ex.Message);
        Assert.Contains("SqlConnection", ex.Message);
        Assert.Contains("NpgsqlConnection", ex.Message);
        Assert.Contains("MySqlConnection", ex.Message);
        Assert.Contains("SQLiteConnection", ex.Message);
        Assert.Contains("SqliteConnection", ex.Message);
    }

    [Fact]
    public void RegisterDialect_WithANullDialect_ThrowsOnTheArgumentRatherThanStoringIt()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => SqlDialectFactory.RegisterDialect("NeverRegisteredConnection", null!));

        Assert.Equal("dialect", ex.ParamName);
    }

    [Fact]
    public void RegisterDialect_WithANullDialect_LeavesTheTypeStillUnresolvable()
    {
        Assert.Throws<ArgumentNullException>(
            () => SqlDialectFactory.RegisterDialect(nameof(UnknownConnection), null!));

        using var connection = new UnknownConnection();

        Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(connection));
    }
}
