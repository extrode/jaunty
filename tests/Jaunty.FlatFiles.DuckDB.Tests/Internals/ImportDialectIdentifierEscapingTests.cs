using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Regression tests for identifier escaping in the SQL Server / PostgreSQL / SQLite import
/// dialects: table/column names containing an embedded double quote or bracket must not be able
/// to break out of the quoted identifier context (AUD-R9-004). SqlServerImportDialect quotes with
/// [brackets] (AUD-R22: brackets work regardless of the session's QUOTED_IDENTIFIER setting,
/// unlike double quotes), so its embedded-character escaping tests use "]" instead of "\"".
/// </summary>
public class ImportDialectIdentifierEscapingTests
{
    private const string MaliciousTableName = "orders\" DROP TABLE users; --";
    private const string MaliciousColumnName = "id\" = 1; --";
    private const string MaliciousTableNameBracket = "orders] DROP TABLE users; --";
    private const string MaliciousColumnNameBracket = "id] = 1; --";

    [Fact]
    public void SqlServerImportDialect_GenerateInsertSql_EscapesEmbeddedQuoteInTableName()
    {
        // A double quote has no special meaning inside a bracket-quoted identifier, so it
        // passes through unescaped and cannot break out of the [brackets] either way.
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            MaliciousTableName, ["id"], ["@p0"], ConflictStrategy.Error, keyColumnName: null);

        Assert.Contains("[orders\" DROP TABLE users; --] (", sql);
    }

    [Fact]
    public void SqlServerImportDialect_GenerateInsertSql_EscapesEmbeddedBracketInTableName()
    {
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            MaliciousTableNameBracket, ["id"], ["@p0"], ConflictStrategy.Error, keyColumnName: null);

        Assert.Contains("[orders]] DROP TABLE users; --] (", sql);
    }

    [Fact]
    public void SqlServerImportDialect_GenerateCreateTableSql_EscapesEmbeddedBracketInTableName()
    {
        string sql = SqlServerImportDialect.Instance.GenerateCreateTableSql(
            MaliciousTableNameBracket, [("id", typeof(int), true, false)]);

        Assert.Contains("[orders]] DROP TABLE users; --] (", sql);
        // The single-quoted sys.tables existence check is unaffected by bracket-doubling.
        Assert.Contains("orders] DROP TABLE users; --", sql);
    }

    [Fact]
    public void SqlServerImportDialect_GenerateInsertSql_Upsert_EscapesEmbeddedBracketInMerge()
    {
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            "orders", [MaliciousColumnNameBracket], ["@p0"], ConflictStrategy.Upsert, keyColumnName: MaliciousColumnNameBracket);

        Assert.Contains("WITH (HOLDLOCK)", sql);
        Assert.Contains("[id]] = 1; --]", sql);
    }

    [Fact]
    public void PostgreSqlImportDialect_GenerateInsertSql_EscapesEmbeddedQuoteInTableName()
    {
        string sql = PostgreSqlImportDialect.Instance.GenerateInsertSql(
            MaliciousTableName, ["id"], ["@p0"], ConflictStrategy.Error, keyColumnName: null);

        Assert.Contains("\"orders\"\" DROP TABLE users; --\"", sql);
    }

    [Fact]
    public void PostgreSqlImportDialect_GenerateInsertSql_Upsert_EscapesEmbeddedQuoteInColumnName()
    {
        string sql = PostgreSqlImportDialect.Instance.GenerateInsertSql(
            "orders", [MaliciousColumnName], ["@p0"], ConflictStrategy.Upsert, keyColumnName: MaliciousColumnName);

        Assert.Contains("\"id\"\" = 1; --\"", sql);
    }

    [Fact]
    public void PostgreSqlImportDialect_GenerateCreateTableSql_EscapesEmbeddedQuoteInTableName()
    {
        string sql = PostgreSqlImportDialect.Instance.GenerateCreateTableSql(
            MaliciousTableName, [("id", typeof(int), true, false)]);

        Assert.Contains("\"orders\"\" DROP TABLE users; --\"", sql);
    }

    [Fact]
    public void SqliteImportDialect_GenerateInsertSql_EscapesEmbeddedQuoteInTableName()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            MaliciousTableName, ["id"], ["@p0"], ConflictStrategy.Error, keyColumnName: null);

        Assert.Contains("\"orders\"\" DROP TABLE users; --\"", sql);
    }

    [Fact]
    public void SqliteImportDialect_GenerateCreateTableSql_EscapesEmbeddedQuoteInTableName()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql(
            MaliciousTableName, [("id", typeof(int), true, false)]);

        Assert.Contains("\"orders\"\" DROP TABLE users; --\"", sql);
    }

    [Theory]
    [InlineData(ConflictStrategy.Skip)]
    [InlineData(ConflictStrategy.Upsert)]
    public void PostgreSqlImportDialect_GenerateInsertSql_KeylessEntityWithNonErrorStrategy_Throws(
        ConflictStrategy conflictStrategy)
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            PostgreSqlImportDialect.Instance.GenerateInsertSql(
                "orders", ["id"], ["@p0"], conflictStrategy, keyColumnName: null));

        Assert.Contains("orders", ex.Message);
        Assert.Contains("[Key]", ex.Message);
    }

    [Theory]
    [InlineData(ConflictStrategy.Skip)]
    [InlineData(ConflictStrategy.Upsert)]
    public void SqlServerImportDialect_GenerateInsertSql_KeylessEntityWithNonErrorStrategy_Throws(
        ConflictStrategy conflictStrategy)
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            SqlServerImportDialect.Instance.GenerateInsertSql(
                "orders", ["id"], ["@p0"], conflictStrategy, keyColumnName: null));

        Assert.Contains("orders", ex.Message);
        Assert.Contains("[Key]", ex.Message);
    }

    [Fact]
    public void SqliteImportDialect_GenerateInsertSql_KeylessEntityWithSkipStrategy_DoesNotThrow()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "orders", ["id"], ["@p0"], ConflictStrategy.Skip, keyColumnName: null);

        Assert.Contains("INSERT OR IGNORE INTO", sql);
    }

    [Fact]
    public void SqliteImportDialect_GenerateInsertSql_KeylessEntityWithUpsertStrategy_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            SqliteImportDialect.Instance.GenerateInsertSql(
                "orders", ["id"], ["@p0"], ConflictStrategy.Upsert, keyColumnName: null));

        Assert.Contains("orders", ex.Message);
        Assert.Contains("[Key]", ex.Message);
    }

    [Fact]
    public void SqliteImportDialect_GenerateInsertSql_Upsert_UsesOnConflictDoUpdateNotReplace()
    {
        // AUD-R20: "INSERT OR REPLACE" is a DELETE+INSERT under the hood - fires DELETE+INSERT
        // triggers instead of an UPDATE trigger, churns the rowid/AUTOINCREMENT counter, and can
        // cascade-delete FK-dependent child rows under PRAGMA foreign_keys=ON with ON DELETE
        // CASCADE. "ON CONFLICT DO UPDATE" is a true UPDATE, matching PostgreSqlImportDialect's
        // and SqlServerImportDialect's Upsert semantics for the identical ConflictStrategy value.
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "orders", ["id", "name"], ["@p0", "@p1"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.DoesNotContain("OR REPLACE", sql);
        Assert.Contains("ON CONFLICT (\"id\") DO UPDATE SET \"name\" = excluded.\"name\"", sql);
    }

    [Fact]
    public void SqliteImportDialect_GenerateInsertSql_Upsert_EscapesEmbeddedQuoteInColumnName()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "orders", [MaliciousColumnName], ["@p0"], ConflictStrategy.Upsert, keyColumnName: MaliciousColumnName);

        Assert.Contains("\"id\"\" = 1; --\"", sql);
    }
}
