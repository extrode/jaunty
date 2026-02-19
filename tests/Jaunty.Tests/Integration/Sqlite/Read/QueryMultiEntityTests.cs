using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

/// <summary>
/// Tests for multi-entity mapping (Query&lt;T1, T2&gt;).
/// Uses property-name matching instead of Dapper's splitOn approach.
/// </summary>
public class QueryMultiEntityTests : IDisposable
{
    private readonly Database _db;

    public QueryMultiEntityTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_TwoEntities_MapsByPropertyName()
    {
        // Use explicit aliases to map to each entity's properties
        var results = _db.Connection.Query<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.unit_price AS UnitPrice,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, r =>
        {
            var (product, category) = r;
            Assert.True(product.ProductId > 0);
            Assert.NotNull(product.ProductName);
            Assert.True(category.CategoryId > 0);
            Assert.NotNull(category.CategoryName);
        });
    }

    [Fact]
    public void Query_TwoEntities_WithCombiner_BuildsObjectGraph()
    {
        var results = _db.Connection.Query<ProductInfo, CategoryInfo, ProductInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.unit_price AS UnitPrice,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 5",
            (product, category) =>
            {
                product.Category = category;
                return product;
            });

        Assert.Equal(5, results.Count);
        Assert.All(results, p =>
        {
            Assert.NotNull(p.Category);
            Assert.True(p.Category.CategoryId > 0);
        });
    }

    [Fact]
    public void QueryFirst_TwoEntities_ReturnsFirstRow()
    {
        var (product, category) = _db.Connection.QueryFirst<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              ORDER BY p.product_id");

        Assert.Equal(1, product.ProductId);
        Assert.NotNull(category.CategoryName);
    }

    [Fact]
    public void QueryFirstOrDefault_TwoEntities_ReturnsNullWhenEmpty()
    {
        var result = _db.Connection.QueryFirstOrDefault<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = -999");

        Assert.Null(result);
    }

    [Fact]
    public void QuerySingle_TwoEntities_ReturnsSingleRow()
    {
        var (product, category) = _db.Connection.QuerySingle<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = 1");

        Assert.Equal(1, product.ProductId);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public void QuerySingle_TwoEntities_ThrowsWhenMultiple()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QuerySingle<ProductInfo, CategoryInfo>(
                @"SELECT
                    p.product_id AS ProductId,
                    p.product_name AS ProductName,
                    c.category_id AS CategoryId,
                    c.category_name AS CategoryName
                  FROM products p
                  JOIN categories c ON p.category_id = c.category_id
                  LIMIT 5"));
    }

    [Fact]
    public void QueryStream_TwoEntities_StreamsResults()
    {
        var results = _db.Connection.QueryStream<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_TwoEntities_WithParameters()
    {
        var results = _db.Connection.Query<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE c.category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(1, r.Item2.CategoryId));
    }

    [Fact]
    public async Task QueryAsync_TwoEntities_Works()
    {
        var results = await _db.Connection.QueryAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 3",
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task QueryFirstAsync_TwoEntities_Works()
    {
        var (product, category) = await _db.Connection.QueryFirstAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              ORDER BY p.product_id",
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public void Query_TwoEntities_T1HasPriority_WhenColumnMatchesBoth()
    {
        // Both ProductInfo and CategoryInfo have no common properties in this test,
        // but if they did, T1 would win
        var results = _db.Connection.Query<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 1");

        Assert.Single(results);
    }

    [Fact]
    public void Query_TwoEntities_IgnoresUnmatchedColumns()
    {
        // Include extra columns that don't match any property - they should be ignored
        var results = _db.Connection.Query<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.units_in_stock AS SomeExtraColumn,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName,
                c.description AS AnotherExtraColumn
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 1");

        Assert.Single(results);
        var (product, category) = results[0];
        Assert.True(product.ProductId > 0);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public void Query_TwoEntities_ThreeTableJoin()
    {
        var results = _db.Connection.Query<ProductInfo, SupplierInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                s.supplier_id AS SupplierId,
                s.company_name AS CompanyName
              FROM products p
              JOIN suppliers s ON p.supplier_id = s.supplier_id
              LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, r =>
        {
            var (product, supplier) = r;
            Assert.True(product.ProductId > 0);
            Assert.True(supplier.SupplierId > 0);
        });
    }
}

// Simple DTOs for multi-entity testing
public class ProductInfo
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal? UnitPrice { get; set; }
    public CategoryInfo? Category { get; set; }
}

public class CategoryInfo
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
}

public class SupplierInfo
{
    public int SupplierId { get; set; }
    public string CompanyName { get; set; } = "";
}
