using Jaunty.Dialects;

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

    #region Non-Keyword Escaping (all dialects should NOT escape)

    [Theory]
    [InlineData("products")]
    [InlineData("categories")]
    [InlineData("my_table")]
    public void SqlServer_EscapeTableName_NonKeyword_NotEscaped(string tableName)
    {
        Assert.Equal(tableName, _sqlServer.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("my_column")]
    public void SqlServer_EscapeColumnName_NonKeyword_NotEscaped(string columnName)
    {
        Assert.Equal(columnName, _sqlServer.EscapeColumnName(columnName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("categories")]
    public void Postgres_EscapeTableName_NonKeyword_NotEscaped(string tableName)
    {
        Assert.Equal(tableName, _postgres.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("my_column")]
    public void Postgres_EscapeColumnName_NonKeyword_NotEscaped(string columnName)
    {
        Assert.Equal(columnName, _postgres.EscapeColumnName(columnName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("categories")]
    public void MySql_EscapeTableName_NonKeyword_NotEscaped(string tableName)
    {
        Assert.Equal(tableName, _mySql.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("my_column")]
    public void MySql_EscapeColumnName_NonKeyword_NotEscaped(string columnName)
    {
        Assert.Equal(columnName, _mySql.EscapeColumnName(columnName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("categories")]
    public void Sqlite_EscapeTableName_NonKeyword_NotEscaped(string tableName)
    {
        Assert.Equal(tableName, _sqlite.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("products")]
    [InlineData("my_column")]
    public void Sqlite_EscapeColumnName_NonKeyword_NotEscaped(string columnName)
    {
        Assert.Equal(columnName, _sqlite.EscapeColumnName(columnName));
    }

    [Fact]
    public void SqlServer_IsKeyword_ReturnsFalseForNonKeywords()
    {
        Assert.False(_sqlServer.IsKeyword("products"));
        Assert.False(_sqlServer.IsKeyword("my_column"));
    }

    [Fact]
    public void Postgres_IsKeyword_ReturnsFalseForNonKeywords()
    {
        Assert.False(_postgres.IsKeyword("products"));
        Assert.False(_postgres.IsKeyword("my_column"));
    }

    [Fact]
    public void MySql_IsKeyword_ReturnsFalseForNonKeywords()
    {
        Assert.False(_mySql.IsKeyword("products"));
        Assert.False(_mySql.IsKeyword("my_column"));
    }

    [Fact]
    public void Sqlite_IsKeyword_ReturnsFalseForNonKeywords()
    {
        Assert.False(_sqlite.IsKeyword("products"));
        Assert.False(_sqlite.IsKeyword("my_column"));
    }

    #endregion

    #region EscapeTableName with Schema Edge Cases

    [Theory]
    [InlineData(null, "products", "products")]
    [InlineData("", "products", "products")]
    [InlineData("  ", "products", "products")]
    public void SqlServer_EscapeTableName_EmptyOrNullSchema_ReturnsTableOnly(string? schema, string table, string expected)
    {
        Assert.Equal(expected, _sqlServer.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("myschema", "products", "myschema.products")]
    [InlineData("dbo", "products", "dbo.products")]
    public void SqlServer_EscapeTableName_NonKeywordSchema_NotEscaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _sqlServer.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData(null, "products", "products")]
    [InlineData("", "products", "products")]
    public void Postgres_EscapeTableName_EmptyOrNullSchema_ReturnsTableOnly(string? schema, string table, string expected)
    {
        Assert.Equal(expected, _postgres.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("myschema", "products", "myschema.products")]
    public void Postgres_EscapeTableName_NonKeywordSchema_NotEscaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _postgres.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData(null, "products", "products")]
    [InlineData("", "products", "products")]
    public void MySql_EscapeTableName_EmptyOrNullSchema_ReturnsTableOnly(string? schema, string table, string expected)
    {
        Assert.Equal(expected, _mySql.EscapeTableName(schema, table));
    }

    [Theory]
    [InlineData("mydb", "products", "mydb.products")]
    public void MySql_EscapeTableName_NonKeywordSchema_NotEscaped(string schema, string table, string expected)
    {
        Assert.Equal(expected, _mySql.EscapeTableName(schema, table));
    }

    #endregion

    #region FormatPattern Tests (Non-SQLite)

    [Theory]
    [InlineData("test", "%test%")]
    [InlineData("hello world", "%hello world%")]
    public void SqlServer_FormatContainsPattern_WrapsWithPercent(string value, string expected)
    {
        Assert.Equal(expected, _sqlServer.FormatContainsPattern(value));
    }

    [Theory]
    [InlineData("test", "test%")]
    [InlineData("hello", "hello%")]
    public void SqlServer_FormatStartsWithPattern_AppendsPercent(string value, string expected)
    {
        Assert.Equal(expected, _sqlServer.FormatStartsWithPattern(value));
    }

    [Theory]
    [InlineData("test", "%test")]
    [InlineData("hello", "%hello")]
    public void SqlServer_FormatEndsWithPattern_PrependsPercent(string value, string expected)
    {
        Assert.Equal(expected, _sqlServer.FormatEndsWithPattern(value));
    }

    [Theory]
    [InlineData("test", "%test%")]
    public void Postgres_FormatContainsPattern_WrapsWithPercent(string value, string expected)
    {
        Assert.Equal(expected, _postgres.FormatContainsPattern(value));
    }

    [Theory]
    [InlineData("test", "test%")]
    public void Postgres_FormatStartsWithPattern_AppendsPercent(string value, string expected)
    {
        Assert.Equal(expected, _postgres.FormatStartsWithPattern(value));
    }

    [Theory]
    [InlineData("test", "%test")]
    public void Postgres_FormatEndsWithPattern_PrependsPercent(string value, string expected)
    {
        Assert.Equal(expected, _postgres.FormatEndsWithPattern(value));
    }

    [Theory]
    [InlineData("test", "%test%")]
    public void MySql_FormatContainsPattern_WrapsWithPercent(string value, string expected)
    {
        Assert.Equal(expected, _mySql.FormatContainsPattern(value));
    }

    [Theory]
    [InlineData("test", "test%")]
    public void MySql_FormatStartsWithPattern_AppendsPercent(string value, string expected)
    {
        Assert.Equal(expected, _mySql.FormatStartsWithPattern(value));
    }

    [Theory]
    [InlineData("test", "%test")]
    public void MySql_FormatEndsWithPattern_PrependsPercent(string value, string expected)
    {
        Assert.Equal(expected, _mySql.FormatEndsWithPattern(value));
    }

    #endregion

    #region PostgreSQL GetLastInsertIdSql Edge Cases

    [Fact]
    public void Postgres_GetLastInsertIdSql_NoColumns_ReturnsDefaultReturning()
    {
        Assert.Equal("RETURNING id;", _postgres.GetLastInsertIdSql());
    }

    [Fact]
    public void Postgres_GetLastInsertIdSql_MultipleColumns_ReturnsAllColumns()
    {
        Assert.Equal("RETURNING Id, Name;", _postgres.GetLastInsertIdSql("Id", "Name"));
    }

    #endregion

    #region SQL Server FK Toggle Returns Null

    [Fact]
    public void SqlServer_GetDisableForeignKeyChecksSql_ReturnsNull()
    {
        Assert.Null(_sqlServer.GetDisableForeignKeyChecksSql());
    }

    [Fact]
    public void SqlServer_GetEnableForeignKeyChecksSql_ReturnsNull()
    {
        Assert.Null(_sqlServer.GetEnableForeignKeyChecksSql());
    }

    #endregion

    #region String Functions - Upper, Lower, Trim (All Dialects)

    [Fact]
    public void SqlServer_GenerateUpper_ReturnsUpper()
    {
        Assert.Equal("UPPER(col)", _sqlServer.GenerateUpper("col"));
    }

    [Fact]
    public void SqlServer_GenerateLower_ReturnsLower()
    {
        Assert.Equal("LOWER(col)", _sqlServer.GenerateLower("col"));
    }

    [Fact]
    public void SqlServer_GenerateTrim_ReturnsTrim()
    {
        Assert.Equal("TRIM(col)", _sqlServer.GenerateTrim("col"));
    }

    [Fact]
    public void Postgres_GenerateUpper_ReturnsUpper()
    {
        Assert.Equal("UPPER(col)", _postgres.GenerateUpper("col"));
    }

    [Fact]
    public void Postgres_GenerateLower_ReturnsLower()
    {
        Assert.Equal("LOWER(col)", _postgres.GenerateLower("col"));
    }

    [Fact]
    public void Postgres_GenerateTrim_ReturnsTrim()
    {
        Assert.Equal("TRIM(col)", _postgres.GenerateTrim("col"));
    }

    [Fact]
    public void MySql_GenerateUpper_ReturnsUpper()
    {
        Assert.Equal("UPPER(col)", _mySql.GenerateUpper("col"));
    }

    [Fact]
    public void MySql_GenerateLower_ReturnsLower()
    {
        Assert.Equal("LOWER(col)", _mySql.GenerateLower("col"));
    }

    [Fact]
    public void MySql_GenerateTrim_ReturnsTrim()
    {
        Assert.Equal("TRIM(col)", _mySql.GenerateTrim("col"));
    }

    [Fact]
    public void Sqlite_GenerateUpper_ReturnsUpper()
    {
        Assert.Equal("UPPER(col)", _sqlite.GenerateUpper("col"));
    }

    [Fact]
    public void Sqlite_GenerateLower_ReturnsLower()
    {
        Assert.Equal("LOWER(col)", _sqlite.GenerateLower("col"));
    }

    [Fact]
    public void Sqlite_GenerateTrim_ReturnsTrim()
    {
        Assert.Equal("TRIM(col)", _sqlite.GenerateTrim("col"));
    }

    #endregion

    #region Upsert SQL Generation (All Dialects)

    [Fact]
    public void SqlServer_SupportsUpsert_ReturnsTrue()
    {
        Assert.True(_sqlServer.SupportsUpsert);
    }

    [Fact]
    public void SqlServer_GenerateUpsertSql_GeneratesMergeSql()
    {
        var sql = _sqlServer.GenerateUpsertSql(
            "products",
            insertColumns: new[] { "id", "name", "value" },
            insertParams: new[] { "@id", "@name", "@value" },
            updateColumns: new[] { "name", "value" },
            updateParams: new[] { "@name", "@value" },
            keyColumns: new[] { "id" });

        Assert.Contains("MERGE INTO products AS target", sql);
        Assert.Contains("USING (VALUES (@id, @name, @value))", sql);
        Assert.Contains("AS source (id, name, value)", sql);
        Assert.Contains("ON target.id = source.id", sql);
        Assert.Contains("WHEN MATCHED THEN UPDATE SET target.name = source.name, target.value = source.value", sql);
        Assert.Contains("WHEN NOT MATCHED THEN INSERT (id, name, value) VALUES (source.id, source.name, source.value)", sql);
    }

    [Fact]
    public void SqlServer_GenerateUpsertSql_MultipleKeys_GeneratesCompoundOn()
    {
        var sql = _sqlServer.GenerateUpsertSql(
            "table",
            insertColumns: new[] { "k1", "k2", "col" },
            insertParams: new[] { "@k1", "@k2", "@col" },
            updateColumns: new[] { "col" },
            updateParams: new[] { "@col" },
            keyColumns: new[] { "k1", "k2" });

        Assert.Contains("target.k1 = source.k1 AND target.k2 = source.k2", sql);
    }

    [Fact]
    public void Postgres_SupportsUpsert_ReturnsTrue()
    {
        Assert.True(_postgres.SupportsUpsert);
    }

    [Fact]
    public void Postgres_GenerateUpsertSql_GeneratesOnConflictSql()
    {
        var sql = _postgres.GenerateUpsertSql(
            "products",
            insertColumns: new[] { "id", "name", "value" },
            insertParams: new[] { "@id", "@name", "@value" },
            updateColumns: new[] { "name", "value" },
            updateParams: new[] { "@name", "@value" },
            keyColumns: new[] { "id" });

        Assert.Contains("INSERT INTO products", sql);
        Assert.Contains("(id, name, value) VALUES (@id, @name, @value)", sql);
        Assert.Contains("ON CONFLICT (id)", sql);
        Assert.Contains("DO UPDATE SET name = EXCLUDED.name, value = EXCLUDED.value", sql);
    }

    [Fact]
    public void MySql_SupportsUpsert_ReturnsTrue()
    {
        Assert.True(_mySql.SupportsUpsert);
    }

    [Fact]
    public void MySql_GenerateUpsertSql_GeneratesOnDuplicateKeySql()
    {
        var sql = _mySql.GenerateUpsertSql(
            "products",
            insertColumns: new[] { "id", "name", "value" },
            insertParams: new[] { "@id", "@name", "@value" },
            updateColumns: new[] { "name", "value" },
            updateParams: new[] { "@name", "@value" },
            keyColumns: new[] { "id" });

        Assert.Contains("INSERT INTO products", sql);
        Assert.Contains("(id, name, value) VALUES (@id, @name, @value)", sql);
        Assert.Contains("ON DUPLICATE KEY UPDATE", sql);
        Assert.Contains("name = VALUES(name)", sql);
        Assert.Contains("value = VALUES(value)", sql);
    }

    [Fact]
    public void Sqlite_SupportsUpsert_ReturnsTrue()
    {
        Assert.True(_sqlite.SupportsUpsert);
    }

    [Fact]
    public void Sqlite_GenerateUpsertSql_GeneratesOnConflictSql()
    {
        var sql = _sqlite.GenerateUpsertSql(
            "products",
            insertColumns: new[] { "id", "name", "value" },
            insertParams: new[] { "@id", "@name", "@value" },
            updateColumns: new[] { "name", "value" },
            updateParams: new[] { "@name", "@value" },
            keyColumns: new[] { "id" });

        Assert.Contains("INSERT INTO products", sql);
        Assert.Contains("(id, name, value) VALUES (@id, @name, @value)", sql);
        Assert.Contains("ON CONFLICT (id)", sql);
        Assert.Contains("DO UPDATE SET name = excluded.name, value = excluded.value", sql);
    }

    [Fact]
    public void Sqlite_GenerateUpsertSql_MultipleKeys_GeneratesCompoundConflict()
    {
        var sql = _sqlite.GenerateUpsertSql(
            "table",
            insertColumns: new[] { "k1", "k2", "col" },
            insertParams: new[] { "@k1", "@k2", "@col" },
            updateColumns: new[] { "col" },
            updateParams: new[] { "@col" },
            keyColumns: new[] { "k1", "k2" });

        Assert.Contains("ON CONFLICT (k1, k2)", sql);
    }

    #endregion

    #region Window Functions (All Dialects)

    [Fact]
    public void SqlServer_GenerateRowNumber_ReturnsRowNumber()
    {
        Assert.Equal("ROW_NUMBER()", _sqlServer.GenerateRowNumber());
    }

    [Fact]
    public void SqlServer_GenerateRank_ReturnsRank()
    {
        Assert.Equal("RANK()", _sqlServer.GenerateRank());
    }

    [Fact]
    public void SqlServer_GenerateDenseRank_ReturnsDenseRank()
    {
        Assert.Equal("DENSE_RANK()", _sqlServer.GenerateDenseRank());
    }

    [Fact]
    public void SqlServer_GenerateNTile_ReturnsNTile()
    {
        Assert.Equal("NTILE(4)", _sqlServer.GenerateNTile(4));
    }

    [Fact]
    public void Postgres_GenerateRowNumber_ReturnsRowNumber()
    {
        Assert.Equal("ROW_NUMBER()", _postgres.GenerateRowNumber());
    }

    [Fact]
    public void Postgres_GenerateRank_ReturnsRank()
    {
        Assert.Equal("RANK()", _postgres.GenerateRank());
    }

    [Fact]
    public void Postgres_GenerateDenseRank_ReturnsDenseRank()
    {
        Assert.Equal("DENSE_RANK()", _postgres.GenerateDenseRank());
    }

    [Fact]
    public void Postgres_GenerateNTile_ReturnsNTile()
    {
        Assert.Equal("NTILE(3)", _postgres.GenerateNTile(3));
    }

    [Fact]
    public void MySql_GenerateRowNumber_ReturnsRowNumber()
    {
        Assert.Equal("ROW_NUMBER()", _mySql.GenerateRowNumber());
    }

    [Fact]
    public void MySql_GenerateRank_ReturnsRank()
    {
        Assert.Equal("RANK()", _mySql.GenerateRank());
    }

    [Fact]
    public void MySql_GenerateDenseRank_ReturnsDenseRank()
    {
        Assert.Equal("DENSE_RANK()", _mySql.GenerateDenseRank());
    }

    [Fact]
    public void MySql_GenerateNTile_ReturnsNTile()
    {
        Assert.Equal("NTILE(5)", _mySql.GenerateNTile(5));
    }

    [Fact]
    public void Sqlite_GenerateRowNumber_ReturnsRowNumber()
    {
        Assert.Equal("ROW_NUMBER()", _sqlite.GenerateRowNumber());
    }

    [Fact]
    public void Sqlite_GenerateRank_ReturnsRank()
    {
        Assert.Equal("RANK()", _sqlite.GenerateRank());
    }

    [Fact]
    public void Sqlite_GenerateDenseRank_ReturnsDenseRank()
    {
        Assert.Equal("DENSE_RANK()", _sqlite.GenerateDenseRank());
    }

    [Fact]
    public void Sqlite_GenerateNTile_ReturnsNTile()
    {
        Assert.Equal("NTILE(2)", _sqlite.GenerateNTile(2));
    }

    #endregion

    #region GenerateOverClause (All Dialects)

    [Fact]
    public void SqlServer_GenerateOverClause_PartitionAndOrder_GeneratesCorrectSql()
    {
        var result = _sqlServer.GenerateOverClause(
            new[] { "category_id" },
            new[] { ("price", false) });

        Assert.Equal(" OVER (PARTITION BY category_id ORDER BY price)", result);
    }

    [Fact]
    public void SqlServer_GenerateOverClause_OrderOnly_GeneratesCorrectSql()
    {
        var result = _sqlServer.GenerateOverClause(
            null,
            new[] { ("price", true) });

        Assert.Equal(" OVER (ORDER BY price DESC)", result);
    }

    [Fact]
    public void SqlServer_GenerateOverClause_PartitionOnly_GeneratesCorrectSql()
    {
        var result = _sqlServer.GenerateOverClause(
            new[] { "category_id" },
            null);

        Assert.Equal(" OVER (PARTITION BY category_id)", result);
    }

    [Fact]
    public void SqlServer_GenerateOverClause_Empty_GeneratesEmptyOver()
    {
        var result = _sqlServer.GenerateOverClause(null, null);
        Assert.Equal(" OVER ()", result);
    }

    [Fact]
    public void SqlServer_GenerateOverClause_MultiplePartitionsAndOrders_GeneratesCorrectSql()
    {
        var result = _sqlServer.GenerateOverClause(
            new[] { "col1", "col2" },
            new[] { ("col3", false), ("col4", true) });

        Assert.Equal(" OVER (PARTITION BY col1, col2 ORDER BY col3, col4 DESC)", result);
    }

    [Fact]
    public void Postgres_GenerateOverClause_PartitionAndOrder_GeneratesCorrectSql()
    {
        var result = _postgres.GenerateOverClause(
            new[] { "category_id" },
            new[] { ("price", false) });

        Assert.Equal(" OVER (PARTITION BY category_id ORDER BY price)", result);
    }

    [Fact]
    public void MySql_GenerateOverClause_PartitionAndOrder_GeneratesCorrectSql()
    {
        var result = _mySql.GenerateOverClause(
            new[] { "category_id" },
            new[] { ("price", true) });

        Assert.Equal(" OVER (PARTITION BY category_id ORDER BY price DESC)", result);
    }

    [Fact]
    public void Sqlite_GenerateOverClause_PartitionAndOrder_GeneratesCorrectSql()
    {
        var result = _sqlite.GenerateOverClause(
            new[] { "category_id" },
            new[] { ("price", false) });

        Assert.Equal(" OVER (PARTITION BY category_id ORDER BY price)", result);
    }

    [Fact]
    public void Sqlite_GenerateOverClause_EmptyArrays_GeneratesEmptyOver()
    {
        var result = _sqlite.GenerateOverClause(
            Array.Empty<string>(),
            Array.Empty<(string, bool)>());

        Assert.Equal(" OVER ()", result);
    }

    #endregion

    #region GenerateWindowAggregate (All Dialects)

    [Fact]
    public void SqlServer_GenerateWindowAggregate_WithExpression_GeneratesCorrectSql()
    {
        Assert.Equal("SUM(price)", _sqlServer.GenerateWindowAggregate("SUM", "price"));
    }

    [Fact]
    public void SqlServer_GenerateWindowAggregate_WithoutExpression_GeneratesCountStar()
    {
        Assert.Equal("COUNT(*)", _sqlServer.GenerateWindowAggregate("COUNT", null));
    }

    [Fact]
    public void Postgres_GenerateWindowAggregate_WithExpression_GeneratesCorrectSql()
    {
        Assert.Equal("AVG(score)", _postgres.GenerateWindowAggregate("AVG", "score"));
    }

    [Fact]
    public void Postgres_GenerateWindowAggregate_WithoutExpression_GeneratesCountStar()
    {
        Assert.Equal("COUNT(*)", _postgres.GenerateWindowAggregate("COUNT", null));
    }

    [Fact]
    public void MySql_GenerateWindowAggregate_WithExpression_GeneratesCorrectSql()
    {
        Assert.Equal("MAX(quantity)", _mySql.GenerateWindowAggregate("MAX", "quantity"));
    }

    [Fact]
    public void MySql_GenerateWindowAggregate_WithoutExpression_GeneratesCountStar()
    {
        Assert.Equal("COUNT(*)", _mySql.GenerateWindowAggregate("COUNT", null));
    }

    [Fact]
    public void Sqlite_GenerateWindowAggregate_WithExpression_GeneratesCorrectSql()
    {
        Assert.Equal("MIN(price)", _sqlite.GenerateWindowAggregate("MIN", "price"));
    }

    [Fact]
    public void Sqlite_GenerateWindowAggregate_WithoutExpression_GeneratesCountStar()
    {
        Assert.Equal("COUNT(*)", _sqlite.GenerateWindowAggregate("COUNT", null));
    }

    #endregion

    #region Coalesce with Multiple Arguments

    [Fact]
    public void SqlServer_GenerateCoalesce_ThreeArgs_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(a, b, c)", _sqlServer.GenerateCoalesce("a", "b", "c"));
    }

    [Fact]
    public void Postgres_GenerateCoalesce_ThreeArgs_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(a, b, c)", _postgres.GenerateCoalesce("a", "b", "c"));
    }

    [Fact]
    public void MySql_GenerateCoalesce_ThreeArgs_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(a, b, c)", _mySql.GenerateCoalesce("a", "b", "c"));
    }

    [Fact]
    public void Sqlite_GenerateCoalesce_ThreeArgs_ReturnsCoalesce()
    {
        Assert.Equal("COALESCE(a, b, c)", _sqlite.GenerateCoalesce("a", "b", "c"));
    }

    #endregion
}