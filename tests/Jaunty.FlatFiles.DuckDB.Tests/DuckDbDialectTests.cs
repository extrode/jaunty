using Jaunty.FlatFiles;
using Jaunty.FlatFiles.DuckDB;

namespace Jaunty.FlatFiles.DuckDB.Tests;

public class DuckDbDialectTests
{
    private readonly DuckDbDialect _dialect = DuckDbDialect.Instance;

    // ==========================================
    // Basic SQL Dialect
    // ==========================================

    [Fact]
    public void GetDefaultSchema_ReturnsMain()
    {
        Assert.Equal("main", _dialect.GetDefaultSchema());
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("select")]
    [InlineData("TABLE")]
    [InlineData("ORDER")]
    [InlineData("USER")]
    public void IsKeyword_ReturnsTrue_ForKeywords(string keyword)
    {
        Assert.True(_dialect.IsKeyword(keyword));
    }

    [Theory]
    [InlineData("ProductName")]
    [InlineData("my_column")]
    [InlineData("foo")]
    public void IsKeyword_ReturnsFalse_ForNonKeywords(string identifier)
    {
        Assert.False(_dialect.IsKeyword(identifier));
    }

    [Fact]
    public void EscapeTableName_QuotesTableName()
    {
        var result = _dialect.EscapeTableName(null, "sales");
        Assert.Equal("\"sales\"", result);
    }

    [Fact]
    public void EscapeTableName_WithSchema_QuotesBoth()
    {
        var result = _dialect.EscapeTableName("main", "sales");
        Assert.Equal("\"main\".\"sales\"", result);
    }

    [Fact]
    public void EscapeColumnName_QuotesColumnName()
    {
        var result = _dialect.EscapeColumnName("ProductName");
        Assert.Equal("\"ProductName\"", result);
    }

    [Fact]
    public void GetLastInsertIdSql_ReturnsReturningClause()
    {
        var result = _dialect.GetLastInsertIdSql("id");
        Assert.Equal("RETURNING id;", result);
    }

    [Fact]
    public void GetLastInsertIdSql_NoColumns_ReturnsReturningStar()
    {
        var result = _dialect.GetLastInsertIdSql();
        Assert.Equal("RETURNING *;", result);
    }

    [Fact]
    public void GetPagingSql_ReturnsLimitOffset()
    {
        var result = _dialect.GetPagingSql("SELECT * FROM sales", 10, 20);
        Assert.Equal("SELECT * FROM sales LIMIT 20 OFFSET 10", result);
    }

    // ==========================================
    // LIKE / Pattern Matching
    // ==========================================

    [Fact]
    public void GenerateCaseSensitiveLike_ReturnsLike()
    {
        var result = _dialect.GenerateCaseSensitiveLike("col", "@p0", "[");
        Assert.Equal("col LIKE @p0 ESCAPE '['", result);
    }

    [Fact]
    public void GenerateCaseInsensitiveLike_ReturnsILike()
    {
        var result = _dialect.GenerateCaseInsensitiveLike("col", "@p0", "[");
        Assert.Equal("col ILIKE @p0 ESCAPE '['", result);
    }

    [Fact]
    public void GenerateCaseInsensitiveEquals_ReturnsLowerComparison()
    {
        var result = _dialect.GenerateCaseInsensitiveEquals("col", "@p0");
        Assert.Equal("LOWER(col) = LOWER(@p0)", result);
    }

    [Fact]
    public void FormatContainsPattern_WrapsWithPercent()
    {
        Assert.Equal("%test%", _dialect.FormatContainsPattern("test"));
    }

    [Fact]
    public void FormatStartsWithPattern_AppendsPercent()
    {
        Assert.Equal("test%", _dialect.FormatStartsWithPattern("test"));
    }

    [Fact]
    public void FormatEndsWithPattern_PrependsPercent()
    {
        Assert.Equal("%test", _dialect.FormatEndsWithPattern("test"));
    }

    // ==========================================
    // FK Toggle
    // ==========================================

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
    public void GetEnableForeignKeyChecksSql_ReturnsNull()
    {
        Assert.Null(_dialect.GetEnableForeignKeyChecksSql());
    }

    // ==========================================
    // Null Functions
    // ==========================================

    [Fact]
    public void GenerateCoalesce_JoinsExpressions()
    {
        var result = _dialect.GenerateCoalesce("a", "b", "c");
        Assert.Equal("COALESCE(a, b, c)", result);
    }

    [Fact]
    public void GenerateIsNull_UsesCoalesce()
    {
        var result = _dialect.GenerateIsNull("col", "0");
        Assert.Equal("COALESCE(col, 0)", result);
    }

    [Fact]
    public void GenerateNullIf_ReturnsNullIf()
    {
        var result = _dialect.GenerateNullIf("col", "0");
        Assert.Equal("NULLIF(col, 0)", result);
    }

    // ==========================================
    // String Functions
    // ==========================================

    [Fact]
    public void GenerateLength_ReturnsLength()
    {
        Assert.Equal("LENGTH(col)", _dialect.GenerateLength("col"));
    }

    [Fact]
    public void GenerateUpper_ReturnsUpper()
    {
        Assert.Equal("UPPER(col)", _dialect.GenerateUpper("col"));
    }

    [Fact]
    public void GenerateLower_ReturnsLower()
    {
        Assert.Equal("LOWER(col)", _dialect.GenerateLower("col"));
    }

    [Fact]
    public void GenerateTrim_ReturnsTrim()
    {
        Assert.Equal("TRIM(col)", _dialect.GenerateTrim("col"));
    }

    [Fact]
    public void GenerateSubstring_ReturnsSubstring()
    {
        Assert.Equal("SUBSTRING(col, 1, 5)", _dialect.GenerateSubstring("col", "1", "5"));
    }

    // ==========================================
    // Date Functions
    // ==========================================

    [Fact]
    public void GenerateYear_ReturnsExtract()
    {
        Assert.Equal("EXTRACT(YEAR FROM col)", _dialect.GenerateYear("col"));
    }

    [Fact]
    public void GenerateMonth_ReturnsExtract()
    {
        Assert.Equal("EXTRACT(MONTH FROM col)", _dialect.GenerateMonth("col"));
    }

    [Fact]
    public void GenerateDay_ReturnsExtract()
    {
        Assert.Equal("EXTRACT(DAY FROM col)", _dialect.GenerateDay("col"));
    }

    // ==========================================
    // Upsert
    // ==========================================

    [Fact]
    public void SupportsUpsert_ReturnsTrue()
    {
        Assert.True(_dialect.SupportsUpsert);
    }

    [Fact]
    public void GenerateUpsertSql_GeneratesOnConflict()
    {
        var result = _dialect.GenerateUpsertSql(
            "\"products\"",
            new[] { "id", "name" },
            new[] { "@p0", "@p1" },
            new[] { "name" },
            new[] { "@p1" },
            new[] { "id" });

        Assert.Contains("INSERT INTO \"products\"", result);
        Assert.Contains("ON CONFLICT (id) DO UPDATE SET", result);
        Assert.Contains("name = EXCLUDED.name", result);
    }

    // ==========================================
    // Multi-Row Insert
    // ==========================================

    [Fact]
    public void SupportsMultiRowInsert_ReturnsTrue()
    {
        Assert.True(_dialect.SupportsMultiRowInsert);
    }

    [Fact]
    public void MaxParametersPerStatement_Returns32768()
    {
        Assert.Equal(32768, _dialect.MaxParametersPerStatement);
    }

    // ==========================================
    // Window Functions
    // ==========================================

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
    public void GenerateOverClause_WithPartitionAndOrder()
    {
        var result = _dialect.GenerateOverClause(
            new[] { "region" },
            new[] { ("revenue", true) });

        Assert.Equal(" OVER (PARTITION BY region ORDER BY revenue DESC)", result);
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
        Assert.Equal("SUM(revenue)", _dialect.GenerateWindowAggregate("SUM", "revenue"));
    }

    [Fact]
    public void GenerateWindowAggregate_NullExpression()
    {
        Assert.Equal("COUNT(*)", _dialect.GenerateWindowAggregate("COUNT", null));
    }

    // ==========================================
    // Bulk Copy
    // ==========================================

    [Fact]
    public void SupportsNativeBulkCopy_ReturnsFalse()
    {
        Assert.False(_dialect.SupportsNativeBulkCopy);
    }

    [Fact]
    public void CreateBulkCopyProvider_ReturnsNull()
    {
        Assert.Null(_dialect.CreateBulkCopyProvider());
    }

    // ==========================================
    // IFlatFileDialect - CREATE VIEW SQL
    // ==========================================

    [Fact]
    public void GenerateCreateViewSql_Csv_GeneratesCorrectSql()
    {
        var source = new CsvFileSource("sales", "/data/sales.csv", typeof(object));

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.StartsWith("CREATE OR REPLACE VIEW \"sales\" AS SELECT * FROM read_csv(", result);
        Assert.Contains("/data/sales.csv", result);
        Assert.Contains("auto_detect = true", result);
    }

    [Fact]
    public void GenerateCreateViewSql_CsvWithOptions_IncludesAllOptions()
    {
        var source = new CsvFileSource("data", "/data/file.csv", typeof(object))
        {
            HasHeader = true,
            Delimiter = ';',
            QuoteChar = '"',
            NullString = "NA",
            SkipRows = 2
        };

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("header = true", result);
        Assert.Contains("delim = ';'", result);
        Assert.Contains("quote = '\"'", result);
        Assert.Contains("nullstr = 'NA'", result);
        Assert.Contains("skip = 2", result);
    }

    [Fact]
    public void GenerateCreateViewSql_Tsv_IncludesTabDelimiter()
    {
        var source = new TsvFileSource("sales", "/data/sales.tsv", typeof(object));

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("read_csv(", result);
        Assert.Contains("delim = '\t'", result);
    }

    [Fact]
    public void GenerateCreateViewSql_Parquet_UsesReadParquet()
    {
        var source = new ParquetFileSource("data", "/data/file.parquet", typeof(object));

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("read_parquet(", result);
        Assert.Contains("/data/file.parquet", result);
    }

    [Fact]
    public void GenerateCreateViewSql_ParquetWithHive_IncludesHiveOption()
    {
        var source = new ParquetFileSource("data", "/data/partitioned/", typeof(object))
        {
            HivePartitioning = true
        };

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("hive_partitioning = true", result);
    }

    [Fact]
    public void GenerateCreateViewSql_Json_UsesReadJsonAuto()
    {
        var source = new JsonFileSource("customers", "/data/customers.json", typeof(object));

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("read_json_auto(", result);
        Assert.Contains("/data/customers.json", result);
    }

    [Fact]
    public void GenerateCreateViewSql_JsonNewlineDelimited_IncludesFormat()
    {
        var source = new JsonFileSource("customers", "/data/customers.ndjson", typeof(object))
        {
            JsonFormat = JsonFileFormat.NewlineDelimited
        };

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("format = 'newline_delimited'", result);
    }

    [Fact]
    public void GenerateCreateViewSql_JsonArray_IncludesFormat()
    {
        var source = new JsonFileSource("customers", "/data/customers.json", typeof(object))
        {
            JsonFormat = JsonFileFormat.Array
        };

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("format = 'array'", result);
    }

    [Fact]
    public void GenerateCreateViewSql_JsonWithMaxDepth_IncludesMaxDepth()
    {
        var source = new JsonFileSource("data", "/data/nested.json", typeof(object))
        {
            MaxDepth = 5
        };

        var result = _dialect.GenerateCreateViewSql(source);

        Assert.Contains("maximum_depth = 5", result);
    }

    // ==========================================
    // IFlatFileDialect - CREATE TABLE AS SQL
    // ==========================================

    [Fact]
    public void GenerateCreateTableAsSql_Csv_GeneratesCorrectSql()
    {
        var source = new CsvFileSource("sales", "/data/sales.csv", typeof(object));

        var result = _dialect.GenerateCreateTableAsSql(source);

        Assert.StartsWith("CREATE OR REPLACE TABLE \"sales\" AS SELECT * FROM read_csv(", result);
    }

    // ==========================================
    // IFlatFileDialect - Promote to Table
    // ==========================================

    [Fact]
    public void GeneratePromoteToTableSql_GeneratesThreeStatements()
    {
        var source = new CsvFileSource("sales", "/data/sales.csv", typeof(object));

        var result = _dialect.GeneratePromoteToTableSql(source);

        Assert.Contains("CREATE TABLE \"sales_tmp\" AS SELECT * FROM \"sales\"", result);
        Assert.Contains("DROP VIEW \"sales\"", result);
        Assert.Contains("ALTER TABLE \"sales_tmp\" RENAME TO \"sales\"", result);
    }

    // ==========================================
    // IFlatFileDialect - COPY TO SQL
    // ==========================================

    [Fact]
    public void GenerateCopyToSql_Csv_GeneratesCorrectSql()
    {
        var result = _dialect.GenerateCopyToSql("sales", "/output/sales.csv", FileFormat.Csv);

        Assert.Contains("COPY \"sales\" TO '/output/sales.csv'", result);
        Assert.Contains("FORMAT CSV", result);
        Assert.Contains("HEADER true", result);
    }

    [Fact]
    public void GenerateCopyToSql_Tsv_IncludesTabDelimiter()
    {
        var result = _dialect.GenerateCopyToSql("sales", "/output/sales.tsv", FileFormat.Tsv);

        Assert.Contains("FORMAT CSV", result);
        Assert.Contains("DELIMITER '\t'", result);
    }

    [Fact]
    public void GenerateCopyToSql_Parquet_GeneratesCorrectSql()
    {
        var result = _dialect.GenerateCopyToSql("sales", "/output/sales.parquet", FileFormat.Parquet);

        Assert.Contains("FORMAT PARQUET", result);
    }

    [Fact]
    public void GenerateCopyToSql_Json_GeneratesCorrectSql()
    {
        var result = _dialect.GenerateCopyToSql("sales", "/output/sales.json", FileFormat.Json);

        Assert.Contains("FORMAT JSON", result);
    }
}
