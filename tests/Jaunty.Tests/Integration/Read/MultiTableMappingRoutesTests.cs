using System.Data;

using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Pins every supported way of mapping a multi-table query against one table pair
/// (products JOIN categories), so the routes documented in
/// docs/01-api-reference/multi-entity-mapping.md are covered as a set rather than
/// individually. Added when the obsolete combiner overload was removed; see
/// docs/decisions/2026-08-02-001-remove-obsolete-multi-entity-overloads.md.
/// </summary>
public class MultiTableMappingRoutesTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public MultiTableMappingRoutesTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string JoinSql(DialectInfo dialect)
    {
        string top = dialect.Provider == DialectProvider.SqlServer ? "TOP (5) " : string.Empty;
        string limit = dialect.Provider == DialectProvider.SqlServer ? string.Empty : " LIMIT 5";

        return $@"SELECT {top}
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                p.unit_price AS UnitPrice,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              ORDER BY p.product_id{limit}";
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void FlatDto_MapsColumnsFromBothTables(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<ProductWithCategory> rows = connection.Query<ProductWithCategory>(JoinSql(dialect));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.True(r.ProductId > 0);
            Assert.NotEmpty(r.ProductName);
            Assert.True(r.CategoryId > 0);
            Assert.NotEmpty(r.CategoryName);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Tuple_MapsEachRowToTwoEntities(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductInfo, CategoryInfo)> rows = connection.Query<ProductInfo, CategoryInfo>(JoinSql(dialect));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.True(r.Item1.ProductId > 0);
            Assert.True(r.Item2.CategoryId > 0);
            Assert.Null(r.Item1.Category);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_BuildsANestedGraphWithoutAnIntermediateList(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<ProductInfo> products = [];

        foreach ((ProductInfo product, CategoryInfo category) in
                 connection.QueryStream<ProductInfo, CategoryInfo>(JoinSql(dialect)))
        {
            product.Category = category;
            products.Add(product);
        }

        Assert.Equal(5, products.Count);
        Assert.All(products, p =>
        {
            Assert.NotNull(p.Category);
            Assert.True(p.Category!.CategoryId > 0);
            Assert.NotEmpty(p.Category.CategoryName);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_ProjectsToAnArbitraryShape(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<string> labels = [];

        foreach ((ProductInfo product, CategoryInfo category) in
                 connection.QueryStream<ProductInfo, CategoryInfo>(JoinSql(dialect)))
        {
            labels.Add($"{category.CategoryName}:{product.ProductName}");
        }

        Assert.Equal(5, labels.Count);
        Assert.All(labels, l => Assert.Contains(':', l));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void WithMapper_BuildsANestedGraphFromRawColumns(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<ProductInfo> products = connection.Query(
            JoinSql(dialect),
            CommandOptions<ProductInfo>.WithMapper(r => new ProductInfo
            {
                ProductId = r.GetInt32(0),
                ProductName = r.GetString(1),
                UnitPrice = r.IsDBNull(2) ? null : Convert.ToDecimal(r.GetValue(2)),
                Category = new CategoryInfo { CategoryId = r.GetInt32(3), CategoryName = r.GetString(4) },
            }));

        Assert.Equal(5, products.Count);
        Assert.All(products, p =>
        {
            Assert.True(p.ProductId > 0);
            Assert.NotNull(p.Category);
            Assert.True(p.Category!.CategoryId > 0);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void WithMapper_OverridesOrdinalClaimingOnTheMultiEntityOverload(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductInfo, CategoryInfo)> rows = connection.Query<ProductInfo, CategoryInfo>(
            JoinSql(dialect),
            CommandOptions<(ProductInfo, CategoryInfo)>.WithMapper(r => (
                new ProductInfo { ProductId = r.GetInt32(0), ProductName = r.GetString(1).ToUpperInvariant() },
                new CategoryInfo { CategoryId = r.GetInt32(3), CategoryName = r.GetString(4) })));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Equal(r.Item1.ProductName.ToUpperInvariant(), r.Item1.ProductName));
        Assert.All(rows, r => Assert.Null(r.Item1.UnitPrice));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void AllRoutes_AgreeOnTheSameRows(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);
        string sql = JoinSql(dialect);

        List<int> flat = connection.Query<ProductWithCategory>(sql).ConvertAll(r => r.ProductId);
        List<int> tuple = connection.Query<ProductInfo, CategoryInfo>(sql).ConvertAll(r => r.Item1.ProductId);

        List<int> streamed = [];
        foreach ((ProductInfo product, CategoryInfo _) in connection.QueryStream<ProductInfo, CategoryInfo>(sql))
        {
            streamed.Add(product.ProductId);
        }

        List<int> mapped = connection.Query(
            sql,
            CommandOptions<ProductInfo>.WithMapper(r => new ProductInfo { ProductId = r.GetInt32(0) }))
            .ConvertAll(p => p.ProductId);

        Assert.Equal(5, flat.Count);
        Assert.Equal(flat, tuple);
        Assert.Equal(flat, streamed);
        Assert.Equal(flat, mapped);
    }
}

public class ProductWithCategory
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal? UnitPrice { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
}
