using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryDictionaryTests : IDisposable
{
    private readonly Database _db;

    public QueryDictionaryTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_DictionaryStringObject_ReturnsAllColumns()
    {
        var results = _db.Connection.Query<Dictionary<string, object>>(
            "SELECT product_id, product_name, unit_price FROM products LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, row =>
        {
            Assert.True(row.ContainsKey("product_id"));
            Assert.True(row.ContainsKey("product_name"));
            Assert.True(row.ContainsKey("unit_price"));
        });
    }

    [Fact]
    public void Query_DictionaryStringObject_ValuesAreCorrectTypes()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = 1");

        Assert.NotNull(result["product_id"]);
        Assert.NotNull(result["product_name"]);
        Assert.IsType<string>(result["product_name"]);
    }

    [Fact]
    public void Query_DictionaryStringObject_HandlesNullValues()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, region FROM products p " +
            "LEFT JOIN suppliers s ON p.supplier_id = s.supplier_id " +
            "WHERE s.region IS NULL LIMIT 1");

        Assert.True(result.ContainsKey("region"));
        Assert.Null(result["region"]);
    }

    [Fact]
    public void Query_DictionaryStringObject_CaseInsensitiveKeys()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        // Keys should be case-insensitive
        Assert.Equal(result["product_id"], result["PRODUCT_ID"]);
        Assert.Equal(result["product_name"], result["Product_Name"]);
    }

    [Fact]
    public void QueryFirst_DictionaryStringObject_ReturnsFirstRow()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.NotNull(result);
        Assert.Equal(1L, Convert.ToInt64(result["product_id"]));
    }

    [Fact]
    public void QueryFirstOrDefault_DictionaryStringObject_ReturnsNullWhenEmpty()
    {
        var result = _db.Connection.QueryFirstOrDefault<Dictionary<string, object>>(
            "SELECT product_id FROM products WHERE product_id = -999");

        Assert.Null(result);
    }

    [Fact]
    public void QuerySingle_DictionaryStringObject_ReturnsSingleRow()
    {
        var result = _db.Connection.QuerySingle<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        Assert.NotNull(result);
        Assert.Equal(1L, Convert.ToInt64(result["product_id"]));
    }

    [Fact]
    public void Query_DictionaryStringObject_WithParameters()
    {
        var results = _db.Connection.Query<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, row => Assert.NotNull(row["product_name"]));
    }

    [Fact]
    public async Task QueryAsync_DictionaryStringObject_Works()
    {
        var results = await _db.Connection.QueryAsync<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void QueryStream_DictionaryStringObject_Streams()
    {
        var results = _db.Connection.QueryStream<Dictionary<string, object>>(
            "SELECT product_id, product_name FROM products LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_DictionaryStringObject_AllColumnTypes()
    {
        // Test various column types
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
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

    [Fact]
    public void Query_DictionaryStringObject_CountQuery()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
            "SELECT COUNT(*) AS total_count FROM products");

        Assert.True(result.ContainsKey("total_count"));
        Assert.True(Convert.ToInt64(result["total_count"]) > 0);
    }

    [Fact]
    public void Query_DictionaryStringObject_AggregateQuery()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, object>>(
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

    [Fact]
    public void Query_DictionaryStringDecimal_ConvertsAllValuesToDecimal()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, decimal>>(
            "SELECT unit_price, units_in_stock FROM products WHERE unit_price IS NOT NULL AND units_in_stock IS NOT NULL LIMIT 1");

        Assert.NotNull(result);
        Assert.True(result.ContainsKey("unit_price"));
        Assert.True(result.ContainsKey("units_in_stock"));
        Assert.IsType<decimal>(result["unit_price"]);
        Assert.IsType<decimal>(result["units_in_stock"]);
    }

    [Fact]
    public void Query_DictionaryStringLong_ConvertsAllValuesToLong()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, long>>(
            "SELECT product_id, category_id, units_in_stock FROM products WHERE units_in_stock IS NOT NULL LIMIT 1");

        Assert.NotNull(result);
        Assert.IsType<long>(result["product_id"]);
        Assert.IsType<long>(result["category_id"]);
        Assert.IsType<long>(result["units_in_stock"]);
    }

    [Fact]
    public void Query_DictionaryStringString_ConvertsAllValuesToString()
    {
        var result = _db.Connection.QueryFirst<Dictionary<string, string>>(
            "SELECT product_name, quantity_per_unit FROM products LIMIT 1");

        Assert.NotNull(result);
        Assert.IsType<string>(result["product_name"]);
        // quantity_per_unit might be null, but if present it should be string
    }

    [Fact]
    public void Query_DictionaryStringInt_ReturnsList()
    {
        var results = _db.Connection.Query<Dictionary<string, int>>(
            "SELECT product_id, category_id FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, row =>
        {
            Assert.IsType<int>(row["product_id"]);
            Assert.IsType<int>(row["category_id"]);
        });
    }

    #endregion
}
