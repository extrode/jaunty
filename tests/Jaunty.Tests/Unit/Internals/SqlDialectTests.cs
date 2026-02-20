using System.Data;

using Jaunty.Internals.Dialects;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Unit tests for SQL dialect implementations.
/// Tests SQL generation differences between SQLite, SQL Server, MySQL, and PostgreSQL.
/// </summary>
public class SqlDialectTests
{
    #region SQLite Dialect

    public class SQLiteDialectTests
    {
        private readonly SQLiteDialect _dialect = new();

        [Fact]
        public void GetDefaultSchema_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, _dialect.GetDefaultSchema());
        }

        [Fact]
        public void EscapeTableName_NonKeyword_NoEscaping()
        {
            Assert.Equal("products", _dialect.EscapeTableName(null, "products"));
        }

        [Fact]
        public void EscapeTableName_Keyword_EscapesWithQuotes()
        {
            Assert.Equal("\"ORDER\"", _dialect.EscapeTableName(null, "ORDER"));
        }

        [Fact]
        public void EscapeTableName_SchemaIgnored()
        {
            // SQLite doesn't support schemas, so schema is ignored
            Assert.Equal("products", _dialect.EscapeTableName("myschema", "products"));
        }

        [Fact]
        public void EscapeColumnName_NonKeyword_NoEscaping()
        {
            Assert.Equal("name", _dialect.EscapeColumnName("name"));
        }

        [Fact]
        public void EscapeColumnName_Keyword_EscapesWithQuotes()
        {
            Assert.Equal("\"ORDER\"", _dialect.EscapeColumnName("ORDER"));
        }

        [Fact]
        public void GetLastInsertIdSql_ReturnsLastInsertRowId()
        {
            Assert.Equal("SELECT last_insert_rowid();", _dialect.GetLastInsertIdSql());
        }

        [Fact]
        public void GetPagingSql_UsesLimitOffset()
        {
            var result = _dialect.GetPagingSql("SELECT * FROM t", 10, 20);
            Assert.Equal("SELECT * FROM t LIMIT 20 OFFSET 10", result);
        }

        [Fact]
        public void IsKeyword_ReservedWord_ReturnsTrue()
        {
            Assert.True(_dialect.IsKeyword("SELECT"));
            Assert.True(_dialect.IsKeyword("ORDER"));
            Assert.True(_dialect.IsKeyword("USER"));
        }

        [Fact]
        public void IsKeyword_NonReserved_ReturnsFalse()
        {
            Assert.False(_dialect.IsKeyword("products"));
            Assert.False(_dialect.IsKeyword("my_column"));
        }

        [Fact]
        public void IsKeyword_CaseInsensitive()
        {
            Assert.True(_dialect.IsKeyword("select"));
            Assert.True(_dialect.IsKeyword("SELECT"));
            Assert.True(_dialect.IsKeyword("Select"));
        }

        [Fact]
        public void SupportsUpsert_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsUpsert);
        }

        [Fact]
        public void SupportsForeignKeyToggle_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsForeignKeyToggle);
        }

        [Fact]
        public void GetDisableForeignKeyChecksSql_ReturnsPragma()
        {
            Assert.Equal("PRAGMA foreign_keys = OFF", _dialect.GetDisableForeignKeyChecksSql());
        }

        [Fact]
        public void GetEnableForeignKeyChecksSql_ReturnsPragma()
        {
            Assert.Equal("PRAGMA foreign_keys = ON", _dialect.GetEnableForeignKeyChecksSql());
        }

        [Fact]
        public void GenerateIsNull_UsesIFNULL()
        {
            Assert.Equal("IFNULL(col, 0)", _dialect.GenerateIsNull("col", "0"));
        }

        [Fact]
        public void GenerateLength_UsesLENGTH()
        {
            Assert.Equal("LENGTH(col)", _dialect.GenerateLength("col"));
        }

        [Fact]
        public void GenerateSubstring_UsesSUBSTR()
        {
            Assert.Equal("SUBSTR(col, 1, 5)", _dialect.GenerateSubstring("col", "1", "5"));
        }

        [Fact]
        public void GenerateYear_UsesStrftime()
        {
            Assert.Equal("CAST(strftime('%Y', col) AS INTEGER)", _dialect.GenerateYear("col"));
        }

        [Fact]
        public void GenerateUpsertSql_UsesOnConflict()
        {
            var result = _dialect.GenerateUpsertSql(
                "items",
                new[] { "id", "name" },
                new[] { "@Id", "@Name" },
                new[] { "name" },
                new[] { "@Name" },
                new[] { "id" });

            Assert.Contains("ON CONFLICT (id)", result);
            Assert.Contains("DO UPDATE SET", result);
            Assert.Contains("excluded.name", result);
        }

        [Fact]
        public void GenerateCaseInsensitiveLike_UsesLike()
        {
            var result = _dialect.GenerateCaseInsensitiveLike("col", "@p", "\\");
            Assert.Contains("LIKE", result);
        }

        [Fact]
        public void GenerateCaseSensitiveLike_UsesGlob()
        {
            var result = _dialect.GenerateCaseSensitiveLike("col", "@p", "\\");
            Assert.Contains("GLOB", result);
        }

        [Fact]
        public void GenerateRowNumber_ReturnsStandard()
        {
            Assert.Equal("ROW_NUMBER()", _dialect.GenerateRowNumber());
        }

        [Fact]
        public void GenerateOverClause_WithPartitionAndOrder()
        {
            var result = _dialect.GenerateOverClause(
                new[] { "dept" },
                new[] { ("salary", true) });

            Assert.Contains("PARTITION BY dept", result);
            Assert.Contains("ORDER BY salary DESC", result);
        }

        [Fact]
        public void GenerateOverClause_Empty()
        {
            var result = _dialect.GenerateOverClause(null, null);
            Assert.Equal(" OVER ()", result);
        }

        [Fact]
        public void GenerateWindowAggregate_WithExpression()
        {
            Assert.Equal("SUM(col)", _dialect.GenerateWindowAggregate("SUM", "col"));
        }

        [Fact]
        public void GenerateWindowAggregate_WithoutExpression()
        {
            Assert.Equal("COUNT(*)", _dialect.GenerateWindowAggregate("COUNT", null));
        }

        [Fact]
        public void FormatContainsPattern_UsesGlobSyntax()
        {
            Assert.Equal("*test*", _dialect.FormatContainsPattern("test"));
        }

        [Fact]
        public void FormatStartsWithPattern_UsesGlobSyntax()
        {
            Assert.Equal("test*", _dialect.FormatStartsWithPattern("test"));
        }

        [Fact]
        public void FormatEndsWithPattern_UsesGlobSyntax()
        {
            Assert.Equal("*test", _dialect.FormatEndsWithPattern("test"));
        }
    }

    #endregion

    #region SQL Server Dialect

    public class SqlServerDialectTests
    {
        private readonly SqlServerDialect _dialect = new();

        [Fact]
        public void GetDefaultSchema_ReturnsDbo()
        {
            Assert.Equal("dbo", _dialect.GetDefaultSchema());
        }

        [Fact]
        public void EscapeTableName_NonKeyword_NoEscaping()
        {
            Assert.Equal("products", _dialect.EscapeTableName(null, "products"));
        }

        [Fact]
        public void EscapeTableName_Keyword_EscapesWithBrackets()
        {
            Assert.Equal("[ORDER]", _dialect.EscapeTableName(null, "ORDER"));
        }

        [Fact]
        public void EscapeTableName_WithSchema()
        {
            Assert.Equal("myschema.products", _dialect.EscapeTableName("myschema", "products"));
        }

        [Fact]
        public void EscapeTableName_KeywordSchema()
        {
            Assert.Equal("[USER].products", _dialect.EscapeTableName("USER", "products"));
        }

        [Fact]
        public void EscapeColumnName_Keyword_EscapesWithBrackets()
        {
            Assert.Equal("[USER]", _dialect.EscapeColumnName("USER"));
        }

        [Fact]
        public void GetLastInsertIdSql_ReturnsScopeIdentity()
        {
            Assert.Equal("SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", _dialect.GetLastInsertIdSql());
        }

        [Fact]
        public void GetPagingSql_UsesOffsetFetch()
        {
            var result = _dialect.GetPagingSql("SELECT * FROM t", 10, 20);
            Assert.Equal("SELECT * FROM t OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", result);
        }

        [Fact]
        public void SupportsForeignKeyToggle_ReturnsFalse()
        {
            Assert.False(_dialect.SupportsForeignKeyToggle);
        }

        [Fact]
        public void GetDisableForeignKeyChecksSql_ReturnsNull()
        {
            Assert.Null(_dialect.GetDisableForeignKeyChecksSql());
        }

        [Fact]
        public void SupportsUpsert_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsUpsert);
        }

        [Fact]
        public void GenerateUpsertSql_UsesMerge()
        {
            var result = _dialect.GenerateUpsertSql(
                "items",
                new[] { "id", "name" },
                new[] { "@Id", "@Name" },
                new[] { "name" },
                new[] { "@Name" },
                new[] { "id" });

            Assert.Contains("MERGE INTO", result);
            Assert.Contains("WHEN MATCHED THEN UPDATE", result);
            Assert.Contains("WHEN NOT MATCHED THEN INSERT", result);
        }

        [Fact]
        public void GenerateIsNull_UsesISNULL()
        {
            Assert.Equal("ISNULL(col, 0)", _dialect.GenerateIsNull("col", "0"));
        }

        [Fact]
        public void GenerateLength_UsesLEN()
        {
            Assert.Equal("LEN(col)", _dialect.GenerateLength("col"));
        }

        [Fact]
        public void GenerateSubstring_UsesSUBSTRING()
        {
            Assert.Equal("SUBSTRING(col, 1, 5)", _dialect.GenerateSubstring("col", "1", "5"));
        }

        [Fact]
        public void GenerateYear_UsesYEAR()
        {
            Assert.Equal("YEAR(col)", _dialect.GenerateYear("col"));
        }

        [Fact]
        public void GenerateCaseSensitiveLike_UsesCollation()
        {
            var result = _dialect.GenerateCaseSensitiveLike("col", "@p", "\\");
            Assert.Contains("COLLATE Latin1_General_CS_AS", result);
        }

        [Fact]
        public void GenerateCaseInsensitiveLike_UsesCollation()
        {
            var result = _dialect.GenerateCaseInsensitiveLike("col", "@p", "\\");
            Assert.Contains("COLLATE Latin1_General_CI_AS", result);
        }

        [Fact]
        public void FormatContainsPattern_UsesPercent()
        {
            Assert.Equal("%test%", _dialect.FormatContainsPattern("test"));
        }
    }

    #endregion

    #region MySQL Dialect

    public class MySqlDialectTests
    {
        private readonly MySqlDialect _dialect = new();

        [Fact]
        public void GetDefaultSchema_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, _dialect.GetDefaultSchema());
        }

        [Fact]
        public void EscapeTableName_Keyword_EscapesWithBackticks()
        {
            Assert.Equal("`ORDER`", _dialect.EscapeTableName(null, "ORDER"));
        }

        [Fact]
        public void EscapeColumnName_Keyword_EscapesWithBackticks()
        {
            Assert.Equal("`USER`", _dialect.EscapeColumnName("USER"));
        }

        [Fact]
        public void EscapeTableName_WithSchema()
        {
            Assert.Equal("mydb.products", _dialect.EscapeTableName("mydb", "products"));
        }

        [Fact]
        public void GetLastInsertIdSql_ReturnsLastInsertId()
        {
            Assert.Equal("SELECT LAST_INSERT_ID();", _dialect.GetLastInsertIdSql());
        }

        [Fact]
        public void GetPagingSql_UsesLimitWithComma()
        {
            var result = _dialect.GetPagingSql("SELECT * FROM t", 10, 20);
            Assert.Equal("SELECT * FROM t LIMIT 10, 20", result);
        }

        [Fact]
        public void SupportsForeignKeyToggle_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsForeignKeyToggle);
        }

        [Fact]
        public void GetDisableForeignKeyChecksSql_ReturnsFkChecksOff()
        {
            Assert.Equal("SET FOREIGN_KEY_CHECKS = 0", _dialect.GetDisableForeignKeyChecksSql());
        }

        [Fact]
        public void GetEnableForeignKeyChecksSql_ReturnsFkChecksOn()
        {
            Assert.Equal("SET FOREIGN_KEY_CHECKS = 1", _dialect.GetEnableForeignKeyChecksSql());
        }

        [Fact]
        public void SupportsUpsert_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsUpsert);
        }

        [Fact]
        public void GenerateUpsertSql_UsesOnDuplicateKey()
        {
            var result = _dialect.GenerateUpsertSql(
                "items",
                new[] { "id", "name" },
                new[] { "@Id", "@Name" },
                new[] { "name" },
                new[] { "@Name" },
                new[] { "id" });

            Assert.Contains("ON DUPLICATE KEY UPDATE", result);
            Assert.Contains("VALUES(name)", result);
        }

        [Fact]
        public void GenerateIsNull_UsesIFNULL()
        {
            Assert.Equal("IFNULL(col, 0)", _dialect.GenerateIsNull("col", "0"));
        }

        [Fact]
        public void GenerateLength_UsesLENGTH()
        {
            Assert.Equal("LENGTH(col)", _dialect.GenerateLength("col"));
        }

        [Fact]
        public void GenerateCaseSensitiveLike_UsesCollation()
        {
            var result = _dialect.GenerateCaseSensitiveLike("col", "@p", "\\");
            Assert.Contains("COLLATE utf8mb4_bin", result);
        }

        [Fact]
        public void GenerateCaseInsensitiveEquals_UsesCollation()
        {
            var result = _dialect.GenerateCaseInsensitiveEquals("col", "@p");
            Assert.Contains("COLLATE utf8mb4_general_ci", result);
        }

        [Fact]
        public void FormatContainsPattern_UsesPercent()
        {
            Assert.Equal("%test%", _dialect.FormatContainsPattern("test"));
        }

        [Fact]
        public void FormatStartsWithPattern_UsesTrailingPercent()
        {
            Assert.Equal("test%", _dialect.FormatStartsWithPattern("test"));
        }

        [Fact]
        public void FormatEndsWithPattern_UsesLeadingPercent()
        {
            Assert.Equal("%test", _dialect.FormatEndsWithPattern("test"));
        }

        [Fact]
        public void EscapeTableName_NonKeyword_ReturnsUnescaped()
        {
            Assert.Equal("products", _dialect.EscapeTableName(null, "products"));
        }

        [Fact]
        public void EscapeColumnName_NonKeyword_ReturnsUnescaped()
        {
            Assert.Equal("product_name", _dialect.EscapeColumnName("product_name"));
        }

        [Fact]
        public void EscapeTableName_KeywordSchema_KeywordTable_EscapesBoth()
        {
            Assert.Equal("`DATABASE`.`ORDER`", _dialect.EscapeTableName("DATABASE", "ORDER"));
        }

        [Fact]
        public void GenerateCaseInsensitiveLike_UsesStandardLike()
        {
            var result = _dialect.GenerateCaseInsensitiveLike("col", "@p", "\\");
            Assert.Equal("col LIKE @p ESCAPE '\\'", result);
        }

        [Fact]
        public void GenerateNullIf_UsesNullIf()
        {
            Assert.Equal("NULLIF(a, b)", _dialect.GenerateNullIf("a", "b"));
        }

        [Fact]
        public void GenerateCoalesce_UsesCoalesce()
        {
            Assert.Equal("COALESCE(a, b, c)", _dialect.GenerateCoalesce("a", "b", "c"));
        }

        [Fact]
        public void GenerateUpper_UsesUpper()
        {
            Assert.Equal("UPPER(col)", _dialect.GenerateUpper("col"));
        }

        [Fact]
        public void GenerateLower_UsesLower()
        {
            Assert.Equal("LOWER(col)", _dialect.GenerateLower("col"));
        }

        [Fact]
        public void GenerateTrim_UsesTrim()
        {
            Assert.Equal("TRIM(col)", _dialect.GenerateTrim("col"));
        }

        [Fact]
        public void GenerateSubstring_UsesSubstring()
        {
            Assert.Equal("SUBSTRING(col, 1, 5)", _dialect.GenerateSubstring("col", "1", "5"));
        }

        [Fact]
        public void GenerateYear_UsesYear()
        {
            Assert.Equal("YEAR(col)", _dialect.GenerateYear("col"));
        }

        [Fact]
        public void GenerateMonth_UsesMonth()
        {
            Assert.Equal("MONTH(col)", _dialect.GenerateMonth("col"));
        }

        [Fact]
        public void GenerateDay_UsesDay()
        {
            Assert.Equal("DAY(col)", _dialect.GenerateDay("col"));
        }

        [Fact]
        public void GenerateRowNumber_ReturnsRowNumber()
        {
            Assert.Equal("ROW_NUMBER()", _dialect.GenerateRowNumber());
        }

        [Fact]
        public void GenerateRank_ReturnsRank()
        {
            Assert.Equal("RANK()", _dialect.GenerateRank());
        }

        [Fact]
        public void GenerateDenseRank_ReturnsDenseRank()
        {
            Assert.Equal("DENSE_RANK()", _dialect.GenerateDenseRank());
        }

        [Fact]
        public void GenerateNTile_ReturnsNTile()
        {
            Assert.Equal("NTILE(4)", _dialect.GenerateNTile(4));
        }

        [Fact]
        public void GenerateOverClause_PartitionOnly_NoOrder()
        {
            var result = _dialect.GenerateOverClause(
                new[] { "department" },
                null);
            Assert.Equal(" OVER (PARTITION BY department)", result);
        }

        [Fact]
        public void GenerateOverClause_OrderOnly_NoPartition()
        {
            var result = _dialect.GenerateOverClause(
                null,
                new[] { ("salary", true) });
            Assert.Equal(" OVER (ORDER BY salary DESC)", result);
        }

        [Fact]
        public void GenerateOverClause_PartitionAndOrder()
        {
            var result = _dialect.GenerateOverClause(
                new[] { "department" },
                new[] { ("salary", false) });
            Assert.Equal(" OVER (PARTITION BY department ORDER BY salary)", result);
        }

        [Fact]
        public void GenerateWindowAggregate_WithExpression_ReturnsFunction()
        {
            Assert.Equal("SUM(salary)", _dialect.GenerateWindowAggregate("SUM", "salary"));
        }

        [Fact]
        public void GenerateWindowAggregate_NullExpression_ReturnsStar()
        {
            Assert.Equal("COUNT(*)", _dialect.GenerateWindowAggregate("COUNT", null));
        }
    }

    #endregion

    #region PostgreSQL Dialect

    public class PostgreSqlDialectTests
    {
        private readonly PostgreSqlDialect _dialect = new();

        [Fact]
        public void GetDefaultSchema_ReturnsPublic()
        {
            Assert.Equal("public", _dialect.GetDefaultSchema());
        }

        [Fact]
        public void EscapeTableName_Keyword_EscapesWithDoubleQuotes()
        {
            Assert.Equal("\"ORDER\"", _dialect.EscapeTableName(null, "ORDER"));
        }

        [Fact]
        public void EscapeColumnName_Keyword_EscapesWithDoubleQuotes()
        {
            Assert.Equal("\"USER\"", _dialect.EscapeColumnName("USER"));
        }

        [Fact]
        public void EscapeTableName_WithSchema()
        {
            Assert.Equal("myschema.products", _dialect.EscapeTableName("myschema", "products"));
        }

        [Fact]
        public void GetLastInsertIdSql_ReturnsReturningId()
        {
            Assert.Equal("RETURNING id;", _dialect.GetLastInsertIdSql());
        }

        [Fact]
        public void GetPagingSql_UsesLimitOffset()
        {
            var result = _dialect.GetPagingSql("SELECT * FROM t", 10, 20);
            Assert.Equal("SELECT * FROM t LIMIT 20 OFFSET 10", result);
        }

        [Fact]
        public void SupportsForeignKeyToggle_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsForeignKeyToggle);
        }

        [Fact]
        public void GetDisableForeignKeyChecksSql_UsesReplicationRole()
        {
            Assert.Equal("SET session_replication_role = 'replica'", _dialect.GetDisableForeignKeyChecksSql());
        }

        [Fact]
        public void GetEnableForeignKeyChecksSql_UsesReplicationRole()
        {
            Assert.Equal("SET session_replication_role = 'origin'", _dialect.GetEnableForeignKeyChecksSql());
        }

        [Fact]
        public void SupportsUpsert_ReturnsTrue()
        {
            Assert.True(_dialect.SupportsUpsert);
        }

        [Fact]
        public void GenerateUpsertSql_UsesOnConflict()
        {
            var result = _dialect.GenerateUpsertSql(
                "items",
                new[] { "id", "name" },
                new[] { "@Id", "@Name" },
                new[] { "name" },
                new[] { "@Name" },
                new[] { "id" });

            Assert.Contains("ON CONFLICT (id)", result);
            Assert.Contains("DO UPDATE SET", result);
            Assert.Contains("EXCLUDED.name", result);
        }

        [Fact]
        public void GenerateIsNull_UsesCOALESCE()
        {
            // PostgreSQL doesn't have ISNULL, uses COALESCE
            Assert.Equal("COALESCE(col, 0)", _dialect.GenerateIsNull("col", "0"));
        }

        [Fact]
        public void GenerateSubstring_UsesFromFor()
        {
            Assert.Equal("SUBSTRING(col FROM 1 FOR 5)", _dialect.GenerateSubstring("col", "1", "5"));
        }

        [Fact]
        public void GenerateYear_UsesExtract()
        {
            Assert.Equal("EXTRACT(YEAR FROM col)", _dialect.GenerateYear("col"));
        }

        [Fact]
        public void GenerateCaseSensitiveLike_UsesCollateC()
        {
            var result = _dialect.GenerateCaseSensitiveLike("col", "@p", "\\");
            Assert.Contains("COLLATE \"C\"", result);
        }

        [Fact]
        public void GenerateCaseInsensitiveLike_UsesILIKE()
        {
            var result = _dialect.GenerateCaseInsensitiveLike("col", "@p", "\\");
            Assert.Contains("ILIKE", result);
        }

        [Fact]
        public void GenerateCaseInsensitiveEquals_UsesLower()
        {
            var result = _dialect.GenerateCaseInsensitiveEquals("col", "@p");
            Assert.Contains("LOWER(col)", result);
            Assert.Contains("LOWER(@p)", result);
        }

        [Fact]
        public void FormatContainsPattern_UsesPercent()
        {
            Assert.Equal("%test%", _dialect.FormatContainsPattern("test"));
        }
    }

    #endregion

    #region Cross-Dialect Comparisons

    public class CrossDialectTests
    {
        [Fact]
        public void AllDialects_SupportUpsert()
        {
            Assert.True(new SQLiteDialect().SupportsUpsert);
            Assert.True(new SqlServerDialect().SupportsUpsert);
            Assert.True(new MySqlDialect().SupportsUpsert);
            Assert.True(new PostgreSqlDialect().SupportsUpsert);
        }

        [Fact]
        public void AllDialects_GenerateCoalesce()
        {
            ISqlDialect[] dialects = new ISqlDialect[] { new SQLiteDialect(), new SqlServerDialect(), new MySqlDialect(), new PostgreSqlDialect() };

            foreach (var dialect in dialects)
            {
                var result = dialect.GenerateCoalesce("a", "b", "c");
                Assert.Equal("COALESCE(a, b, c)", result);
            }
        }

        [Fact]
        public void AllDialects_GenerateNullIf()
        {
            ISqlDialect[] dialects = new ISqlDialect[] { new SQLiteDialect(), new SqlServerDialect(), new MySqlDialect(), new PostgreSqlDialect() };

            foreach (var dialect in dialects)
            {
                Assert.Equal("NULLIF(a, b)", dialect.GenerateNullIf("a", "b"));
            }
        }

        [Fact]
        public void AllDialects_WindowFunctions()
        {
            ISqlDialect[] dialects = new ISqlDialect[] { new SQLiteDialect(), new SqlServerDialect(), new MySqlDialect(), new PostgreSqlDialect() };

            foreach (var dialect in dialects)
            {
                Assert.Equal("ROW_NUMBER()", dialect.GenerateRowNumber());
                Assert.Equal("RANK()", dialect.GenerateRank());
                Assert.Equal("DENSE_RANK()", dialect.GenerateDenseRank());
                Assert.Equal("NTILE(4)", dialect.GenerateNTile(4));
            }
        }

        [Fact]
        public void ForeignKeyToggle_SqlServerDoesNotSupport()
        {
            Assert.False(new SqlServerDialect().SupportsForeignKeyToggle);
            Assert.Null(new SqlServerDialect().GetDisableForeignKeyChecksSql());
            Assert.Null(new SqlServerDialect().GetEnableForeignKeyChecksSql());
        }

        [Fact]
        public void ForeignKeyToggle_OtherDialectsSupport()
        {
            Assert.True(new SQLiteDialect().SupportsForeignKeyToggle);
            Assert.True(new MySqlDialect().SupportsForeignKeyToggle);
            Assert.True(new PostgreSqlDialect().SupportsForeignKeyToggle);
        }
    }

    #endregion

    #region SqlDialectFactory

    public class SqlDialectFactoryTests
    {
        [Fact]
        public void GetDialect_SQLiteConnection_ReturnsSQLiteDialect()
        {
            var connection = new SQLiteConnection();
            var dialect = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<SQLiteDialect>(dialect);
        }

        [Fact]
        public void GetDialect_SqlConnection_ReturnsSqlServerDialect()
        {
            var connection = new SqlConnection();
            var dialect = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<SqlServerDialect>(dialect);
        }

        [Fact]
        public void GetDialect_NpgsqlConnection_ReturnsPostgreSqlDialect()
        {
            var connection = new NpgsqlConnection();
            var dialect = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<PostgreSqlDialect>(dialect);
        }

        [Fact]
        public void GetDialect_MySqlConnection_ReturnsMySqlDialect()
        {
            var connection = new MySqlConnection();
            var dialect = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<MySqlDialect>(dialect);
        }

        [Fact]
        public void GetDialect_SqliteConnection_MicrosoftDataSqlite_ReturnsSQLiteDialect()
        {
            var connection = new SqliteConnection();
            var dialect = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<SQLiteDialect>(dialect);
        }

        [Fact]
        public void GetDialect_UnknownConnection_DefaultsToSqlServer()
        {
            var connection = new UnknownConnection();
            var dialect = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<SqlServerDialect>(dialect);
        }

        // Mock connection classes whose type names match the factory's switch cases
        private class SQLiteConnection : MockConnectionBase { }
        private class SqliteConnection : MockConnectionBase { } // Microsoft.Data.Sqlite uses this name
        private class SqlConnection : MockConnectionBase { }
        private class NpgsqlConnection : MockConnectionBase { }
        private class MySqlConnection : MockConnectionBase { }
        private class UnknownConnection : MockConnectionBase { }

        private abstract class MockConnectionBase : IDbConnection
        {
            public string ConnectionString { get => ""; set { } }
            public int ConnectionTimeout => 0;
            public string Database => "";
            public ConnectionState State => ConnectionState.Closed;
            public IDbTransaction BeginTransaction() => throw new NotImplementedException();
            public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
            public void ChangeDatabase(string databaseName) { }
            public void Close() { }
            public IDbCommand CreateCommand() => throw new NotImplementedException();
            public void Dispose() { }
            public void Open() { }
        }
    }

    #endregion
}
