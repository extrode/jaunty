using Jaunty.Core;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// CF-9 / AUD-R31-007: the joined counterpart of AUD-R26-060. GroupedJoinedQueryBuilder{,3,4}
/// execute their own command rather than delegating to core, so before these overloads a
/// grouped joined query could not be enlisted in a caller's transaction or given a timeout.
/// Sibling to FluentJoinCommandOptionsTests, which covers the ungrouped join terminals.
///
/// <para>
/// These are round-trip tests only: they prove the options overloads execute and return the same
/// groups, not that the transaction reaches the command. SQLite associates commands with the
/// connection's open transaction implicitly, so a round-trip passes either way - verified by
/// deleting the FluentCommandOptions.Apply call and watching all of these stay green. The
/// assertion that the option actually lands on the command is in FluentCommandOptionsTests,
/// against a capturing IDbCommand.
/// </para>
/// </summary>
public class FluentGroupedJoinCommandOptionsTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupedJoinCommandOptionsTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private int InsertProduct(string name, short categoryId, int supplierId)
        => (int)_fixture.Connection.Into<Product>()
            .Values(new
            {
                ProductName = name,
                SupplierId = supplierId,
                CategoryId = categoryId,
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

    #region 2-way

    [Fact]
    public void TwoWay_Select_WithTransaction_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoinSelect", 1, 1);

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        Assert.Equal(1, results[0].Count);
        tx.Rollback();
    }

    [Fact]
    public async Task TwoWay_SelectAsync_WithTransaction_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoinSelectAsync", 1, 1);

        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .GroupBy((p, c) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    [Fact]
    public void TwoWay_Select_WithTimeout_StillReturnsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    [Fact]
    public void TwoWay_Select_WithDefaultOptions_MatchesTheOverloadWithoutOptions()
    {
        var withoutOptions = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        var withDefault = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, default);

        Assert.Equal(withoutOptions, withDefault);
    }

    [Fact]
    public void TwoWay_Select_WithTransactionAndHaving_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoinHaving", 1, 1);

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 0)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    #endregion

    #region 3-way

    [Fact]
    public void ThreeWay_Select_WithTransaction_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoin3Select", 1, 1);

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        Assert.Equal(1, results[0].Count);
        tx.Rollback();
    }

    [Fact]
    public async Task ThreeWay_SelectAsync_WithTransaction_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoin3SelectAsync", 1, 1);

        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .GroupBy((p, c, s) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    [Fact]
    public void ThreeWay_Select_WithTimeout_StillReturnsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    #endregion

    #region 4-way

    [Fact]
    public void FourWay_Select_WithTransaction_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoin4Select", 1, 1);

        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    [Fact]
    public async Task FourWay_SelectAsync_WithTransaction_ExecutesAndReturnsTheGroup()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxGroupedJoin4SelectAsync", 1, 1);

        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .GroupBy((p, c, s, o) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    [Fact]
    public void FourWay_Select_WithTimeout_StillReturnsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>().On("products.supplier_id", "orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    #endregion
}
