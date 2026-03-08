using Jaunty;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryDictionaryTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryDictionaryTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_ReturnsAllColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<Dictionary<string, object>>(
            dialect.SelectTop("product_id, product_name, unit_price", "FROM products", 5));

        Assert.Equal(5, results.Count);
        Assert.All(results, row =>
        {
            Assert.True(row.ContainsKey("product_id"));
            Assert.True(row.ContainsKey("product_name"));
            Assert.True(row.ContainsKey("unit_price"));
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_ValuesAreCorrectTypes(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = 1");

        Assert.NotNull(result["product_id"]);
        Assert.NotNull(result["product_name"]);
        Assert.IsType<string>(result["product_name"]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_HandlesNullValues(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, object>>(
            dialect.SelectTop(
                "product_id, region",
                "FROM products p LEFT JOIN suppliers s ON p.supplier_id = s.supplier_id WHERE s.region IS NULL",
                1));

        Assert.True(result.ContainsKey("region"));
        Assert.Null(result["region"]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_CaseInsensitiveKeys(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        // Keys should be case-insensitive
        Assert.Equal(result["product_id"], result["PRODUCT_ID"]);
        Assert.Equal(result["product_name"], result["Product_Name"]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirst_DictionaryStringObject_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.NotNull(result);
        Assert.Equal(1L, Convert.ToInt64(result["product_id"]));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirstOrDefault_DictionaryStringObject_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirstOrDefault<Dictionary<string, object>>(
            "SELECT product_id FROM products WHERE product_id = -999");

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QuerySingle_DictionaryStringObject_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QuerySingle<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        Assert.NotNull(result);
        Assert.Equal(1L, Convert.ToInt64(result["product_id"]));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_WithParameters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, row => Assert.NotNull(row["product_name"]));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_DictionaryStringObject_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = await connection.QueryAsync<Dictionary<string, object>>(
            dialect.SelectTop("product_id, product_name", "FROM products", 3));

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_DictionaryStringObject_Streams(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.QueryStream<Dictionary<string, object>>(
            dialect.SelectTop("product_id, product_name", "FROM products", 5)).ToList();

        Assert.Equal(5, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_AllColumnTypes(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Test various column types
        var result = connection.QueryFirst<Dictionary<string, object>>(
            @"SELECT
                product_id,
                product_name,
                unit_price,
                units_in_stock,
                discontinued
              FROM products
              WHERE product_id = 1");

        Assert.NotNull(result["product_id"]);
        Assert.NotNull(result["product_name"]);
        // unit_price, units_in_stock may be null depending on data
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_CountQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, object>>(
            "SELECT COUNT(*) AS total_count FROM products");

        Assert.True(result.ContainsKey("total_count"));
        Assert.True(Convert.ToInt64(result["total_count"]) > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_AggregateQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, object>>(
            @"SELECT
                category_id,
                COUNT(*) AS product_count,
                AVG(unit_price) AS avg_price
              FROM products
              WHERE category_id = 1
              GROUP BY category_id");

        Assert.True(result.ContainsKey("category_id"));
        Assert.True(result.ContainsKey("product_count"));
        Assert.True(result.ContainsKey("avg_price"));
    }

    #region Typed Dictionary Tests - Dictionary<string, TValue>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringDecimal_ConvertsAllValuesToDecimal(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, decimal>>(
            dialect.SelectTop(
                "unit_price, units_in_stock",
                "FROM products WHERE unit_price IS NOT NULL AND units_in_stock IS NOT NULL",
                1));

        Assert.NotNull(result);
        Assert.True(result.ContainsKey("unit_price"));
        Assert.True(result.ContainsKey("units_in_stock"));
        Assert.IsType<decimal>(result["unit_price"]);
        Assert.IsType<decimal>(result["units_in_stock"]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringLong_ConvertsAllValuesToLong(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, long>>(
            dialect.SelectTop(
                "product_id, category_id, units_in_stock",
                "FROM products WHERE units_in_stock IS NOT NULL",
                1));

        Assert.NotNull(result);
        Assert.IsType<long>(result["product_id"]);
        Assert.IsType<long>(result["category_id"]);
        Assert.IsType<long>(result["units_in_stock"]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringString_ConvertsAllValuesToString(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<Dictionary<string, string>>(
            dialect.SelectTop("product_name, quantity_per_unit", "FROM products", 1));

        Assert.NotNull(result);
        Assert.IsType<string>(result["product_name"]);
        // quantity_per_unit might be null, but if present it should be string
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringInt_ReturnsList(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<Dictionary<string, int>>(
            dialect.SelectTop("product_id, category_id", "FROM products", 3));

        Assert.Equal(3, results.Count);
        Assert.All(results, row =>
        {
            Assert.IsType<int>(row["product_id"]);
            Assert.IsType<int>(row["category_id"]);
        });
    }

    #endregion
}