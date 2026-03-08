using System.Data;

using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Fluent Write Operations: Delete, Update, Insert.
/// Uses in-memory SQLite for complete data isolation — no shared database is modified.
/// </summary>
public class FluentWriteOperationsTests : IDisposable
{
    private readonly InMemoryDatabase _db;

    public FluentWriteOperationsTests()
    {
        _db = new InMemoryDatabase();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region Delete Tests

    [Fact]
    public void Delete_WithWhereCondition_DeletesMatchingRows()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteProduct",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        var rowsDeleted = _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteProduct")
            .Delete();

        Assert.Equal(1, rowsDeleted);

        var remaining = _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteProduct")
            .Select();
        Assert.Empty(remaining);
    }

    [Fact]
    public void Delete_WithMultipleConditions_DeletesMatchingRows()
    {
        _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestMultiDelete1",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestMultiDelete2",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 19.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        var rowsDeleted = _db.Connection.From<Product>()
            .Where(p => p.ProductName!.StartsWith("TestMultiDelete"))
            .And(p => p.UnitPrice < 15)
            .Delete();

        Assert.Equal(1, rowsDeleted);

        var remaining = _db.Connection.From<Product>()
            .Where(p => p.ProductName!.StartsWith("TestMultiDelete"))
            .Select();
        Assert.Equal(1, remaining.Count);
        Assert.Equal(19.99m, remaining[0].UnitPrice);
    }

    [Fact]
    public async Task DeleteAsync_WithWhereCondition_DeletesMatchingRows()
    {
        _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteAsync",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        var rowsDeleted = await _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteAsync")
            .DeleteAsync();

        Assert.Equal(1, rowsDeleted);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithSetAndWhere_UpdatesMatchingRows()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateProduct",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 999.99m)
            .Where(p => p.ProductId == (int)insertedId)
            .Update();

        Assert.Equal(1, rowsUpdated);

        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(999.99m, updated.UnitPrice);
    }

    [Fact]
    public void Update_WithMultipleSets_UpdatesAllColumns()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateMulti",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 888.88m)
            .Set(p => p.UnitsInStock, (short)999)
            .Where(p => p.ProductId == (int)insertedId)
            .Update();

        Assert.Equal(1, rowsUpdated);

        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(888.88m, updated.UnitPrice);
        Assert.Equal((short)999, updated.UnitsInStock);
    }

    [Fact]
    public void Update_WithAnonymousObject_UpdatesAllProperties()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateAnon",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        var rowsUpdated = _db.Connection.From<Product>()
            .Set(new { UnitPrice = 777.77m, UnitsInStock = (short)777 })
            .Where(p => p.ProductId == (int)insertedId)
            .Update();

        Assert.Equal(1, rowsUpdated);

        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(777.77m, updated.UnitPrice);
        Assert.Equal((short)777, updated.UnitsInStock);
    }

    [Fact]
    public async Task UpdateAsync_WithSetAndWhere_UpdatesMatchingRows()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateAsync",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        var rowsUpdated = await _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 666.66m)
            .Where(p => p.ProductId == (int)insertedId)
            .UpdateAsync();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_ToSql_ReturnsCorrectSql()
    {
        var sql = ((ISetClause<Product>)_db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 100m))
            .ToSql();

        Assert.Contains("UPDATE", sql);
        Assert.Contains("products", sql);
        Assert.Contains("SET", sql);
        Assert.Contains("unit_price", sql);
    }

    #endregion

    #region Insert Tests

    [Fact]
    public void Insert_WithEntity_InsertsAndReturnsId()
    {
        var product = new Product
        {
            ProductName = "TestInsertProduct",
            SupplierId = 1,
            CategoryId = 1,
            QuantityPerUnit = "10 boxes",
            UnitPrice = 19.99m,
            UnitsInStock = 50,
            UnitsOnOrder = 0,
            ReorderLevel = 10,
            Discontinued = false
        };

        var id = _db.Connection.Into<Product>()
            .Values(product)
            .Insert();

        Assert.True(id > 0);

        var inserted = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .SelectFirst();
        Assert.Equal("TestInsertProduct", inserted.ProductName);
    }

    [Fact]
    public void Insert_WithAnonymousObject_InsertsAndReturnsId()
    {
        var id = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestInsertAnon",
                SupplierId = 1,
                CategoryId = 1,
                QuantityPerUnit = "5 boxes",
                UnitPrice = 29.99m,
                UnitsInStock = (short)25,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        Assert.True(id > 0);

        var inserted = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .SelectFirst();
        Assert.Equal("TestInsertAnon", inserted.ProductName);
        Assert.Equal(29.99m, inserted.UnitPrice);
    }

    [Fact]
    public void Insert_WithIndividualValues_InsertsAndReturnsId()
    {
        var id = _db.Connection.Into<Product>()
            .Value(p => p.ProductName, "TestInsertIndividual")
            .Value("supplier_id", 1)
            .Value("category_id", 1)
            .Value(p => p.QuantityPerUnit, "3 boxes")
            .Value(p => p.UnitPrice, 39.99m)
            .Value(p => p.UnitsInStock, (short)15)
            .Value(p => p.UnitsOnOrder, (short)0)
            .Value(p => p.ReorderLevel, (short)3)
            .Value(p => p.Discontinued, false)
            .Insert();

        Assert.True(id > 0);

        var inserted = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .SelectFirst();
        Assert.Equal("TestInsertIndividual", inserted.ProductName);
    }

    [Fact]
    public async Task InsertAsync_WithEntity_InsertsAndReturnsId()
    {
        var product = new Product
        {
            ProductName = "TestInsertAsync",
            SupplierId = 1,
            CategoryId = 1,
            QuantityPerUnit = "8 boxes",
            UnitPrice = 49.99m,
            UnitsInStock = 30,
            UnitsOnOrder = 0,
            ReorderLevel = 8,
            Discontinued = false
        };

        var id = await _db.Connection.Into<Product>()
            .Values(product)
            .InsertAsync();

        Assert.True(id > 0);
    }

    [Fact]
    public void Insert_ToSql_ReturnsCorrectSql()
    {
        var sql = _db.Connection.Into<Product>()
            .Value(p => p.ProductName, "Test")
            .Value(p => p.UnitPrice, 10m)
            .ToSql();

        Assert.Contains("INSERT INTO", sql);
        Assert.Contains("products", sql);
        Assert.Contains("product_name", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains("VALUES", sql);
    }

    #endregion
}