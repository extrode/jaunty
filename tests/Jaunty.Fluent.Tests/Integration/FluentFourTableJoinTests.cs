using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Jaunty.Fluent;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for 4-table join functionality (JoinedQuery4Builder and JoinClause4Builder).
/// Exercises all On() overloads, Where, OrderBy/ThenBy, Select, SelectAll,
/// Count, LongCount, SelectFirst, SelectFirstOrDefault, SelectPartial, and ToSql.
///
/// Schema: Product → Category (category_id), Product → Supplier (supplier_id).
/// The fourth join (Order) uses a raw-string ON clause because the Northwind-style
/// test schema has no FK between orders and products/categories/suppliers.
/// </summary>
public class FluentFourTableJoinTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentFourTableJoinTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------
    // ToSql shape — basic join count / join type assertions
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_ToSql_ContainsThreeJoins()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("products.supplier_id", "orders.employee_id")
            .ToSql();

        Assert.Contains("SELECT", sql);
        Assert.Contains("FROM", sql);
        int joinCount = sql.Split("INNER JOIN", StringSplitOptions.None).Length - 1;
        Assert.Equal(3, joinCount);
    }

    [Fact]
    public void FourTableJoin_LeftJoinFourth_ToSql_MixesInnerAndLeft()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("products.supplier_id", "orders.employee_id")
            .ToSql();

        Assert.Contains("INNER JOIN", sql);
        Assert.Contains("LEFT JOIN", sql);
    }

    [Fact]
    public void FourTableJoin_WithAliases_ToSql_IncludesAliasedColumns()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On("s.supplier_id", "o.employee_id")
            .ToSql();

        Assert.Contains("p.", sql);
        Assert.Contains("c.", sql);
        Assert.Contains("s.", sql);
        Assert.Contains("o.", sql);
    }

    // ------------------------------------------------------------------
    // JoinClause4Builder: On() overloads
    // ------------------------------------------------------------------

    [Fact]
    public void JoinClause4Builder_OnFromFirst_ToSql_UsesT1AndT4Columns()
    {
        // On<TLeftKey, TRightKey>(T1 → T4): Product.SupplierId (int?) joined to Order.EmployeeId (int?)
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On<int?, int?>(p => p.SupplierId, o => o.EmployeeId)
            .ToSql();

        Assert.Contains("supplier_id", sql);
        Assert.Contains("employee_id", sql);
    }

    [Fact]
    public void JoinClause4Builder_OnFromSecond_ToSql_UsesT2AndT4Columns()
    {
        // OnFromSecond<TLeftKey, TRightKey>(T2 → T4): Category.CategoryId (int) joined to Order.EmployeeId (int?)
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .OnFromSecond<int, int?>(c => c.CategoryId, o => o.EmployeeId)
            .ToSql();

        Assert.Contains("category_id", sql);
        Assert.Contains("employee_id", sql);
    }

    [Fact]
    public void JoinClause4Builder_OnFromThird_ToSql_UsesT3AndT4Columns()
    {
        // OnFromThird<TLeftKey, TRightKey>(T3 → T4): Supplier.SupplierId (int) joined to Order.EmployeeId (int?)
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .OnFromThird<int, int?>(s => s.SupplierId, o => o.EmployeeId)
            .ToSql();

        Assert.Contains("supplier_id", sql);
        Assert.Contains("employee_id", sql);
    }

    [Fact]
    public void JoinClause4Builder_OnTwoStrings_ProducesEqualityCondition()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("products.supplier_id", "orders.employee_id")
            .ToSql();

        Assert.Contains("products.supplier_id = orders.employee_id", sql);
    }

    [Fact]
    public void JoinClause4Builder_OnSingleString_PassesThroughCondition()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .ToSql();

        Assert.Contains("orders.order_id > 0", sql);
    }

    // ------------------------------------------------------------------
    // Select / SelectAll execution
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_Select_ReturnsPrimaryEntityList()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .Select();

        Assert.NotNull(results);
        Assert.IsType<List<Product>>(results);
    }

    [Fact]
    public void FourTableJoin_SelectAll_ReturnsFourTupleList()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .SelectAll();

        Assert.NotNull(results);
        Assert.IsType<List<(Product, Category, Supplier, Order)>>(results);
    }

    [Fact]
    public void FourTableJoin_SelectAll_EachTupleHasNonNullT1()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .SelectAll();

        Assert.NotEmpty(results);
        Assert.All(results, t => Assert.NotNull(t.Item1));
    }

    // ------------------------------------------------------------------
    // Where clause
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_WhereStringCondition_ToSql_ContainsWhereClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .Where("products.unit_price > 10")
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("unit_price", sql);
    }

    [Fact]
    public void FourTableJoin_WhereStringCondition_FiltersResults()
    {
        var all = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .Select();

        var filtered = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .Where("products.unit_price > 100")
            .Select();

        Assert.True(filtered.Count <= all.Count);
    }

    // ------------------------------------------------------------------
    // OrderBy / ThenBy
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_OrderBy_ToSql_ContainsOrderByClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .OrderBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void FourTableJoin_OrderByDescending_ToSql_ContainsDesc()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .OrderByDescending(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void FourTableJoin_OrderByJoined_T4_ToSql_ContainsT4Column()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .OrderByJoined(o => o.OrderId)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("order_id", sql);
    }

    [Fact]
    public void FourTableJoin_ThenBy_ToSql_ContainsMultipleOrderCriteria()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("product_name", sql);
    }

    // ------------------------------------------------------------------
    // Count / LongCount
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_Count_ReturnsNonNegativeInteger()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .Count();

        // Every product has a category/supplier (inner joins) and the left join's near-tautological
        // "order_id > 0" condition matches every order row, so the joined result set is large;
        // a broken join (e.g. one that dropped a table or miscounted) would return 0 or throw.
        Assert.True(count > 0);
    }

    [Fact]
    public void FourTableJoin_LongCount_ReturnsNonNegativeLong()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .LongCount();

        // Same reasoning as FourTableJoin_Count_ReturnsNonNegativeInteger - the joined result
        // set is large, so a broken join returning 0 must be caught, not just "not negative".
        Assert.True(count > 0L);
    }

    // ------------------------------------------------------------------
    // SelectFirst / SelectFirstOrDefault
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_SelectFirst_ReturnsProductWithId()
    {
        var first = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .SelectFirst();

        Assert.NotNull(first);
        Assert.True(first.ProductId > 0);
    }

    [Fact]
    public void FourTableJoin_SelectFirstOrDefault_NoMatch_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0")
            .Where("products.product_name = 'this-product-does-not-exist-xyz'")
            .SelectFirstOrDefault();

        Assert.Null(result);
    }

    // ------------------------------------------------------------------
    // SelectPartial
    // ------------------------------------------------------------------

    [Fact]
    public void FourTableJoin_SelectPartial_ReturnsProjectedRows()
    {
        var results = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On("o.order_id > 0")
            .SelectPartial("p.product_name, c.category_name");

        Assert.NotNull(results);
        Assert.All(results, row => Assert.True(row.Count > 0));
    }
}
