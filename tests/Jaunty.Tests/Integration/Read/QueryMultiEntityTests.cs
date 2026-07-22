using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests for multi-entity mapping (Query&lt;T1, T2&gt;).
/// Uses property-name matching instead of Dapper's splitOn approach.
/// </summary>
public class QueryMultiEntityTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryMultiEntityTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string TopPrefix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? $"TOP ({count}) " : string.Empty;

    private static string LimitSuffix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? string.Empty : $" LIMIT {count}";

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_MapsByPropertyName(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Use explicit aliases to map to each entity's properties
        var results = connection.Query<ProductInfo, CategoryInfo>(
            $@"SELECT {TopPrefix(dialect, 5)}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.unit_price AS UnitPrice,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              {LimitSuffix(dialect, 5)}");

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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [Obsolete]
    public void Query_TwoEntities_WithCombiner_BuildsObjectGraph(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<ProductInfo> results = connection.Query<ProductInfo, CategoryInfo, ProductInfo>(
            $@"SELECT {TopPrefix(dialect, 5)}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.unit_price AS UnitPrice,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              {LimitSuffix(dialect, 5)}",
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirst_TwoEntities_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var (product, category) = connection.QueryFirst<ProductInfo, CategoryInfo>(
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirstOrDefault_TwoEntities_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirstOrDefault<ProductInfo, CategoryInfo>(
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QuerySingle_TwoEntities_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var (product, category) = connection.QuerySingle<ProductInfo, CategoryInfo>(
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QuerySingle_TwoEntities_ThrowsWhenMultiple(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Assert.Throws<InvalidOperationException>(() =>
            connection.QuerySingle<ProductInfo, CategoryInfo>(
                $@"SELECT {TopPrefix(dialect, 5)}
                    p.product_id AS ProductId,
                    p.product_name AS ProductName,
                    c.category_id AS CategoryId,
                    c.category_name AS CategoryName
                  FROM products p
                  JOIN categories c ON p.category_id = c.category_id
                  {LimitSuffix(dialect, 5)}"));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_TwoEntities_StreamsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.QueryStream<ProductInfo, CategoryInfo>(
            $@"SELECT {TopPrefix(dialect, 5)}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              {LimitSuffix(dialect, 5)}").ToList();

        Assert.Equal(5, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_WithParameters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<ProductInfo, CategoryInfo>(
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_TwoEntities_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = await connection.QueryAsync<ProductInfo, CategoryInfo>(
            $@"SELECT {TopPrefix(dialect, 3)}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              {LimitSuffix(dialect, 3)}",
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstAsync_TwoEntities_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var (product, category) = await connection.QueryFirstAsync<ProductInfo, CategoryInfo>(
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstAsync_TwoEntities_WithParameters_FiltersRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var (product, _) = await connection.QueryFirstAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstOrDefaultAsync_TwoEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = await connection.QueryFirstOrDefaultAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = @ProductId",
            new { ProductId = -999 },
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleAsync_TwoEntities_WithParameters_FiltersRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var (product, _) = await connection.QuerySingleAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleOrDefaultAsync_TwoEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = await connection.QuerySingleOrDefaultAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = @ProductId",
            new { ProductId = -999 },
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryStreamAsync_TwoEntities_WithParameters_FiltersRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = new List<(ProductInfo, CategoryInfo)>();
        await foreach (var row in connection.QueryStreamAsync<ProductInfo, CategoryInfo>(
            @"SELECT
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE c.category_id = @CategoryId",
            new { CategoryId = 1 },
            CancellationToken.None))
        {
            results.Add(row);
        }

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(1, r.Item2.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_T1HasPriority_WhenColumnMatchesBoth(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // NamedT1 and NamedT2 both have a "Name" property, but the result set has only one
        // column aliased "Name" - proving T1 claims it (left-to-right ordinal claiming) and T2's
        // Name property is left at its default, rather than both binding the same column.
        var results = connection.Query<NamedT1, NamedT2>(
            $@"SELECT {TopPrefix(dialect, 1)}
                p.product_id AS Id,
                p.product_name AS Name,
                c.category_id AS OtherId
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              {LimitSuffix(dialect, 1)}");

        (NamedT1 t1, NamedT2 t2) = Assert.Single(results);
        Assert.NotEqual(0, t1.Id);
        Assert.False(string.IsNullOrEmpty(t1.Name));
        Assert.NotEqual(0, t2.OtherId);
        Assert.Equal(string.Empty, t2.Name);
    }

    private class NamedT1
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class NamedT2
    {
        public int OtherId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_IgnoresUnmatchedColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Include extra columns that don't match any property - they should be ignored
        var results = connection.Query<ProductInfo, CategoryInfo>(
            $@"SELECT {TopPrefix(dialect, 1)}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.units_in_stock AS SomeExtraColumn,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName,
                c.description AS AnotherExtraColumn
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              {LimitSuffix(dialect, 1)}");

        Assert.Single(results);
        var (product, category) = results[0];
        Assert.True(product.ProductId > 0);
        Assert.True(category.CategoryId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_ThreeTableJoin(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<ProductInfo, SupplierInfo>(
            $@"SELECT {TopPrefix(dialect, 5)}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                s.supplier_id AS SupplierId,
                s.company_name AS CompanyName
              FROM products p
              JOIN suppliers s ON p.supplier_id = s.supplier_id
              {LimitSuffix(dialect, 5)}");

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