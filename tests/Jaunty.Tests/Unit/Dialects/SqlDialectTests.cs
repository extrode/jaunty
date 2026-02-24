using FluentAssertions;

using Jaunty.Internals.Dialects;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// Unit tests for SQL dialect implementations.
/// Tests escape methods, keyword detection, and dialect-specific SQL generation.
/// </summary>
public class SqlDialectTests
{
    #region SqlServerDialect Tests

    private readonly SqlServerDialect _sqlServer = new();

    [Theory]
    [InlineData("select", "[select]")]
    [InlineData("ORDER", "[ORDER]")]
    [InlineData("User", "[User]")]
    public void SqlServer_EscapeTableName_Keyword_Escaped(string tableName, string expected)
    {
        _sqlServer.EscapeTableName(null, tableName).Should().Be(expected);
    }

    [Theory]
    [InlineData("dbo", "select", "dbo.[select]")]
    [InlineData("schema", "ORDER", "[schema].[ORDER]")]
    public void SqlServer_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        _sqlServer.EscapeTableName(schema, table).Should().Be(expected);
    }

    [Theory]
    [InlineData("select", "[select]")]
    [InlineData("ORDER", "[ORDER]")]
    public void SqlServer_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        _sqlServer.EscapeColumnName(columnName).Should().Be(expected);
    }

    [Fact]
    public void SqlServer_IsKeyword_ReturnsTrueForKeywords()
    {
        _sqlServer.IsKeyword("SELECT").Should().BeTrue();
        _sqlServer.IsKeyword("ORDER").Should().BeTrue();
        _sqlServer.IsKeyword("User").Should().BeTrue();
    }

    [Fact]
    public void SqlServer_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _sqlServer.GetPagingSql("SELECT * FROM table", 10, 20);
        result.Should().Be("SELECT * FROM table OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY");
    }

    [Fact]
    public void SqlServer_GetLastInsertIdSql_ReturnsScopeIdentity()
    {
        _sqlServer.GetLastInsertIdSql("Id").Should().Be("SELECT CAST(SCOPE_IDENTITY() AS BIGINT);");
    }

    [Fact]
    public void SqlServer_SupportsForeignKeyToggle_ReturnsFalse()
    {
        _sqlServer.SupportsForeignKeyToggle.Should().BeFalse();
    }

    [Fact]
    public void SqlServer_GetDefaultSchema_ReturnsDbo()
    {
        _sqlServer.GetDefaultSchema().Should().Be("dbo");
    }

    #endregion

    #region PostgreSqlDialect Tests

    private readonly PostgreSqlDialect _postgres = new();

    [Theory]
    [InlineData("select", "\"select\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    [InlineData("User", "\"User\"")]
    public void Postgres_EscapeTableName_Keyword_Escaped(string tableName, string expected)
    {
        _postgres.EscapeTableName(null, tableName).Should().Be(expected);
    }

    [Theory]
    [InlineData("public", "select", "public.\"select\"")]
    [InlineData("schema", "ORDER", "schema.\"ORDER\"")]
    public void Postgres_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        _postgres.EscapeTableName(schema, table).Should().Be(expected);
    }

    [Theory]
    [InlineData("select", "\"select\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    public void Postgres_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        _postgres.EscapeColumnName(columnName).Should().Be(expected);
    }

    [Fact]
    public void Postgres_IsKeyword_ReturnsTrueForKeywords()
    {
        _postgres.IsKeyword("SELECT").Should().BeTrue();
        _postgres.IsKeyword("ORDER").Should().BeTrue();
        _postgres.IsKeyword("ANALYZE").Should().BeTrue();
    }

    [Fact]
    public void Postgres_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _postgres.GetPagingSql("SELECT * FROM table", 10, 20);
        result.Should().Be("SELECT * FROM table LIMIT 20 OFFSET 10");
    }

    [Fact]
    public void Postgres_GetLastInsertIdSql_WithColumn_ReturnsReturning()
    {
        _postgres.GetLastInsertIdSql("Id").Should().Be("RETURNING Id;");
    }

    [Fact]
    public void Postgres_SupportsForeignKeyToggle_ReturnsTrue()
    {
        _postgres.SupportsForeignKeyToggle.Should().BeTrue();
    }

    [Fact]
    public void Postgres_GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _postgres.GetDisableForeignKeyChecksSql().Should().Be("SET session_replication_role = 'replica'");
    }

    [Fact]
    public void Postgres_GetEnableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _postgres.GetEnableForeignKeyChecksSql().Should().Be("SET session_replication_role = 'origin'");
    }

    [Fact]
    public void Postgres_GetDefaultSchema_ReturnsPublic()
    {
        _postgres.GetDefaultSchema().Should().Be("public");
    }

    #endregion

    #region MySqlDialect Tests

    private readonly MySqlDialect _mySql = new();

    [Theory]
    [InlineData("select", "`select`")]
    [InlineData("ORDER", "`ORDER`")]
    [InlineData("User", "`User`")]
    public void MySql_EscapeTableName_Keyword_Escaped(string tableName, string expected)
    {
        _mySql.EscapeTableName(null, tableName).Should().Be(expected);
    }

    [Theory]
    [InlineData("db", "select", "db.`select`")]
    [InlineData("database", "ORDER", "`database`.`ORDER`")]
    public void MySql_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        _mySql.EscapeTableName(schema, table).Should().Be(expected);
    }

    [Theory]
    [InlineData("select", "`select`")]
    [InlineData("ORDER", "`ORDER`")]
    public void MySql_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        _mySql.EscapeColumnName(columnName).Should().Be(expected);
    }

    [Fact]
    public void MySql_IsKeyword_ReturnsTrueForKeywords()
    {
        _mySql.IsKeyword("SELECT").Should().BeTrue();
        _mySql.IsKeyword("ORDER").Should().BeTrue();
        _mySql.IsKeyword("ACCESSIBLE").Should().BeTrue();
    }

    [Fact]
    public void MySql_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _mySql.GetPagingSql("SELECT * FROM table", 10, 20);
        result.Should().Be("SELECT * FROM table LIMIT 10, 20");
    }

    [Fact]
    public void MySql_GetLastInsertIdSql_ReturnsLastInsertId()
    {
        _mySql.GetLastInsertIdSql("Id").Should().Be("SELECT LAST_INSERT_ID();");
    }

    [Fact]
    public void MySql_SupportsForeignKeyToggle_ReturnsTrue()
    {
        _mySql.SupportsForeignKeyToggle.Should().BeTrue();
    }

    [Fact]
    public void MySql_GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _mySql.GetDisableForeignKeyChecksSql().Should().Be("SET FOREIGN_KEY_CHECKS = 0");
    }

    [Fact]
    public void MySql_GetEnableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _mySql.GetEnableForeignKeyChecksSql().Should().Be("SET FOREIGN_KEY_CHECKS = 1");
    }

    [Fact]
    public void MySql_GetDefaultSchema_ReturnsEmpty()
    {
        _mySql.GetDefaultSchema().Should().BeEmpty();
    }

    #endregion

    #region SQLiteDialect Tests

    private readonly SQLiteDialect _sqlite = new();

    [Theory]
    [InlineData("select", "\"select\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    [InlineData("User", "\"User\"")]
    public void Sqlite_EscapeTableName_Keyword_Escaped(string tableName, string expected)
    {
        _sqlite.EscapeTableName(null, tableName).Should().Be(expected);
    }

    [Theory]
    [InlineData("", "select", "\"select\"")]
    public void Sqlite_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        _sqlite.EscapeTableName(schema, table).Should().Be(expected);
    }

    [Theory]
    [InlineData("select", "\"select\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    public void Sqlite_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        _sqlite.EscapeColumnName(columnName).Should().Be(expected);
    }

    [Fact]
    public void Sqlite_IsKeyword_ReturnsTrueForKeywords()
    {
        _sqlite.IsKeyword("SELECT").Should().BeTrue();
        _sqlite.IsKeyword("ORDER").Should().BeTrue();
        _sqlite.IsKeyword("ABORT").Should().BeTrue();
    }

    [Fact]
    public void Sqlite_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _sqlite.GetPagingSql("SELECT * FROM table", 10, 20);
        result.Should().Be("SELECT * FROM table LIMIT 20 OFFSET 10");
    }

    [Fact]
    public void Sqlite_GetLastInsertIdSql_ReturnsLastInsertRowId()
    {
        _sqlite.GetLastInsertIdSql("Id").Should().Be("SELECT last_insert_rowid();");
    }

    [Fact]
    public void Sqlite_SupportsForeignKeyToggle_ReturnsTrue()
    {
        _sqlite.SupportsForeignKeyToggle.Should().BeTrue();
    }

    [Fact]
    public void Sqlite_GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _sqlite.GetDisableForeignKeyChecksSql().Should().Be("PRAGMA foreign_keys = OFF");
    }

    [Fact]
    public void Sqlite_GetEnableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _sqlite.GetEnableForeignKeyChecksSql().Should().Be("PRAGMA foreign_keys = ON");
    }

    [Fact]
    public void Sqlite_GetDefaultSchema_ReturnsEmpty()
    {
        _sqlite.GetDefaultSchema().Should().BeEmpty();
    }

    [Theory]
    [InlineData("test", "*test*")]
    [InlineData("test*", "*test[*]*")]
    [InlineData("test?", "*test[?]*")]
    [InlineData("test[", "*test[[]*")]
    public void Sqlite_FormatContainsPattern_EscapesGlobSpecialChars(string input, string expected)
    {
        _sqlite.FormatContainsPattern(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("test", "test*")]
    [InlineData("test*", "test[*]*")]
    [InlineData("test?", "test[?]*")]
    public void Sqlite_FormatStartsWithPattern_EscapesGlobSpecialChars(string input, string expected)
    {
        _sqlite.FormatStartsWithPattern(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("test", "*test")]
    [InlineData("test*", "*test[*]")]
    [InlineData("test?", "*test[?]")]
    public void Sqlite_FormatEndsWithPattern_EscapesGlobSpecialChars(string input, string expected)
    {
        _sqlite.FormatEndsWithPattern(input).Should().Be(expected);
    }

    #endregion
}
