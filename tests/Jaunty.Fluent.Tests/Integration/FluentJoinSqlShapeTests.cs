using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentJoinSqlShapeTests : IClassFixture<FluentDatabaseFixture>
{
    private const string ProductColumns =
        "product_id, {0}product_name, {0}supplier_id, {0}category_id, {0}quantity_per_unit, " +
        "{0}unit_price, {0}units_in_stock, {0}units_on_order, {0}reorder_level, {0}discontinued";

    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinSqlShapeTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private static string Products(string prefix) => prefix + string.Format(ProductColumns, prefix);

    [Fact]
    public void KeySelectorOn_InfersAliasesFromLambdaParameterNames()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.CategoryId == 1)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("p.")} FROM products p " +
            "INNER JOIN categories c ON p.category_id = c.category_id " +
            "WHERE (p.category_id = @p_category_id)",
            sql);
    }

    [Fact]
    public void PredicateOn_InfersAliasesFromLambdaParameterNames()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((prod, cat) => prod.CategoryId == cat.CategoryId)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("prod.")} FROM products prod " +
            "INNER JOIN categories cat ON (prod.category_id = cat.category_id)",
            sql);
    }

    [Fact]
    public void ExplicitAliasesBeatInferredOnes()
    {
        var sql = _fixture.Connection.From<Product>("prd")
            .InnerJoin<Category>("cat")
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("prd.")} FROM products prd " +
            "INNER JOIN categories cat ON prd.category_id = cat.category_id",
            sql);
    }

    [Fact]
    public void OneIdentifierOnBothSides_FallsBackToTableNames()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(x => x.CategoryId, x => x.CategoryId)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("products.")} FROM products " +
            "INNER JOIN categories ON products.category_id = categories.category_id",
            sql);
    }

    [Fact]
    public void NameShadowingATableInTheQuery_FallsBackToTableNames()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(products => products.CategoryId, c => c.CategoryId)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("products.")} FROM products " +
            "INNER JOIN categories ON products.category_id = categories.category_id",
            sql);
    }

    [Fact]
    public void StringOn_DoesNotInfer()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id", "categories.category_id")
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("products.")} FROM products " +
            "INNER JOIN categories ON products.category_id = categories.category_id",
            sql);
    }

    [Fact]
    public void SelfJoin_WithoutUsableNames_TakesThePositionalScheme()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Product>()
            .On(x => x.SupplierId, x => x.SupplierId)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("t1.")} FROM products t1 " +
            "INNER JOIN products t2 ON t1.supplier_id = t2.supplier_id",
            sql);
    }

    [Fact]
    public void SelfJoin_WithDistinctNames_UsesThem()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Product>()
            .On(a => a.SupplierId, b => b.SupplierId)
            .ToSql();

        Assert.Equal(
            $"SELECT {Products("a.")} FROM products a " +
            "INNER JOIN products b ON a.supplier_id = b.supplier_id",
            sql);
    }

    [Fact]
    public void TwoFiltersOnOneColumn_NumberTheSecondParameter()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.CategoryId == 1 || p.CategoryId == 2)
            .ToSql();

        Assert.EndsWith(
            "WHERE ((p.category_id = @p_category_id) OR (p.category_id = @p_category_id_2))",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhereLambdaNames_DoNotOverrideTheOnNames()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .Where((zzz, yyy) => zzz.CategoryId == 1)
            .ToSql();

        Assert.Contains("FROM products p INNER JOIN categories c", sql, StringComparison.Ordinal);
        Assert.EndsWith("WHERE (p.category_id = @p_category_id)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThreeTableJoin_InfersTheThirdAlias()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>()
            .On((p, c, s) => p.SupplierId == s.SupplierId)
            .ToSql();

        Assert.Contains(
            "FROM products p INNER JOIN categories c ON (p.category_id = c.category_id) " +
            "INNER JOIN suppliers s ON (p.supplier_id = s.supplier_id)",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SelectBoth_MapsEachEntityFromItsOwnColumns()
    {
        var rows = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.CategoryId == 1)
            .SelectBoth();

        Assert.NotEmpty(rows);

        foreach ((Product product, Category category) in rows)
        {
            Assert.Equal((short?)1, product.CategoryId);
            Assert.Equal(1, category.CategoryId);

            Assert.NotNull(product.ProductName);
            Assert.NotNull(category.CategoryName);
            Assert.NotEqual(product.ProductName, category.CategoryName);
        }
    }

    [Fact]
    public void AWhereOnAColumnTheOnAlreadyBound_TakesASuffixedName()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId && p.CategoryId > 0)
            .Where((p, c) => p.CategoryId == 1)
            .ToSql();

        Assert.Contains("p.category_id > @p_category_id)", sql, StringComparison.Ordinal);
        Assert.EndsWith("WHERE (p.category_id = @p_category_id_2)", sql, StringComparison.Ordinal);
    }
}
