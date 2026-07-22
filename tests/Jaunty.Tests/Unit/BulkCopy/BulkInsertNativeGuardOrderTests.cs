using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Tests.Entities;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// Regression coverage (round 16): SqlServerDialect.SupportsForeignKeyToggle is false, but the
/// native bulk-copy path honors ignoreConstraints itself via BulkCopyOptions.CheckConstraints and
/// never touches the session-level FK-toggle pragma. BulkInsertCore/BulkInsertAsyncCore used to
/// run the SupportsForeignKeyToggle guard before the native-bulk-copy eligibility check, so a
/// dialect that supports native bulk copy but not session-level toggling (e.g. SQL Server) could
/// never reach the native path with ignoreConstraints=true - it always threw NotSupportedException.
/// A fake dialect reproduces that exact combination without needing a live SQL Server instance.
/// </summary>
[Collection("Write Operations")]
public class BulkInsertNativeGuardOrderTests : IDisposable
{
    private readonly SqliteConnection _inner;
    private readonly bool _originalEnableNativeBulkCopy;
    private readonly int _originalMinimumRows;
    private readonly RecordingBulkCopyProvider _provider = new();

    public BulkInsertNativeGuardOrderTests()
    {
        _inner = new SqliteConnection("Data Source=:memory:");
        _inner.Open();
        using (var cmd = _inner.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, value INTEGER NOT NULL)";
            cmd.ExecuteNonQuery();
        }

        _originalEnableNativeBulkCopy = BulkCopyConfiguration.EnableNativeBulkCopy;
        _originalMinimumRows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy;
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 5;

        SqlDialectFactory.RegisterDialect(nameof(NativeOnlyConnection), new NativeOnlyDialect(_provider));
    }

    private static List<BulkTestEntity> MakeEntities(int n) =>
        [.. Enumerable.Range(0, n).Select(i => new BulkTestEntity { Name = $"P{i}", Value = i })];

    [Fact]
    public void BulkInsertIgnoreConstraints_NativeEligible_ReachesNativePathInsteadOfThrowing()
    {
        var connection = new NativeOnlyConnection(_inner);

        int inserted = connection.BulkInsertIgnoreConstraints(MakeEntities(5));

        Assert.Equal(5, inserted);
        Assert.True(_provider.WasCalled);
        Assert.False(_provider.LastOptions!.CheckConstraints);
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_NativeEligible_ReachesNativePathInsteadOfThrowing()
    {
        var connection = new NativeOnlyConnection(_inner);

        int inserted = await connection.BulkInsertIgnoreConstraintsAsync(MakeEntities(5));

        Assert.Equal(5, inserted);
        Assert.True(_provider.WasCalled);
        Assert.False(_provider.LastOptions!.CheckConstraints);
    }

    // AUD-R18 batch-6: IBulkCopyProvider.CopyToServer/CopyToServerAsync used to take only a bare
    // tableName, silently dropping EntityMetadata.SchemaName for entities mapped to a non-default
    // schema - the native bulk-copy path would target the wrong (schema-less) table on any dialect
    // where that matters. SchemaQualifiedBulkTestEntity is mapped to schema "custom"; asserting the
    // provider actually received it (not just that WasCalled is true) is what would have caught
    // this since RecordingBulkCopyProvider never touches a real connection.
    [Fact]
    public void BulkInsertIgnoreConstraints_SchemaQualifiedEntity_PassesSchemaNameToProvider()
    {
        var connection = new NativeOnlyConnection(_inner);
        var entities = Enumerable.Range(0, 5)
            .Select(i => new SchemaQualifiedBulkTestEntity { Name = $"P{i}", Value = i })
            .ToList();

        connection.BulkInsertIgnoreConstraints(entities);

        Assert.True(_provider.WasCalled);
        Assert.Equal("custom", _provider.LastSchemaName);
        Assert.Equal("bulk_test", _provider.LastTableName);
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_SchemaQualifiedEntity_PassesSchemaNameToProvider()
    {
        var connection = new NativeOnlyConnection(_inner);
        var entities = Enumerable.Range(0, 5)
            .Select(i => new SchemaQualifiedBulkTestEntity { Name = $"P{i}", Value = i })
            .ToList();

        await connection.BulkInsertIgnoreConstraintsAsync(entities);

        Assert.True(_provider.WasCalled);
        Assert.Equal("custom", _provider.LastSchemaName);
        Assert.Equal("bulk_test", _provider.LastTableName);
    }

    public void Dispose()
    {
        BulkCopyConfiguration.EnableNativeBulkCopy = _originalEnableNativeBulkCopy;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = _originalMinimumRows;
        _inner.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Thin DbConnection wrapper around a real SQLite connection, registered under its own type name
/// so <see cref="NativeOnlyDialect"/> can be resolved for it via
/// <see cref="SqlDialectFactory.RegisterDialect(string, ISqlDialect)"/> without altering dialect
/// resolution for real SqliteConnection instances used elsewhere in the test process.
/// </summary>
internal sealed class NativeOnlyConnection : DbConnection
{
    private readonly DbConnection _inner;

    public NativeOnlyConnection(DbConnection inner) => _inner = inner;

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

    protected override DbCommand CreateDbCommand() => _inner.CreateCommand();

    protected override void Dispose(bool disposing)
    {
        // Inner SQLite connection lifetime is owned by the test fixture, not this wrapper.
        base.Dispose(disposing);
    }
}

/// <summary>
/// Reproduces the exact SupportsForeignKeyToggle=false / SupportsNativeBulkCopy=true combination
/// that exposed the guard-ordering bug (SQL Server's real dialects), without requiring a live SQL
/// Server instance. Everything unrelated to that combination delegates to a real SQLiteDialect so
/// the fake stays behaviorally realistic.
/// </summary>
internal sealed class NativeOnlyDialect : ISqlDialect
{
    private readonly SQLiteDialect _inner = new();
    private readonly RecordingBulkCopyProvider _provider;

    public NativeOnlyDialect(RecordingBulkCopyProvider provider) => _provider = provider;

    public bool SupportsForeignKeyToggle => false;
    public bool RequiresAutocommitForForeignKeyToggle => _inner.RequiresAutocommitForForeignKeyToggle;
    public bool SupportsNativeBulkCopy => true;
    public IBulkCopyProvider? CreateBulkCopyProvider() => _provider;

    public string ParameterPrefix => _inner.ParameterPrefix;
    public string GetDefaultSchema() => _inner.GetDefaultSchema();
    public string EscapeTableName(string? schemaName, string tableName) => _inner.EscapeTableName(schemaName, tableName);
    public string EscapeColumnName(string columnName) => _inner.EscapeColumnName(columnName);
    public string EscapeStringLiteral(string value) => _inner.EscapeStringLiteral(value);
    public string GetLastInsertIdSql(params string[] columnNames) => _inner.GetLastInsertIdSql(columnNames);
    public string GetPagingSql(string baseSql, int offset, int fetchNext) => _inner.GetPagingSql(baseSql, offset, fetchNext);
    public bool IsKeyword(string identifier) => _inner.IsKeyword(identifier);

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) =>
        _inner.GenerateCaseSensitiveLike(columnName, parameterName, escapeChar);
    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) =>
        _inner.GenerateCaseInsensitiveLike(columnName, parameterName, escapeChar);
    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) =>
        _inner.GenerateCaseInsensitiveEquals(columnName, parameterName);

    public string FormatContainsPattern(string value) => _inner.FormatContainsPattern(value);
    public string FormatStartsWithPattern(string value) => _inner.FormatStartsWithPattern(value);
    public string FormatEndsWithPattern(string value) => _inner.FormatEndsWithPattern(value);

    public string? GetDisableForeignKeyChecksSql() => _inner.GetDisableForeignKeyChecksSql();
    public string? GetEnableForeignKeyChecksSql() => _inner.GetEnableForeignKeyChecksSql();

    public string GenerateCoalesce(params string[] expressions) => _inner.GenerateCoalesce(expressions);
    public string GenerateIsNull(string expression, string defaultExpression) => _inner.GenerateIsNull(expression, defaultExpression);
    public string GenerateNullIf(string expression, string compareExpression) => _inner.GenerateNullIf(expression, compareExpression);

    public string GenerateLength(string expression) => _inner.GenerateLength(expression);
    public string GenerateUpper(string expression) => _inner.GenerateUpper(expression);
    public string GenerateLower(string expression) => _inner.GenerateLower(expression);
    public string GenerateTrim(string expression) => _inner.GenerateTrim(expression);
    public string GenerateSubstring(string expression, string start, string length) => _inner.GenerateSubstring(expression, start, length);

    public string GenerateYear(string expression) => _inner.GenerateYear(expression);
    public string GenerateMonth(string expression) => _inner.GenerateMonth(expression);
    public string GenerateDay(string expression) => _inner.GenerateDay(expression);

    public bool SupportsUpsert => _inner.SupportsUpsert;
    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns,
        string[] keyParams) =>
        _inner.GenerateUpsertSql(tableName, insertColumns, insertParams, updateColumns, updateParams, keyColumns, keyParams);

    public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
    public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;

    public string GenerateRowNumber() => _inner.GenerateRowNumber();
    public string GenerateRank() => _inner.GenerateRank();
    public string GenerateDenseRank() => _inner.GenerateDenseRank();
    public string GenerateNTile(int buckets) => _inner.GenerateNTile(buckets);
    public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy) =>
        _inner.GenerateOverClause(partitionBy, orderBy);
    public string GenerateWindowAggregate(string function, string? expression) => _inner.GenerateWindowAggregate(function, expression);
}

/// <summary>
/// Records the options a native bulk-copy call was made with instead of touching the connection,
/// so tests can assert both reachability (no NotSupportedException) and that ignoreConstraints
/// flowed through to BulkCopyOptions.CheckConstraints.
/// </summary>
internal sealed class RecordingBulkCopyProvider : IBulkCopyProvider
{
    public bool IsSupported => true;
    public bool WasCalled { get; private set; }
    public BulkCopyOptions? LastOptions { get; private set; }
    public string? LastSchemaName { get; private set; }
    public string? LastTableName { get; private set; }

    public int CopyToServer(IDbConnection connection, string? schemaName, string tableName, IDataReader data, BulkCopyOptions options)
    {
        WasCalled = true;
        LastOptions = options;
        LastSchemaName = schemaName;
        LastTableName = tableName;
        return -1;
    }

    public ValueTask<int> CopyToServerAsync(
        DbConnection connection,
        string? schemaName,
        string tableName,
        IDataReader data,
        BulkCopyOptions options,
        CancellationToken cancellationToken)
    {
        WasCalled = true;
        LastOptions = options;
        LastSchemaName = schemaName;
        LastTableName = tableName;
        return new ValueTask<int>(-1);
    }
}
