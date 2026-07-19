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
    // Fallback for unknown connection types
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_UnknownConnectionType_FallsBackToSqliteDialect()
    {
        using var conn = new FallbackDbConnection();
        var dialect = ImportDialectResolver.Resolve(conn, null);

        // Built-in fallback is SqliteImportDialect
        Assert.IsType<SqliteImportDialect>(dialect);
    }

    // ------------------------------------------------------------------
    // Custom registration
    // ------------------------------------------------------------------

    [Fact]
    public void Register_CustomKey_ResolvesForMatchingConnectionTypeName()
    {
        var custom = new StubImportDialect();
        // "UnknownDb" is a substring of UnknownDbConnection's full type name
        ImportDialectResolver.Register("UnknownDb", custom);

        using var conn = new UnknownDbConnection();
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

        ImportDialectResolver.Register("OverwriteTestKey", first);
        ImportDialectResolver.Register("OverwriteTestKey", second);

        using var conn = new OverwriteTestKeyConnection();
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
    /// A DbConnection whose type name contains "UnknownDb" to trigger the
    /// fallback / custom-registration path in ImportDialectResolver.
    /// </summary>
    private sealed class UnknownDbConnection : DbConnection
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
    /// Does NOT contain "UnknownDb" in its name to avoid matching the custom registration test.
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
    /// A DbConnection whose type name contains "OverwriteTestKey" - used to verify
    /// that a second Register() call for the same key replaces the first registration.
    /// </summary>
    private sealed class OverwriteTestKeyConnection : DbConnection
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
