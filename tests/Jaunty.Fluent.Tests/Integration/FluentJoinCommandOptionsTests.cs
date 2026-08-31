using Jaunty.Core;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for the CommandOptions-accepting overloads added to the join builder family
/// (JoinedQueryBuilder&lt;TFrom,TJoin&gt; via IJoinedQuery, JoinedQuery3Builder&lt;T1,T2,T3&gt;
/// via IJoinedQuery3, JoinedQuery4Builder&lt;T1,T2,T3,T4&gt; via IJoinedQuery4).
/// Round 21 finding: unlike QueryBuilder&lt;T&gt; and SetOperationBuilder&lt;T&gt;
/// (see FluentReadCommandOptionsTests), the join builders had no CommandOptions/transaction
/// support at all on any terminal method.
/// </summary>
public class FluentJoinCommandOptionsTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinCommandOptionsTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private int InsertProduct(string name, short categoryId, int supplierId)
    {
        return (int)_fixture.Connection.Into<Product>()
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
    }

    #region 2-way join (JoinedQueryBuilder<Product, Category>)

    [Fact]
    public void Select_WithCommandOptionsTransaction_SeesUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinSelect", 1, 1);

        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .Select(CommandOptions.WithTransaction(tx));

        Assert.Single(products);
        tx.Rollback();
    }

    [Fact]
    public void SelectFirst_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinSelectFirst", 1, 1);

        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .SelectFirst(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxJoinSelectFirst", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public void SelectFirstOrDefault_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _fixture.Connection.BeginTransaction();

        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -1)
            .SelectFirstOrDefault(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public void SelectSingle_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinSelectSingle", 1, 1);

        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .SelectSingle(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxJoinSelectSingle", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public void SelectSingleOrDefault_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _fixture.Connection.BeginTransaction();

        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -1)
            .SelectSingleOrDefault(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public void Count_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinCount", 1, 1);

        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .Count(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public void LongCount_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinLongCount", 1, 1);

        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .LongCount(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectAsync_WithCommandOptionsTransaction_SeesUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinSelectAsync", 1, 1);

        var products = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .SelectAsync(CommandOptions.WithTransaction(tx));

        Assert.Single(products);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectFirstAsync_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinSelectFirstAsync", 1, 1);

        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .SelectFirstAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxJoinSelectFirstAsync", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectFirstOrDefaultAsync_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _fixture.Connection.BeginTransaction();

        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -1)
            .SelectFirstOrDefaultAsync(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectSingleAsync_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinSelectSingleAsync", 1, 1);

        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .SelectSingleAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxJoinSelectSingleAsync", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectSingleOrDefaultAsync_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _fixture.Connection.BeginTransaction();

        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -1)
            .SelectSingleOrDefaultAsync(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public async Task CountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinCountAsync", 1, 1);

        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .CountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public async Task LongCountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoinLongCountAsync", 1, 1);

        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == insertedId)
            .LongCountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    #endregion

    #region 3-way join (JoinedQuery3Builder<Product, Category, Supplier>)

    [Fact]
    public void ThreeWayJoin_Select_WithCommandOptionsTransaction_SeesUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin3Select", 1, 1);

        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .Select(CommandOptions.WithTransaction(tx));

        Assert.Single(products);
        tx.Rollback();
    }

    [Fact]
    public async Task ThreeWayJoin_SelectAsync_WithCommandOptionsTransaction_SeesUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin3SelectAsync", 1, 1);

        var products = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .SelectAsync(CommandOptions.WithTransaction(tx));

        Assert.Single(products);
        tx.Rollback();
    }

    [Fact]
    public void ThreeWayJoin_Count_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin3Count", 1, 1);

        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .Count(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public void ThreeWayJoin_LongCount_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin3LongCount", 1, 1);

        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .LongCount(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    [Fact]
    public async Task ThreeWayJoin_CountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin3CountAsync", 1, 1);

        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .CountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public async Task ThreeWayJoin_LongCountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin3LongCountAsync", 1, 1);

        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == insertedId)
            .LongCountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    #endregion

    #region 4-way join (JoinedQuery4Builder<Product, Category, Supplier, Order>)

    // The fourth join is pinned to a single, specific order row (order_id = 1) so every
    // matched product joins to exactly one order row, regardless of the inserted product's
    // supplier_id - avoiding the row fan-out that a supplier/employee_id-based ON condition
    // would introduce (see FluentFourTableJoinTests' SelectSingleAsync test for the same
    // fan-out caveat).

    [Fact]
    public void FourWayJoin_Select_WithCommandOptionsTransaction_SeesUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin4Select", 1, 1);

        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id = 1")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .Select(CommandOptions.WithTransaction(tx));

        Assert.Single(products);
        tx.Rollback();
    }

    [Fact]
    public async Task FourWayJoin_SelectAsync_WithCommandOptionsTransaction_SeesUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin4SelectAsync", 1, 1);

        var products = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id = 1")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .SelectAsync(CommandOptions.WithTransaction(tx));

        Assert.Single(products);
        tx.Rollback();
    }

    [Fact]
    public void FourWayJoin_Count_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin4Count", 1, 1);

        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id = 1")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .Count(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public void FourWayJoin_LongCount_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin4LongCount", 1, 1);

        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id = 1")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .LongCount(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    [Fact]
    public async Task FourWayJoin_CountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin4CountAsync", 1, 1);

        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id = 1")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .CountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public async Task FourWayJoin_LongCountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxJoin4LongCountAsync", 1, 1);

        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id = 1")
            .Where((p, c, s, o) => p.ProductId == insertedId)
            .LongCountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    #endregion
}
