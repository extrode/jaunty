using System.Data;

using Jaunty.Core;
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

    [Fact]
    public void Delete_WithCommandOptionsTransaction_CommitsWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteTxCommit",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            var rowsDeleted = _db.Connection.From<Product>()
                .Where(p => p.ProductId == (int)insertedId)
                .Delete(CommandOptions.WithTransaction(tx));
            Assert.Equal(1, rowsDeleted);
            tx.Commit();
        }

        var gone = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirstOrDefault();
        Assert.Null(gone);
    }

    [Fact]
    public void Delete_WithCommandOptionsTransaction_RollsBackWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteTxRollback",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            var rowsDeleted = _db.Connection.From<Product>()
                .Where(p => p.ProductId == (int)insertedId)
                .Delete(CommandOptions.WithTransaction(tx));
            Assert.Equal(1, rowsDeleted);
            tx.Rollback();
        }

        var stillThere = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirstOrDefault();
        Assert.NotNull(stillThere);
    }

    // ExecuteNonQuery (the private helper backing Delete/DeleteAll) assigned options.Transaction
    // to command.Transaction unconditionally. On a real DbConnection that setter casts internally,
    // so a non-DbTransaction IDbTransaction threw an opaque InvalidCastException instead of
    // Jaunty's clear ArgumentException. (AUD-R11)
    [Fact]
    public void Delete_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteNonDbTx",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.From<Product>()
                .Where(p => p.ProductId == (int)insertedId)
                .Delete(CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    // ExecuteNonQueryAsync (the private helper backing DeleteAsync/UpdateAsync) assigned
    // options.Transaction to command.Transaction only when it was already a DbTransaction,
    // with no else branch - a non-DbTransaction IDbTransaction was silently dropped instead of
    // erroring, so the command executed outside the caller's requested transaction with no
    // indication anything was wrong. The sync counterpart (ExecuteNonQuery, see the
    // Delete_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException test
    // above) already threw a clear ArgumentException for this case; ExecuteNonQueryAsync now
    // matches. (AUD-R13)
    [Fact]
    public async Task DeleteAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteAsyncNonDbTx",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _db.Connection.From<Product>()
                .Where(p => p.ProductId == (int)insertedId)
                .DeleteAsync(CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public async Task DeleteAsync_WithCommandOptionsTransaction_RollsBackWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteAsyncTxRollback",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            var rowsDeleted = await _db.Connection.From<Product>()
                .Where(p => p.ProductId == (int)insertedId)
                .DeleteAsync(CommandOptions.WithTransaction(tx));
            Assert.Equal(1, rowsDeleted);
            tx.Rollback();
        }

        var stillThere = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirstOrDefault();
        Assert.NotNull(stillThere);
    }

    [Fact]
    public void DeleteAll_WithCommandOptionsTransaction_RollsBackWithTransaction()
    {
        _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestDeleteAllTxRollback",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            _db.Connection.From<Product>()
                .DeleteAll(CommandOptions.WithTransaction(tx));
            tx.Rollback();
        }

        var stillThere = _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteAllTxRollback")
            .SelectFirstOrDefault();
        Assert.NotNull(stillThere);
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
    public void Update_WithCommandOptionsTransaction_CommitsWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateTxCommit",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            var rowsUpdated = _db.Connection.From<Product>()
                .Set(p => p.UnitPrice, 123.45m)
                .Where(p => p.ProductId == (int)insertedId)
                .Update(CommandOptions.WithTransaction(tx));
            Assert.Equal(1, rowsUpdated);
            tx.Commit();
        }

        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(123.45m, updated.UnitPrice);
    }

    [Fact]
    public void Update_WithCommandOptionsTransaction_RollsBackWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateTxRollback",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            var rowsUpdated = _db.Connection.From<Product>()
                .Set(p => p.UnitPrice, 543.21m)
                .Where(p => p.ProductId == (int)insertedId)
                .Update(CommandOptions.WithTransaction(tx));
            Assert.Equal(1, rowsUpdated);
            tx.Rollback();
        }

        var unchanged = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(10.00m, unchanged.UnitPrice);
    }

    [Fact]
    public async Task UpdateAsync_WithCommandOptionsTransaction_RollsBackWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateAsyncTxRollback",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            var rowsUpdated = await _db.Connection.From<Product>()
                .Set(p => p.UnitPrice, 999.11m)
                .Where(p => p.ProductId == (int)insertedId)
                .UpdateAsync(CommandOptions.WithTransaction(tx));
            Assert.Equal(1, rowsUpdated);
            tx.Rollback();
        }

        var unchanged = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(10.00m, unchanged.UnitPrice);
    }

    [Fact]
    public void UpdateAll_WithCommandOptionsTransaction_RollsBackWithTransaction()
    {
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateAllTxRollback",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        using (var tx = _db.Connection.BeginTransaction())
        {
            _db.Connection.From<Product>()
                .Set(p => p.UnitPrice, 1.23m)
                .UpdateAll(CommandOptions.WithTransaction(tx));
            tx.Rollback();
        }

        var unchanged = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(10.00m, unchanged.UnitPrice);
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

    [Fact]
    public void Update_SetThenWhereStringColumn_UpdatesCorrectRow()
    {
        // Regression test: ISetClause<T>.Where(string, object?) used to build its parameter
        // name as a bare "@column" (identical to what WhereExpressionVisitor's first-occurrence
        // naming produces), rather than the suffixed name every other Where-family overload
        // uses via GetUniqueParamName. Left uncaught, a bare name here could collide with
        // another bare-named parameter added to the same builder instance and throw
        // ArgumentException("A parameter named ... has already been added.") from
        // ParameterCollection.Add instead of executing the update.
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = "TestUpdateStringWhere",
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        var rowsUpdated = ((ISetClause<Product>)_db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 555.55m))
            .Where("product_id", (int)insertedId)
            .Update();

        Assert.Equal(1, rowsUpdated);

        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        Assert.Equal(555.55m, updated.UnitPrice);
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