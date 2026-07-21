using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Regression tests for identifier escaping in the SQL Server / PostgreSQL / SQLite import
/// dialects: table/column names containing an embedded double quote must not be able to break
/// out of the quoted identifier context (AUD-R9-004).
/// </summary>
public class ImportDialectIdentifierEscapingTests
{
    private const string MaliciousTableName = "orders\" DROP TABLE users; --";
    private const string MaliciousColumnName = "id\" = 1; --";

    [Fact]
    public void SqlServerImportDialect_GenerateInsertSql_EscapesEmbeddedQuoteInTableName()
    {
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            MaliciousTableName, ["id"], ["@p0"], ConflictStrategy.Error, keyColumnName: null);

        Assert.Contains("\"orders\"\" DROP TABLE users; --\"", sql);
        Assert.DoesNotContain("DROP TABLE users; --\"\n", sql);
    }

    [Fact]
    public void SqlServerImportDialect_GenerateCreateTableSql_EscapesEmbeddedQuoteInTableName()
    {
        string sql = SqlServerImportDialect.Instance.GenerateCreateTableSql(
            MaliciousTableName, [("id", typeof(int), true, false)]);

        Assert.Contains("\"orders\"\" DROP TABLE users; --\"", sql);
        // The single-quoted sys.tables existence check must also double-escape via single quotes.
        Assert.Contains("orders\" DROP TABLE users; --", sql);
    }

    [Fact]
    public void SqlServerImportDialect_GenerateInsertSql_Upsert_EscapesEmbeddedQuoteInMerge()
    {
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            "orders", [MaliciousColumnName], ["@p0"], ConflictStrategy.Upsert, keyColumnName: MaliciousColumnName);

        Assert.Contains("WITH (HOLDLOCK)", sql);
        Assert.Contains("\"id\"\" = 1; --\"", sql);
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
}
