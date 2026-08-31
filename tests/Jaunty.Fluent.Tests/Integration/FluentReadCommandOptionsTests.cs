using System.Data;

using Jaunty.Core;
using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for the CommandOptions-accepting overloads on terminal read methods
/// (QueryBuilder&lt;T&gt; via IQueryTerminal&lt;T&gt;, and SetOperationBuilder&lt;T&gt;
/// via ISetOperationClause&lt;T&gt;/ISetOperationOrderByClause&lt;T&gt;).
/// Uses in-memory SQLite for complete data isolation.
/// </summary>
public class FluentReadCommandOptionsTests : IDisposable
{
    private readonly InMemoryDatabase _db;

    public FluentReadCommandOptionsTests()
    {
        _db = new InMemoryDatabase();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private int InsertProduct(string name, short categoryId)
    {
        return (int)_db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = name,
                SupplierId = 1,
                CategoryId = categoryId,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();
    }

    #region QueryBuilder — Select family

    [Fact]
    public void Select_WithCommandOptionsTransaction_SeesUncommittedRowWithinTransaction()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxSelectVisible", 1);

        var results = _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .Select(CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    [Fact]
    public void SelectFirst_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxSelectFirst", 1);

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .SelectFirst(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxSelectFirst", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public void SelectFirstOrDefault_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .SelectFirstOrDefault(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public void SelectSingle_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxSelectSingle", 1);

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .SelectSingle(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxSelectSingle", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public void SelectSingleOrDefault_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .SelectSingleOrDefault(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public void Count_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxCount", 1);

        var count = _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .Count(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public void LongCount_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxLongCount", 1);

        var count = _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .LongCount(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    #endregion

    #region QueryBuilder — Async Select family

    [Fact]
    public async Task SelectAsync_WithCommandOptionsTransaction_SeesUncommittedRowWithinTransaction()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxSelectAsync", 1);

        var results = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .SelectAsync(CommandOptions.WithTransaction(tx));

        Assert.Single(results);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectFirstAsync_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxSelectFirstAsync", 1);

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .SelectFirstAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxSelectFirstAsync", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectFirstOrDefaultAsync_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .SelectFirstOrDefaultAsync(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectSingleAsync_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxSelectSingleAsync", 1);

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .SelectSingleAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxSelectSingleAsync", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public async Task SelectSingleOrDefaultAsync_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .SelectSingleOrDefaultAsync(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public async Task CountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxCountAsync", 1);

        var count = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .CountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1, count);
        tx.Rollback();
    }

    [Fact]
    public async Task LongCountAsync_WithCommandOptionsTransaction_CountsUncommittedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var insertedId = InsertProduct("TxLongCountAsync", 1);

        var count = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == insertedId)
            .LongCountAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(1L, count);
        tx.Rollback();
    }

    #endregion

    #region SetOperationBuilder — Select family

    [Fact]
    public void Union_Select_WithCommandOptionsTransaction_SeesUncommittedRows()
    {
        using var tx = _db.Connection.BeginTransaction();
        var idA = InsertProduct("TxUnionA", 1);
        var idB = InsertProduct("TxUnionB", 2);

        var results = _db.Connection.From<Product>()
            .Where(p => p.ProductId == idA)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == idB))
            .Select(CommandOptions.WithTransaction(tx));

        Assert.Equal(2, results.Count);
        tx.Rollback();
    }

    [Fact]
    public void Union_SelectFirst_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var idA = InsertProduct("TxUnionFirstA", 1);
        var idB = InsertProduct("TxUnionFirstB", 2);

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == idA)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == idB))
            .SelectFirst(CommandOptions.WithTransaction(tx));

        Assert.NotNull(product);
        tx.Rollback();
    }

    [Fact]
    public void Union_SelectFirstOrDefault_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == -2))
            .SelectFirstOrDefault(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public void Union_SelectSingle_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var idA = InsertProduct("TxUnionSingleA", 1);

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == idA)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == -2))
            .SelectSingle(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxUnionSingleA", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public void Union_SelectSingleOrDefault_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == -2))
            .SelectSingleOrDefault(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    #endregion

    #region SetOperationBuilder — Async Select family

    [Fact]
    public async Task Union_SelectAsync_WithCommandOptionsTransaction_SeesUncommittedRows()
    {
        using var tx = _db.Connection.BeginTransaction();
        var idA = InsertProduct("TxUnionAsyncA", 1);
        var idB = InsertProduct("TxUnionAsyncB", 2);

        var results = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == idA)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == idB))
            .SelectAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal(2, results.Count);
        tx.Rollback();
    }

    [Fact]
    public async Task Union_SelectFirstAsync_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var idA = InsertProduct("TxUnionFirstAsyncA", 1);
        var idB = InsertProduct("TxUnionFirstAsyncB", 2);

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == idA)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == idB))
            .SelectFirstAsync(CommandOptions.WithTransaction(tx));

        Assert.NotNull(product);
        tx.Rollback();
    }

    [Fact]
    public async Task Union_SelectFirstOrDefaultAsync_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == -2))
            .SelectFirstOrDefaultAsync(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    [Fact]
    public async Task Union_SelectSingleAsync_WithCommandOptionsTransaction_ReturnsInsertedRow()
    {
        using var tx = _db.Connection.BeginTransaction();
        var idA = InsertProduct("TxUnionSingleAsyncA", 1);

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == idA)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == -2))
            .SelectSingleAsync(CommandOptions.WithTransaction(tx));

        Assert.Equal("TxUnionSingleAsyncA", product.ProductName);
        tx.Rollback();
    }

    [Fact]
    public async Task Union_SelectSingleOrDefaultAsync_WithCommandOptionsTransaction_ReturnsNullWhenNoMatch()
    {
        using var tx = _db.Connection.BeginTransaction();

        var product = await _db.Connection.From<Product>()
            .Where(p => p.ProductId == -1)
            .Union(_db.Connection.From<Product>().Where(p => p.ProductId == -2))
            .SelectSingleOrDefaultAsync(CommandOptions.WithTransaction(tx));

        Assert.Null(product);
        tx.Rollback();
    }

    #endregion
}
