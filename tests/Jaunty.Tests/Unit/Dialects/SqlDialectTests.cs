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

    [Fact]
    public void Sqlite_GenerateCaseSensitiveLike_UsesGlob()
    {
        _sqlite.GenerateCaseSensitiveLike("col", "@param", "\\").Should().Be("col GLOB @param");
    }

    [Fact]
    public void Sqlite_GenerateCaseInsensitiveLike_UsesLike()
    {
        _sqlite.GenerateCaseInsensitiveLike("col", "@param", "\\").Should().Be("col LIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void Sqlite_GenerateCaseInsensitiveEquals_UsesLower()
    {
        _sqlite.GenerateCaseInsensitiveEquals("col", "@param").Should().Be("LOWER(col) = LOWER(@param)");
    }

    [Fact]
    public void Sqlite_GenerateCoalesce_ReturnsCoalesce()
    {
        _sqlite.GenerateCoalesce("col", "default").Should().Be("COALESCE(col, default)");
    }

    [Fact]
    public void Sqlite_GenerateIsNull_ReturnsIsnull()
    {
        _sqlite.GenerateIsNull("col", "default").Should().Be("IFNULL(col, default)");
    }

    [Fact]
    public void Sqlite_GenerateNullIf_ReturnsNullif()
    {
        _sqlite.GenerateNullIf("col", "compare").Should().Be("NULLIF(col, compare)");
    }

    [Fact]
    public void Sqlite_GenerateLength_ReturnsLength()
    {
        _sqlite.GenerateLength("col").Should().Be("LENGTH(col)");
    }

    [Fact]
    public void Sqlite_GenerateSubstring_ReturnsSubstr()
    {
        _sqlite.GenerateSubstring("col", "start", "length").Should().Be("SUBSTR(col, start, length)");
    }

    [Fact]
    public void Sqlite_GenerateYear_ReturnsStrftime()
    {
        _sqlite.GenerateYear("col").Should().Be("CAST(strftime('%Y', col) AS INTEGER)");
    }

    [Fact]
    public void Sqlite_GenerateMonth_ReturnsStrftime()
    {
        _sqlite.GenerateMonth("col").Should().Be("CAST(strftime('%m', col) AS INTEGER)");
    }

    [Fact]
    public void Sqlite_GenerateDay_ReturnsStrftime()
    {
        _sqlite.GenerateDay("col").Should().Be("CAST(strftime('%d', col) AS INTEGER)");
    }

    #endregion

    #region SQL Server Dialect - String Functions

    [Fact]
    public void SqlServer_GenerateCaseSensitiveLike_UsesCollate()
    {
        _sqlServer.GenerateCaseSensitiveLike("col", "@param", "\\").Should().Be("col COLLATE Latin1_General_CS_AS LIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void SqlServer_GenerateCaseInsensitiveLike_UsesCollate()
    {
        _sqlServer.GenerateCaseInsensitiveLike("col", "@param", "\\").Should().Be("col COLLATE Latin1_General_CI_AS LIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void SqlServer_GenerateCaseInsensitiveEquals_UsesCollate()
    {
        _sqlServer.GenerateCaseInsensitiveEquals("col", "@param").Should().Be("col COLLATE Latin1_General_CI_AS = @param");
    }

    [Fact]
    public void SqlServer_GenerateCoalesce_ReturnsCoalesce()
    {
        _sqlServer.GenerateCoalesce("col", "default").Should().Be("COALESCE(col, default)");
    }

    [Fact]
    public void SqlServer_GenerateIsNull_ReturnsIsnull()
    {
        _sqlServer.GenerateIsNull("col", "default").Should().Be("ISNULL(col, default)");
    }

    [Fact]
    public void SqlServer_GenerateNullIf_ReturnsNullif()
    {
        _sqlServer.GenerateNullIf("col", "compare").Should().Be("NULLIF(col, compare)");
    }

    [Fact]
    public void SqlServer_GenerateLength_ReturnsLen()
    {
        _sqlServer.GenerateLength("col").Should().Be("LEN(col)");
    }

    [Fact]
    public void SqlServer_GenerateSubstring_ReturnsSubstring()
    {
        _sqlServer.GenerateSubstring("col", "start", "length").Should().Be("SUBSTRING(col, start, length)");
    }

    [Fact]
    public void SqlServer_GenerateYear_ReturnsYear()
    {
        _sqlServer.GenerateYear("col").Should().Be("YEAR(col)");
    }

    [Fact]
    public void SqlServer_GenerateMonth_ReturnsMonth()
    {
        _sqlServer.GenerateMonth("col").Should().Be("MONTH(col)");
    }

    [Fact]
    public void SqlServer_GenerateDay_ReturnsDay()
    {
        _sqlServer.GenerateDay("col").Should().Be("DAY(col)");
    }

    #endregion

    #region PostgreSQL Dialect - String Functions

    [Fact]
    public void Postgres_GenerateCaseSensitiveLike_UsesCollate()
    {
        _postgres.GenerateCaseSensitiveLike("col", "@param", "\\").Should().Be("col COLLATE \"C\" LIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void Postgres_GenerateCaseInsensitiveLike_UsesIlike()
    {
        _postgres.GenerateCaseInsensitiveLike("col", "@param", "\\").Should().Be("col ILIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void Postgres_GenerateCaseInsensitiveEquals_UsesLower()
    {
        _postgres.GenerateCaseInsensitiveEquals("col", "@param").Should().Be("LOWER(col) = LOWER(@param)");
    }

    [Fact]
    public void Postgres_GenerateCoalesce_ReturnsCoalesce()
    {
        _postgres.GenerateCoalesce("col", "default").Should().Be("COALESCE(col, default)");
    }

    [Fact]
    public void Postgres_GenerateIsNull_ReturnsCoalesce()
    {
        _postgres.GenerateIsNull("col", "default").Should().Be("COALESCE(col, default)");
    }

    [Fact]
    public void Postgres_GenerateNullIf_ReturnsNullif()
    {
        _postgres.GenerateNullIf("col", "compare").Should().Be("NULLIF(col, compare)");
    }

    [Fact]
    public void Postgres_GenerateLength_ReturnsLength()
    {
        _postgres.GenerateLength("col").Should().Be("LENGTH(col)");
    }

    [Fact]
    public void Postgres_GenerateSubstring_ReturnsSubstring()
    {
        _postgres.GenerateSubstring("col", "start", "length").Should().Be("SUBSTRING(col FROM start FOR length)");
    }

    [Fact]
    public void Postgres_GenerateYear_ReturnsExtract()
    {
        _postgres.GenerateYear("col").Should().Be("EXTRACT(YEAR FROM col)");
    }

    [Fact]
    public void Postgres_GenerateMonth_ReturnsExtract()
    {
        _postgres.GenerateMonth("col").Should().Be("EXTRACT(MONTH FROM col)");
    }

    [Fact]
    public void Postgres_GenerateDay_ReturnsExtract()
    {
        _postgres.GenerateDay("col").Should().Be("EXTRACT(DAY FROM col)");
    }

    #endregion

    #region MySQL Dialect - String Functions

    [Fact]
    public void MySql_GenerateCaseSensitiveLike_UsesCollate()
    {
        _mySql.GenerateCaseSensitiveLike("col", "@param", "\\").Should().Be("col COLLATE utf8mb4_bin LIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void MySql_GenerateCaseInsensitiveLike_UsesLike()
    {
        _mySql.GenerateCaseInsensitiveLike("col", "@param", "\\").Should().Be("col LIKE @param ESCAPE '\\'");
    }

    [Fact]
    public void MySql_GenerateCaseInsensitiveEquals_UsesCollate()
    {
        _mySql.GenerateCaseInsensitiveEquals("col", "@param").Should().Be("col COLLATE utf8mb4_general_ci = @param");
    }

    [Fact]
    public void MySql_GenerateCoalesce_ReturnsCoalesce()
    {
        _mySql.GenerateCoalesce("col", "default").Should().Be("COALESCE(col, default)");
    }

    [Fact]
    public void MySql_GenerateIsNull_ReturnsIfnull()
    {
        _mySql.GenerateIsNull("col", "default").Should().Be("IFNULL(col, default)");
    }

    [Fact]
    public void MySql_GenerateNullIf_ReturnsNullif()
    {
        _mySql.GenerateNullIf("col", "compare").Should().Be("NULLIF(col, compare)");
    }

    [Fact]
    public void MySql_GenerateLength_ReturnsLength()
    {
        _mySql.GenerateLength("col").Should().Be("LENGTH(col)");
    }

    [Fact]
    public void MySql_GenerateSubstring_ReturnsSubstring()
    {
        _mySql.GenerateSubstring("col", "start", "length").Should().Be("SUBSTRING(col, start, length)");
    }

    [Fact]
    public void MySql_GenerateYear_ReturnsYear()
    {
        _mySql.GenerateYear("col").Should().Be("YEAR(col)");
    }

    [Fact]
    public void MySql_GenerateMonth_ReturnsMonth()
    {
        _mySql.GenerateMonth("col").Should().Be("MONTH(col)");
    }

    [Fact]
    public void MySql_GenerateDay_ReturnsDay()
    {
        _mySql.GenerateDay("col").Should().Be("DAY(col)");
    }

    #endregion
}
