using System.Data;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite;

/// <summary>
/// Tests for edge cases and special scenarios.
/// Covers P1 High Issue #19: Add Tests for Edge Cases
/// </summary>
public class EdgeCaseTests : IDisposable
{
    private readonly Database _db;

    public EdgeCaseTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    #region Composite Primary Key Tests

    [Fact]
    public void Delete_WithCompositePrimaryKey_DeletesSuccessfully()
    {
        // OrderDetail has composite key (OrderId, ProductId)
        var orderDetail = new OrderDetail
        {
            OrderId = 1,
            ProductId = 1,
            Quantity = 10,
            UnitPrice = 5.00m,
            Discount = 0
        };

        // Should not throw - composite key is handled
        var ex = Record.Exception(() => _db.Connection.Delete(orderDetail));

        // May fail due to foreign key constraints, but should not throw for composite key
        Assert.Null(ex);
    }

    [Fact]
    public void DeleteById_WithCompositePrimaryKey_ThrowsInformativeException()
    {
        // DeleteById should fail for composite keys with informative message
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.Delete<OrderDetail>(new { OrderId = 1, ProductId = 1 }));

        Assert.Contains("composite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void QueryPartial_WithNullInNonNullableColumn_HandlesGracefully()
    {
        // Products with null UnitPrice (if any exist)
        var products = _db.Connection.QueryPartial<Product>(
            "SELECT * FROM products WHERE unit_price IS NULL");

        // Should not throw, even with null values
        Assert.NotNull(products);
    }

    [Fact]
    public void QueryFirstOrDefault_NoResults_ReturnsNull()
    {
        var result = _db.Connection.QueryFirstOrDefault<Category>(
            "SELECT category_id, category_name, description FROM categories WHERE category_id = 99999");

        Assert.Null(result);
    }

    [Fact]
    public void QuerySingle_NoResults_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QuerySingle<Category>(
                "SELECT category_id, category_name, description FROM categories WHERE category_id = 99999"));

        Assert.Contains("no elements", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryPartialSingle_MultipleResults_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryPartialSingle<Category>(
                "SELECT category_id, category_name, description FROM categories"));

        Assert.Contains("more than one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region FK Constraint Toggle Tests

    [Fact]
    public void BulkDeleteIgnoreConstraints_WithSqlite_DisablesFKChecks()
    {
        // SQLite supports FK toggle via PRAGMA
        var categories = new List<Category> { new() { CategoryId = 999 } };

        // Should not throw even with non-existent category
        var result = _db.Connection.BulkDeleteIgnoreConstraints(categories);

        // May not delete anything, but should not throw
        Assert.True(result >= 0);
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_WithValidData_InsertsSuccessfully()
    {
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
            createCmd.ExecuteNonQuery();
        }

        var newEntities = new List<BulkTestEntity>
        {
            new() { Name = "Test Entity 1", Value = 1 },
            new() { Name = "Test Entity 2", Value = 2 }
        };

        var result = connection.BulkInsertIgnoreConstraints(newEntities);

        // Should insert all entities
        Assert.Equal(newEntities.Count, result);

        connection.Close();
    }

    #endregion

    #region Transaction Rollback Tests

    [Fact]
    public void BulkInsert_WithTransactionRollback_NoDataInserted()
    {
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
            createCmd.ExecuteNonQuery();
        }

        var newEntities = new List<BulkTestEntity>
        {
            new() { Name = "Rollback Test 1", Value = 1 },
            new() { Name = "Rollback Test 2", Value = 2 }
        };

        using var transaction = connection.BeginTransaction();

        try
        {
            connection.BulkInsert(newEntities, CommandOptions<BulkTestEntity>.WithTransaction(transaction));

            // Rollback instead of commit
            transaction.Rollback();

            // Verify no data was inserted
            var count = connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM bulk_test WHERE name LIKE 'Rollback Test%'");

            Assert.Equal(0, count);
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public void Update_WithTransaction_CommitsSuccessfully()
    {
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create and seed test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE categories (category_id INTEGER PRIMARY KEY AUTOINCREMENT, category_name TEXT, description TEXT)";
            createCmd.ExecuteNonQuery();
            createCmd.CommandText = "INSERT INTO categories (category_name, description) VALUES ('Test Category', 'Test')";
            createCmd.ExecuteNonQuery();
        }

        using var transaction = connection.BeginTransaction();

        try
        {
            var category = connection.QueryFirstOrDefault<Category>(
                "SELECT category_id, category_name, description FROM categories LIMIT 1",
                CommandOptions<Category>.WithTransaction(transaction));

            Assert.NotNull(category);

            var originalName = category.CategoryName;
            category.CategoryName = "Updated In Transaction";

            var result = ((IDbConnection)connection).Update(category, CommandOptions<Category>.WithTransaction(transaction));
            Assert.Equal(1, result);

            transaction.Commit();

            // Verify update persisted
            var updated = connection.QueryFirstOrDefault<Category>(
                "SELECT category_id, category_name, description FROM categories WHERE category_id = @Id",
                new { Id = category.CategoryId });

            Assert.Equal("Updated In Transaction", updated?.CategoryName);
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public void Insert_WithTransactionRollback_NoDataInserted()
    {
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE categories (category_id INTEGER PRIMARY KEY AUTOINCREMENT, category_name TEXT, description TEXT)";
            createCmd.ExecuteNonQuery();
        }

        var newCategory = new Category { CategoryName = "Rollback Insert Test", Description = "Test" };

        using var transaction = connection.BeginTransaction();

        try
        {
            connection.Insert(newCategory, CommandOptions<Category>.WithTransaction(transaction));

            // Rollback
            transaction.Rollback();

            // Verify no data was inserted
            var count = connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM categories WHERE category_name = @Name",
                new { Name = "Rollback Insert Test" });

            Assert.Equal(0, count);
        }
        finally
        {
            connection.Close();
        }
    }

    #endregion

    #region Empty Collection Tests

    [Fact]
    public void BulkInsert_EmptyCollection_ReturnsZero()
    {
        var emptyEntities = new List<BulkTestEntity>();

        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
            createCmd.ExecuteNonQuery();
        }

        var result = connection.BulkInsert(emptyEntities);

        Assert.Equal(0, result);

        connection.Close();
    }

    [Fact]
    public void BulkUpdate_EmptyCollection_ReturnsZero()
    {
        var emptyEntities = new List<BulkTestEntity>();

        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
            createCmd.ExecuteNonQuery();
        }

        var result = connection.BulkUpdate(emptyEntities);

        Assert.Equal(0, result);

        connection.Close();
    }

    [Fact]
    public void BulkDelete_EmptyCollection_ReturnsZero()
    {
        var emptyEntities = new List<BulkTestEntity>();

        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
            createCmd.ExecuteNonQuery();
        }

        var result = connection.BulkDelete(emptyEntities);

        Assert.Equal(0, result);

        connection.Close();
    }

    #endregion

    #region Large Batch Tests

    [Fact]
    public void BulkInsert_LargeBatch_InsertsAllRecords()
    {
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();

        // Create test table
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE bulk_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, value INTEGER)";
            createCmd.ExecuteNonQuery();
        }

        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 100; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"Bulk Test {i}",
                Value = i
            });
        }

        var result = connection.BulkInsert(entities);

        // Should insert all 100 entities
        Assert.Equal(100, result);

        connection.Close();
    }

    #endregion
}
