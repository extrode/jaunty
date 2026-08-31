using System.Data;
using System.Data.Common;
using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;
using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Tests for ImportDialectResolver: built-in auto-detection, custom registration,
/// explicit dialect override, and fallback to SQLite for unknown connections.
/// Accessible because Jaunty.FlatFiles.DuckDB has InternalsVisibleTo this test project.
/// </summary>
/// <remarks>
/// ImportDialectResolver.Register mutates a static, process-wide registry with no corresponding
/// Unregister/Reset API, so every registration below persists for the rest of the test process.
/// Registration keys and connection type names are prefixed with this class's name specifically
/// to make an accidental substring collision with an unrelated connection type name elsewhere in
/// the process effectively impossible.
/// </remarks>
public class ImportDialectResolverTests
{
    // ------------------------------------------------------------------
    // Built-in detection: SQLite
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_SqliteConnection_ReturnsSqliteDialect()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        var dialect = ImportDialectResolver.Resolve(conn, null);

        Assert.IsType<SqliteImportDialect>(dialect);
    }

    // ------------------------------------------------------------------
    // Built-in detection: SQL Server (and the MySQL false-positive regression)
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_SqlConnectionTypeName_ReturnsSqlServerDialect()
    {
        using var conn = new SqlConnection();
        var dialect = ImportDialectResolver.Resolve(conn, null);

        Assert.IsType<SqlServerImportDialect>(dialect);
    }

    [Fact]
    public void Resolve_MySqlConnectionTypeName_DoesNotFalsePositiveAsSqlServer()
    {
        // "MySqlConnection" contains "SqlConnection" as a substring; the resolver must not match it
        // to SqlServerImportDialect.
        //
        // AUD-R26: with no MySql dialect registered this used to fall through to the silent
        // SqliteImportDialect default, so the assertion was "not SQL Server, therefore SQLite". It
        // now reports the type as unrecognised, which demonstrates the same thing more directly -
        // the name was not misread as SQL Server - and is the correct outcome for a MySQL target,
        // which SQLite's DDL and conflict syntax would have failed on anyway.
        using var conn = new MySqlConnection();

        var ex = Assert.Throws<InvalidOperationException>(() => ImportDialectResolver.Resolve(conn, null));

        Assert.Contains(nameof(MySqlConnection), ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Thread safety of the mutable registry
    // ------------------------------------------------------------------

    [Fact]
    public void Register_ConcurrentRegistrations_DoNotThrow()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 50, i =>
        {
            try
            {
                ImportDialectResolver.Register($"ImportDialectResolverTestsConcurrentKey{i}", new StubImportDialect());
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
    }

    [Fact]
    public void Resolve_ConcurrentWithRegister_DoesNotThrow()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        using var conn = new SqliteConnection("Data Source=:memory:");

        Parallel.For(0, 50, i =>
        {
            try
            {
                if (i % 2 == 0)
                    ImportDialectResolver.Register($"ImportDialectResolverTestsConcurrentResolveKey{i}", new StubImportDialect());
                else
                    ImportDialectResolver.Resolve(conn, null);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
    }

    // ------------------------------------------------------------------
    // Explicit dialect always wins over auto-detection
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_ExplicitDialectProvided_ReturnsThatDialect()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        var explicit_ = new StubImportDialect();

        var dialect = ImportDialectResolver.Resolve(conn, explicit_);

        Assert.Same(explicit_, dialect);
    }

    [Fact]
    public void Resolve_ExplicitDialect_OverridesEvenForSqlite()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        var override_ = new StubImportDialect();

        var result = ImportDialectResolver.Resolve(conn, override_);

        Assert.IsType<StubImportDialect>(result);
    }

    // ------------------------------------------------------------------
    // Unknown connection types
    // ------------------------------------------------------------------

    /// <summary>
    /// AUD-R26 (batch 7). This asserted the silent fallback to <see cref="SqliteImportDialect"/>,
    /// which meant a MySQL, MariaDB, Oracle or DuckDB target with no registered dialect was handed
    /// SQLite's SQL: SQLite type names (TEXT, INTEGER, REAL) in the generated DDL and SQLite's
    /// INSERT OR IGNORE / ON CONFLICT ... DO UPDATE conflict syntax. On MySQL every one of those is
    /// a syntax error arriving from the provider with no hint that dialect detection caused it.
    /// A silent default is only defensible when the default is broadly correct, and SQLite's is the
    /// narrowest of the three built in here.
    /// </summary>
    [Fact]
    public void Resolve_UnknownConnectionType_ThrowsNamingTheTypeAndTheWayOut()
    {
        using var conn = new FallbackDbConnection();

        var ex = Assert.Throws<InvalidOperationException>(() => ImportDialectResolver.Resolve(conn, null));

        Assert.Contains(nameof(FallbackDbConnection), ex.Message, StringComparison.Ordinal);

        // AUD-R35-249: the way out has to be one a caller outside this assembly can take.
        // ImportDialectResolver is internal, so the old "Register(...)" advice compiled for the
        // test project and nobody else.
        Assert.Contains("ImportOptions.Dialect", ex.Message, StringComparison.Ordinal);
        Assert.Contains("IImportDialect", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ImportDialectResolver.Register", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>An explicitly supplied dialect still short-circuits everything, unknown type or not.</summary>
    [Fact]
    public void Resolve_UnknownConnectionType_WithExplicitDialect_UsesIt()
    {
        using var conn = new FallbackDbConnection();
        var explicitDialect = SqlServerImportDialect.Instance;

        Assert.Same(explicitDialect, ImportDialectResolver.Resolve(conn, explicitDialect));
    }

    /// <summary>
    /// AUD-R26, same finding's secondary half: the custom registry is a ConcurrentDictionary that
    /// was iterated with foreach, whose order is unspecified. When two registered substrings both
    /// match a connection type name, which dialect won was arbitrary and could differ between runs.
    /// The longer key is the more specific match and now wins deterministically.
    /// </summary>
    [Fact]
    public void Resolve_TwoRegisteredSubstringsBothMatch_ThePreciseOneWins()
    {
        ImportDialectResolver.Register("ImportDialectResolverTestsAmbiguous", SqliteImportDialect.Instance);
        ImportDialectResolver.Register(
            "ImportDialectResolverTestsAmbiguousSpecific", PostgreSqlImportDialect.Instance);

        using var conn = new ImportDialectResolverTestsAmbiguousSpecificConnection();

        // Both keys are substrings of this type's full name. Without a defined order this assertion
        // passes or fails depending on dictionary internals.
        Assert.IsType<PostgreSqlImportDialect>(ImportDialectResolver.Resolve(conn, null));
    }

    private sealed class ImportDialectResolverTestsAmbiguousSpecificConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override System.Data.Common.DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override System.Data.Common.DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    // ------------------------------------------------------------------
    // Custom registration
    // ------------------------------------------------------------------

    [Fact]
    public void Register_CustomKey_ResolvesForMatchingConnectionTypeName()
    {
        var custom = new StubImportDialect();
        // "ImportDialectResolverTestsUnknownDb" is a substring of ImportDialectResolverTestsUnknownDbConnection's full type name
        ImportDialectResolver.Register("ImportDialectResolverTestsUnknownDb", custom);

        using var conn = new ImportDialectResolverTestsUnknownDbConnection();
        var resolved = ImportDialectResolver.Resolve(conn, null);

        Assert.Same(custom, resolved);
    }

    [Fact]
    public void Register_NullKey_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() =>
            ImportDialectResolver.Register(null!, new StubImportDialect()));

    [Fact]
    public void Register_NullDialect_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() =>
            ImportDialectResolver.Register("AnyKey", null!));

    [Fact]
    public void Register_OverwriteExistingKey_ReplacesRegistration()
    {
        var first = new StubImportDialect();
        var second = new StubImportDialect();

        ImportDialectResolver.Register("ImportDialectResolverTestsOverwriteKey", first);
        ImportDialectResolver.Register("ImportDialectResolverTestsOverwriteKey", second);

        using var conn = new ImportDialectResolverTestsOverwriteKeyConnection();
        var resolved = ImportDialectResolver.Resolve(conn, null);

        // The second registration must win, proving Register() overwrites the existing
        // entry for the key instead of ignoring the second call.
        Assert.Same(second, resolved);
        Assert.NotSame(first, resolved);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private sealed class StubImportDialect : IImportDialect
    {
        public string MapClrTypeToSqlType(Type clrType) => "TEXT";

        public string GenerateInsertSql(
            string tableName,
            IReadOnlyList<string> columnNames,
            IReadOnlyList<string> parameterNames,
            ConflictStrategy conflictStrategy,
            string? keyColumnName) => string.Empty;

        public string GenerateCreateTableSql(
            string tableName,
            IReadOnlyList<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns)
            => string.Empty;
    }

    /// <summary>
    /// A DbConnection whose type name contains "ImportDialectResolverTestsUnknownDb" to trigger the
    /// fallback / custom-registration path in ImportDialectResolver.
    /// </summary>
    private sealed class ImportDialectResolverTestsUnknownDbConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotSupportedException();
    }
    /// <summary>
    /// A DbConnection with no registered key - triggers the SQLite fallback path.
    /// Does NOT contain "ImportDialectResolverTestsUnknownDb" in its name to avoid matching the custom registration test.
    /// </summary>
    private sealed class FallbackDbConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotSupportedException();
    }

    /// <summary>
    /// A DbConnection whose type name contains "ImportDialectResolverTestsOverwriteKey" - used to verify
    /// that a second Register() call for the same key replaces the first registration.
    /// </summary>
    private sealed class ImportDialectResolverTestsOverwriteKeyConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotSupportedException();
    }

}

/// <summary>
/// A fake connection type named exactly "SqlConnection" (top-level, not nested) so its
/// <c>GetType().FullName</c> ends with ".SqlConnection" — mirroring the real
/// <c>System.Data.SqlClient.SqlConnection</c> / <c>Microsoft.Data.SqlClient.SqlConnection</c> shape
/// that <see cref="ImportDialectResolver"/> detects.
/// </summary>
internal sealed class SqlConnection : DbConnection
{
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => string.Empty;
    public override string DataSource => string.Empty;
    public override string ServerVersion => string.Empty;
    public override ConnectionState State => ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
        => throw new NotSupportedException();
}

/// <summary>
/// A fake connection type named "MySqlConnection" (top-level, not nested) so its
/// <c>GetType().FullName</c> ends with "...MySqlConnection" — reproducing the real
/// <c>MySql.Data.MySqlClient.MySqlConnection</c> shape whose type name contains "SqlConnection"
/// as a substring without being preceded by a namespace dot.
/// </summary>
internal sealed class MySqlConnection : DbConnection
{
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => string.Empty;
    public override string DataSource => string.Empty;
    public override string ServerVersion => string.Empty;
    public override ConnectionState State => ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
        => throw new NotSupportedException();
}
