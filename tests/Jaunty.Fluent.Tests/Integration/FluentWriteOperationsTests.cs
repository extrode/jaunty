using System.Data;
using FluentAssertions;
using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Fluent Write Operations: Delete, Update, Insert.
/// </summary>
public class FluentWriteOperationsTests : IDisposable
{
    private readonly Database _db;
    private readonly string _testId;

    public FluentWriteOperationsTests()
    {
        _db = new Database();
        _testId = Guid.NewGuid().ToString("N")[..8]; // Short unique ID for this test run
        CleanupTestData();
    }

    public void Dispose()
    {
        CleanupTestData();
        _db.Dispose();
    }

    private void CleanupTestData()
    {
        // Clean up any test data from this test class
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM products WHERE product_name LIKE 'Test%'";
        _db.Connection.Open();
        cmd.ExecuteNonQuery();
        _db.Connection.Close();
    }

    #region Delete Tests

    [Fact]
    public void Delete_WithWhereCondition_DeletesMatchingRows()
    {
        // Arrange - Insert a test product to delete
        var sql = "INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, discontinued) VALUES ('TestDeleteProduct', 1, 1, '10 boxes', 9.99, 10, 0)";
        using (var cmd = _db.Connection.CreateCommand())
        {
            cmd.CommandText = sql;
            _db.Connection.Open();
            cmd.ExecuteNonQuery();
            _db.Connection.Close();
        }

        // Act
        var rowsDeleted = _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteProduct")
            .Delete();

        // Assert
        rowsDeleted.Should().Be(1);

        // Verify it's gone
        var remaining = _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteProduct")
            .Select();
        remaining.Should().BeEmpty();
    }

    // Note: Delete() without WHERE is compile-time prevented (Delete is on IWhereClause, not IFromClause)
    // Similarly, DeleteAll() is available on IFromClause for explicit "delete all" operations

    [Fact]
    public void Delete_WithMultipleConditions_DeletesMatchingRows()
    {
        // Arrange - Insert test products
        var sql = @"
            INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, discontinued)
            VALUES ('TestMultiDelete1', 1, 1, '10 boxes', 9.99, 10, 0);
            INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, discontinued)
            VALUES ('TestMultiDelete2', 1, 1, '10 boxes', 19.99, 10, 0);
        ";
        using (var cmd = _db.Connection.CreateCommand())
        {
            cmd.CommandText = sql;
            _db.Connection.Open();
            cmd.ExecuteNonQuery();
            _db.Connection.Close();
        }

        // Act - Delete only products with price < 15
        var rowsDeleted = _db.Connection.From<Product>()
            .Where(p => p.ProductName!.StartsWith("TestMultiDelete"))
            .And(p => p.UnitPrice < 15)
            .Delete();

        // Assert
        rowsDeleted.Should().Be(1);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductName!.StartsWith("TestMultiDelete"))
            .Delete();
    }

    [Fact]
    public async Task DeleteAsync_WithWhereCondition_DeletesMatchingRows()
    {
        // Arrange
        var sql = "INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, discontinued) VALUES ('TestDeleteAsync', 1, 1, '10 boxes', 9.99, 10, 0)";
        using (var cmd = _db.Connection.CreateCommand())
        {
            cmd.CommandText = sql;
            _db.Connection.Open();
            cmd.ExecuteNonQuery();
            _db.Connection.Close();
        }

        // Act
        var rowsDeleted = await _db.Connection.From<Product>()
            .Where(p => p.ProductName == "TestDeleteAsync")
            .DeleteAsync();

        // Assert
        rowsDeleted.Should().Be(1);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithSetAndWhere_UpdatesMatchingRows()
    {
        // Arrange - Insert a test product to update
        var testName = $"TestUpdateProduct_{_testId}";
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = testName,
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        // Act
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 999.99m)
            .Where(p => p.ProductId == (int)insertedId)
            .Update();

        // Assert
        rowsUpdated.Should().Be(1);

        // Verify
        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        updated.UnitPrice.Should().Be(999.99m);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .Delete();
    }

    [Fact]
    public void Update_WithMultipleSets_UpdatesAllColumns()
    {
        // Arrange - Insert a test product
        var testName = $"TestUpdateMulti_{_testId}";
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = testName,
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        // Act
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 888.88m)
            .Set(p => p.UnitsInStock, (short)999)
            .Where(p => p.ProductId == (int)insertedId)
            .Update();

        // Assert
        rowsUpdated.Should().Be(1);

        // Verify
        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        updated.UnitPrice.Should().Be(888.88m);
        updated.UnitsInStock.Should().Be(999);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .Delete();
    }

    // Note: Update() without WHERE is compile-time prevented (Update is on IUpdateWhereClause, not ISetClause)
    // Similarly, UpdateAll() is available on ISetClause for explicit "update all" operations
    // Update without Set is compile-time prevented (you must call Set before reaching Update)

    [Fact]
    public void Update_WithAnonymousObject_UpdatesAllProperties()
    {
        // Arrange - Insert a test product
        var testName = $"TestUpdateAnon_{_testId}";
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = testName,
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        // Act
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(new { UnitPrice = 777.77m, UnitsInStock = (short)777 })
            .Where(p => p.ProductId == (int)insertedId)
            .Update();

        // Assert
        rowsUpdated.Should().Be(1);

        // Verify
        var updated = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .SelectFirst();
        updated.UnitPrice.Should().Be(777.77m);
        updated.UnitsInStock.Should().Be(777);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .Delete();
    }

    [Fact]
    public async Task UpdateAsync_WithSetAndWhere_UpdatesMatchingRows()
    {
        // Arrange - Insert a test product
        var testName = $"TestUpdateAsync_{_testId}";
        var insertedId = _db.Connection.Into<Product>()
            .Values(new
            {
                ProductName = testName,
                SupplierId = 1,
                CategoryId = (short)1,
                UnitPrice = 10.00m,
                UnitsInStock = (short)10,
                UnitsOnOrder = (short)0,
                ReorderLevel = (short)5,
                Discontinued = false
            })
            .Insert();

        // Act
        var rowsUpdated = await _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 666.66m)
            .Where(p => p.ProductId == (int)insertedId)
            .UpdateAsync();

        // Assert
        rowsUpdated.Should().Be(1);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)insertedId)
            .Delete();
    }

    [Fact]
    public void Update_ToSql_ReturnsCorrectSql()
    {
        // Act
        var sql = ((ISetClause<Product>)_db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 100m))
            .ToSql();

        // Assert
        sql.Should().Contain("UPDATE");
        sql.Should().Contain("products");
        sql.Should().Contain("SET");
        sql.Should().Contain("unit_price");
    }

    #endregion

    #region Insert Tests

    [Fact]
    public void Insert_WithEntity_InsertsAndReturnsId()
    {
        // Arrange
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

        // Act
        var id = _db.Connection.Into<Product>()
            .Values(product)
            .Insert();

        // Assert
        id.Should().BeGreaterThan(0);

        // Verify
        var inserted = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .SelectFirst();
        inserted.ProductName.Should().Be("TestInsertProduct");

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .Delete();
    }

    [Fact]
    public void Insert_WithAnonymousObject_InsertsAndReturnsId()
    {
        // Act
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

        // Assert
        id.Should().BeGreaterThan(0);

        // Verify
        var inserted = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .SelectFirst();
        inserted.ProductName.Should().Be("TestInsertAnon");
        inserted.UnitPrice.Should().Be(29.99m);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .Delete();
    }

    [Fact]
    public void Insert_WithIndividualValues_InsertsAndReturnsId()
    {
        // Act - Use string column names for nullable columns to avoid type inference issues
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

        // Assert
        id.Should().BeGreaterThan(0);

        // Verify
        var inserted = _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .SelectFirst();
        inserted.ProductName.Should().Be("TestInsertIndividual");

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .Delete();
    }

    [Fact]
    public async Task InsertAsync_WithEntity_InsertsAndReturnsId()
    {
        // Arrange
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

        // Act
        var id = await _db.Connection.Into<Product>()
            .Values(product)
            .InsertAsync();

        // Assert
        id.Should().BeGreaterThan(0);

        // Cleanup
        _db.Connection.From<Product>()
            .Where(p => p.ProductId == (int)id)
            .Delete();
    }

    [Fact]
    public void Insert_ToSql_ReturnsCorrectSql()
    {
        // Act
        var sql = _db.Connection.Into<Product>()
            .Value(p => p.ProductName, "Test")
            .Value(p => p.UnitPrice, 10m)
            .ToSql();

        // Assert
        sql.Should().Contain("INSERT INTO");
        sql.Should().Contain("products");
        sql.Should().Contain("product_name");
        sql.Should().Contain("unit_price");
        sql.Should().Contain("VALUES");
    }

    // Note: Insert() without Values is compile-time prevented (Insert is on IValuesClause, not IIntoClause)
    // You must call Values() or Value() before reaching Insert()

    #endregion
}
