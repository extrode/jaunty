using System.Data;
using Extrode.Jaunty.Dialects;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

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

    /// <summary>
    /// Exposes a property named the same as one of the conventional decorator property names
    /// (<c>InnerConnection</c>), but of the wrong type - the probe must skip it rather than crash
    /// or mismatch, and fall through to the private field that actually holds the wrapped
    /// connection.
    /// </summary>
    private sealed class WrongTypedPropertyWrapper : IDbConnection
    {
        public string InnerConnection { get; set; } = string.Empty;

        private readonly IDbConnection _actual;

        public WrongTypedPropertyWrapper(IDbConnection actual) => _actual = actual;

        public string ConnectionString { get => _actual.ConnectionString; set => _actual.ConnectionString = value; }
        public int ConnectionTimeout => _actual.ConnectionTimeout;
        public string Database => _actual.Database;
        public ConnectionState State => _actual.State;
        public IDbTransaction BeginTransaction() => _actual.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _actual.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _actual.ChangeDatabase(databaseName);
        public void Close() => _actual.Close();
        public IDbCommand CreateCommand() => _actual.CreateCommand();
        public void Open() => _actual.Open();
        public void Dispose() => _actual.Dispose();
    }

    [Fact]
    public void AProbedPropertyNameOfTheWrongTypeIsSkipped()
    {
        using var sqlite = new SqliteConnection("Data Source=:memory:");
        using var wrapper = new WrongTypedPropertyWrapper(sqlite);

        ISqlDialect dialect = SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(wrapper));

        Assert.IsType<SQLiteDialect>(dialect);
    }

    [Fact]
    public void RegisterDialect_CustomDialectForAlreadyCachedTypeName_TakesEffect()
    {
        using var sqlite = new SqliteConnection("Data Source=:memory:");

        try
        {
            ISqlDialect before = SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(sqlite));
            Assert.IsType<SQLiteDialect>(before);

            var custom = new SqlServerDialect();
            SqlDialectFactory.RegisterDialect(nameof(SqliteConnection), custom);

            ISqlDialect after = SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(sqlite));
            Assert.Same(custom, after);
        }
        finally
        {
            SqlDialectFactory.ResetRegistrations();
        }
    }
}
