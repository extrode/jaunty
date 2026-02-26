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
        Assert.Equal(expected, _sqlServer.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("dbo", "select", "dbo.[select]")]
    [InlineData("schema", "ORDER", "[schema].[ORDER]")]
    public void SqlServer_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _sqlServer.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("select", "[select]")]
    [InlineData("ORDER", "[ORDER]")]
    public void SqlServer_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        Assert.Equal(expected, _sqlServer.EscapeColumnName(columnName));
    }

    [Fact]
    public void SqlServer_IsKeyword_ReturnsTrueForKeywords()
    {
        Assert.True(_sqlServer.IsKeyword("SELECT"));
        Assert.True(_sqlServer.IsKeyword("ORDER"));
        Assert.True(_sqlServer.IsKeyword("User"));
    }

    [Fact]
    public void SqlServer_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _sqlServer.GetPagingSql("SELECT * FROM table", 10, 20);
        Assert.Equal("SELECT * FROM table OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", result);
    }

    [Fact]
    public void SqlServer_GetLastInsertIdSql_ReturnsScopeIdentity()
    {
        Assert.Equal("SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", _sqlServer.GetLastInsertIdSql("Id"));
    }

    [Fact]
    public void SqlServer_SupportsForeignKeyToggle_ReturnsFalse()
    {
        Assert.False(_sqlServer.SupportsForeignKeyToggle);
    }

    [Fact]
    public void SqlServer_GetDefaultSchema_ReturnsDbo()
    {
        Assert.Equal("dbo", _sqlServer.GetDefaultSchema());
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
        Assert.Equal(expected, _postgres.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("public", "select", "public.\"select\"")]
    [InlineData("schema", "ORDER", "schema.\"ORDER\"")]
    public void Postgres_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _postgres.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("select", "\"select\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    public void Postgres_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        Assert.Equal(expected, _postgres.EscapeColumnName(columnName));
    }

    [Fact]
    public void Postgres_IsKeyword_ReturnsTrueForKeywords()
    {
        Assert.True(_postgres.IsKeyword("SELECT"));
        Assert.True(_postgres.IsKeyword("ORDER"));
        Assert.True(_postgres.IsKeyword("ANALYZE"));
    }

    [Fact]
    public void Postgres_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _postgres.GetPagingSql("SELECT * FROM table", 10, 20);
        Assert.Equal("SELECT * FROM table LIMIT 20 OFFSET 10", result);
    }

    [Fact]
    public void Postgres_GetLastInsertIdSql_WithColumn_ReturnsReturning()
    {
        Assert.Equal("RETURNING Id;", _postgres.GetLastInsertIdSql("Id"));
    }

    [Fact]
    public void Postgres_SupportsForeignKeyToggle_ReturnsTrue()
    {
        Assert.True(_postgres.SupportsForeignKeyToggle);
    }

    [Fact]
    public void Postgres_GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        Assert.Equal("SET session_replication_role = 'replica'", _postgres.GetDisableForeignKeyChecksSql());
    }

    [Fact]
    public void Postgres_GetEnableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        Assert.Equal("SET session_replication_role = 'origin'", _postgres.GetEnableForeignKeyChecksSql());
    }

    [Fact]
    public void Postgres_GetDefaultSchema_ReturnsPublic()
    {
        Assert.Equal("public", _postgres.GetDefaultSchema());
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
        Assert.Equal(expected, _mySql.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("db", "select", "db.`select`")]
    [InlineData("database", "ORDER", "`database`.`ORDER`")]
    public void MySql_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _mySql.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("select", "`select`")]
    [InlineData("ORDER", "`ORDER`")]
    public void MySql_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        Assert.Equal(expected, _mySql.EscapeColumnName(columnName));
    }

    [Fact]
    public void MySql_IsKeyword_ReturnsTrueForKeywords()
    {
        Assert.True(_mySql.IsKeyword("SELECT"));
        Assert.True(_mySql.IsKeyword("ORDER"));
        Assert.True(_mySql.IsKeyword("ACCESSIBLE"));
    }

    [Fact]
    public void MySql_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _mySql.GetPagingSql("SELECT * FROM table", 10, 20);
        Assert.Equal("SELECT * FROM table LIMIT 10, 20", result);
    }

    [Fact]
    public void MySql_GetLastInsertIdSql_ReturnsLastInsertId()
    {
        Assert.Equal("SELECT LAST_INSERT_ID();", _mySql.GetLastInsertIdSql("Id"));
    }

    [Fact]
    public void MySql_SupportsForeignKeyToggle_ReturnsTrue()
    {
        Assert.True(_mySql.SupportsForeignKeyToggle);
    }

    [Fact]
    public void MySql_GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        Assert.Equal("SET FOREIGN_KEY_CHECKS = 0", _mySql.GetDisableForeignKeyChecksSql());
    }

    [Fact]
    public void MySql_GetEnableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        Assert.Equal("SET FOREIGN_KEY_CHECKS = 1", _mySql.GetEnableForeignKeyChecksSql());
    }

    [Fact]
    public void MySql_GetDefaultSchema_ReturnsEmpty()
    {
        Assert.Empty(_mySql.GetDefaultSchema());
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
        Assert.Equal(expected, _sqlite.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("", "select", "\"select\"")]
    public void Sqlite_EscapeTableName_WithSchema_Keyword_Escaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _sqlite.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("select", "\"select\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    public void Sqlite_EscapeColumnName_Keyword_Escaped(string columnName, string expected)
    {
        Assert.Equal(expected, _sqlite.EscapeColumnName(columnName));
    }

    [Fact]
    public void Sqlite_IsKeyword_ReturnsTrueForKeywords()
    {
        Assert.True(_sqlite.IsKeyword("SELECT"));
        Assert.True(_sqlite.IsKeyword("ORDER"));
        Assert.True(_sqlite.IsKeyword("ABORT"));
    }

    [Fact]
    public void Sqlite_GetPagingSql_GeneratesCorrectSql()
    {
        var result = _sqlite.GetPagingSql("SELECT * FROM table", 10, 20);
        Assert.Equal("SELECT * FROM table LIMIT 20 OFFSET 10", result);
    }

    [Fact]
    public void Sqlite_GetLastInsertIdSql_ReturnsLastInsertRowId()
    {
        Assert.Equal("SELECT last_insert_rowid();", _sqlite.GetLastInsertIdSql("Id"));
    }

    [Fact]
    public void Sqlite_SupportsForeignKeyToggle_ReturnsTrue()
    {
        Assert.True(_sqlite.SupportsForeignKeyToggle);
    }

    [Fact]
    public void Sqlite_GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        Assert.Equal("PRAGMA foreign_keys = OFF", _sqlite.GetDisableForeignKeyChecksSql());
    }

    [Fact]
    public void Sqlite_GetEnableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        Assert.Equal("PRAGMA foreign_keys = ON", _sqlite.GetEnableForeignKeyChecksSql());
    }

    [Fact]
    public void Sqlite_GetDefaultSchema_ReturnsEmpty()
    {
        Assert.Empty(_sqlite.GetDefaultSchema());
    }

    [Theory]
    [InlineData("test", "*test*")]
    [InlineData("test*", "*test[*]*")]
    [InlineData("test?", "*test[?]*")]
    [InlineData("test[", "*test[[]*")]
    public void Sqlite_FormatContainsPattern_EscapesGlobSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, _sqlite.FormatContainsPattern(input));
    }

    [Theory]
    [InlineData("test", "test*")]
    [InlineData("test*", "test[*]*")]
    [InlineData("test?", "test[?]*")]
    public void Sqlite_FormatStartsWithPattern_EscapesGlobSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, _sqlite.FormatStartsWithPattern(input));
    }

    [Theory]
    [InlineData("test", "*test")]
    [InlineData("test*", "*test[*]")]
    [InlineData("test?", "*test[?]")]
    public void Sqlite_FormatEndsWithPattern_EscapesGlobSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, _sqlite.FormatEndsWithPattern(input));
    }

    [Fact]
    public void Sqlite_GenerateCaseSensitiveLike_UsesGlob()
    {
        Assert.Equal("col GLOB @param", _sqlite.GenerateCaseSensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void Sqlite_GenerateCaseInsensitiveLike_UsesLike()
    {
        Assert.Equal("col LIKE @param ESCAPE '\\'", _sqlite.GenerateCaseInsensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void Sqlite_GenerateCaseInsensitiveEquals_UsesLower()
    {
        Assert.Equal("LOWER(col) = LOWER(@param)", _sqlite.GenerateCaseInsensitiveEquals("col", "@param"));
    }

    [Fact]
    public void Sqlite_GenerateCoalesce_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(col, default)", _sqlite.GenerateCoalesce("col", "default"));
    }

    [Fact]
    public void Sqlite_GenerateIsNull_ReturnsIsnull()
    {
        Assert.Equal("IFNULL(col, default)", _sqlite.GenerateIsNull("col", "default"));
    }

    [Fact]
    public void Sqlite_GenerateNullIf_ReturnsNullif()
    {
        Assert.Equal("NULLIF(col, compare)", _sqlite.GenerateNullIf("col", "compare"));
    }

    [Fact]
    public void Sqlite_GenerateLength_ReturnsLength()
    {
        Assert.Equal("LENGTH(col)", _sqlite.GenerateLength("col"));
    }

    [Fact]
    public void Sqlite_GenerateSubstring_ReturnsSubstr()
    {
        Assert.Equal("SUBSTR(col, start, length)", _sqlite.GenerateSubstring("col", "start", "length"));
    }

    [Fact]
    public void Sqlite_GenerateYear_ReturnsStrftime()
    {
        Assert.Equal("CAST(strftime('%Y', col) AS INTEGER)", _sqlite.GenerateYear("col"));
    }

    [Fact]
    public void Sqlite_GenerateMonth_ReturnsStrftime()
    {
        Assert.Equal("CAST(strftime('%m', col) AS INTEGER)", _sqlite.GenerateMonth("col"));
    }

    [Fact]
    public void Sqlite_GenerateDay_ReturnsStrftime()
    {
        Assert.Equal("CAST(strftime('%d', col) AS INTEGER)", _sqlite.GenerateDay("col"));
    }

    #endregion

    #region SQL Server Dialect - String Functions

    [Fact]
    public void SqlServer_GenerateCaseSensitiveLike_UsesCollate()
    {
        Assert.Equal("col COLLATE Latin1_General_CS_AS LIKE @param ESCAPE '\\'", _sqlServer.GenerateCaseSensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void SqlServer_GenerateCaseInsensitiveLike_UsesCollate()
    {
        Assert.Equal("col COLLATE Latin1_General_CI_AS LIKE @param ESCAPE '\\'", _sqlServer.GenerateCaseInsensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void SqlServer_GenerateCaseInsensitiveEquals_UsesCollate()
    {
        Assert.Equal("col COLLATE Latin1_General_CI_AS = @param", _sqlServer.GenerateCaseInsensitiveEquals("col", "@param"));
    }

    [Fact]
    public void SqlServer_GenerateCoalesce_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(col, default)", _sqlServer.GenerateCoalesce("col", "default"));
    }

    [Fact]
    public void SqlServer_GenerateIsNull_ReturnsIsnull()
    {
        Assert.Equal("ISNULL(col, default)", _sqlServer.GenerateIsNull("col", "default"));
    }

    [Fact]
    public void SqlServer_GenerateNullIf_ReturnsNullif()
    {
        Assert.Equal("NULLIF(col, compare)", _sqlServer.GenerateNullIf("col", "compare"));
    }

    [Fact]
    public void SqlServer_GenerateLength_ReturnsLen()
    {
        Assert.Equal("LEN(col)", _sqlServer.GenerateLength("col"));
    }

    [Fact]
    public void SqlServer_GenerateSubstring_ReturnsSubstring()
    {
        Assert.Equal("SUBSTRING(col, start, length)", _sqlServer.GenerateSubstring("col", "start", "length"));
    }

    [Fact]
    public void SqlServer_GenerateYear_ReturnsYear()
    {
        Assert.Equal("YEAR(col)", _sqlServer.GenerateYear("col"));
    }

    [Fact]
    public void SqlServer_GenerateMonth_ReturnsMonth()
    {
        Assert.Equal("MONTH(col)", _sqlServer.GenerateMonth("col"));
    }

    [Fact]
    public void SqlServer_GenerateDay_ReturnsDay()
    {
        Assert.Equal("DAY(col)", _sqlServer.GenerateDay("col"));
    }

    #endregion

    #region PostgreSQL Dialect - String Functions

    [Fact]
    public void Postgres_GenerateCaseSensitiveLike_UsesCollate()
    {
        Assert.Equal("col COLLATE \"C\" LIKE @param ESCAPE '\\'", _postgres.GenerateCaseSensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void Postgres_GenerateCaseInsensitiveLike_UsesIlike()
    {
        Assert.Equal("col ILIKE @param ESCAPE '\\'", _postgres.GenerateCaseInsensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void Postgres_GenerateCaseInsensitiveEquals_UsesLower()
    {
        Assert.Equal("LOWER(col) = LOWER(@param)", _postgres.GenerateCaseInsensitiveEquals("col", "@param"));
    }

    [Fact]
    public void Postgres_GenerateCoalesce_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(col, default)", _postgres.GenerateCoalesce("col", "default"));
    }

    [Fact]
    public void Postgres_GenerateIsNull_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(col, default)", _postgres.GenerateIsNull("col", "default"));
    }

    [Fact]
    public void Postgres_GenerateNullIf_ReturnsNullif()
    {
        Assert.Equal("NULLIF(col, compare)", _postgres.GenerateNullIf("col", "compare"));
    }

    [Fact]
    public void Postgres_GenerateLength_ReturnsLength()
    {
        Assert.Equal("LENGTH(col)", _postgres.GenerateLength("col"));
    }

    [Fact]
    public void Postgres_GenerateSubstring_ReturnsSubstring()
    {
        Assert.Equal("SUBSTRING(col FROM start FOR length)", _postgres.GenerateSubstring("col", "start", "length"));
    }

    [Fact]
    public void Postgres_GenerateYear_ReturnsExtract()
    {
        Assert.Equal("EXTRACT(YEAR FROM col)", _postgres.GenerateYear("col"));
    }

    [Fact]
    public void Postgres_GenerateMonth_ReturnsExtract()
    {
        Assert.Equal("EXTRACT(MONTH FROM col)", _postgres.GenerateMonth("col"));
    }

    [Fact]
    public void Postgres_GenerateDay_ReturnsExtract()
    {
        Assert.Equal("EXTRACT(DAY FROM col)", _postgres.GenerateDay("col"));
    }

    #endregion

    #region MySQL Dialect - String Functions

    [Fact]
    public void MySql_GenerateCaseSensitiveLike_UsesCollate()
    {
        Assert.Equal("col COLLATE utf8mb4_bin LIKE @param ESCAPE '\\'", _mySql.GenerateCaseSensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void MySql_GenerateCaseInsensitiveLike_UsesLike()
    {
        Assert.Equal("col LIKE @param ESCAPE '\\'", _mySql.GenerateCaseInsensitiveLike("col", "@param", "\\"));
    }

    [Fact]
    public void MySql_GenerateCaseInsensitiveEquals_UsesCollate()
    {
        Assert.Equal("col COLLATE utf8mb4_general_ci = @param", _mySql.GenerateCaseInsensitiveEquals("col", "@param"));
    }

    [Fact]
    public void MySql_GenerateCoalesce_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(col, default)", _mySql.GenerateCoalesce("col", "default"));
    }

    [Fact]
    public void MySql_GenerateIsNull_ReturnsIfnull()
    {
        Assert.Equal("IFNULL(col, default)", _mySql.GenerateIsNull("col", "default"));
    }

    [Fact]
    public void MySql_GenerateNullIf_ReturnsNullif()
    {
        Assert.Equal("NULLIF(col, compare)", _mySql.GenerateNullIf("col", "compare"));
    }

    [Fact]
    public void MySql_GenerateLength_ReturnsLength()
    {
        Assert.Equal("LENGTH(col)", _mySql.GenerateLength("col"));
    }

    [Fact]
    public void MySql_GenerateSubstring_ReturnsSubstring()
    {
        Assert.Equal("SUBSTRING(col, start, length)", _mySql.GenerateSubstring("col", "start", "length"));
    }

    [Fact]
    public void MySql_GenerateYear_ReturnsYear()
    {
        Assert.Equal("YEAR(col)", _mySql.GenerateYear("col"));
    }

    [Fact]
    public void MySql_GenerateMonth_ReturnsMonth()
    {
        Assert.Equal("MONTH(col)", _mySql.GenerateMonth("col"));
    }

    [Fact]
    public void MySql_GenerateDay_ReturnsDay()
    {
        Assert.Equal("DAY(col)", _mySql.GenerateDay("col"));
    }

    #endregion
}
