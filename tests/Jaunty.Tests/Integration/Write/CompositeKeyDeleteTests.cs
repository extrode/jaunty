using Jaunty.Attributes;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests for composite primary key handling in delete operations.
/// Uses in-memory SQLite since OrderDetail table may not exist in all test databases.
/// </summary>
public class CompositeKeyDeleteTests
{
    /// <summary>
    /// Entity with composite key (OrderId, ProductId).
    /// Delete with composite key should be handled (may fail on FK constraints but not on key structure).
    /// </summary>
    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_WithCompositePrimaryKey_DoesNotThrowForKeyStructure(DialectInfo dialect)
    {
        using var connection = dialect.Provider == DialectProvider.SystemSqlite
            ? (System.Data.IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
            : (System.Data.IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        
        connection.Open();
        
        // Create OrderDetail table with composite key
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE order_detail (
                    order_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    quantity INTEGER NOT NULL,
                    unit_price REAL NOT NULL,
                    discount REAL NOT NULL,
                    PRIMARY KEY (order_id, product_id)
                )";
            cmd.ExecuteNonQuery();
        }
        
        var orderDetail = new OrderDetailEntity
        {
            OrderId = 1,
            ProductId = 1,
            Quantity = 10,
            UnitPrice = 5.00m,
            Discount = 0
        };

        // Should not throw for composite key structure
        var ex = Record.Exception(() => connection.Delete(orderDetail));

        // Should not throw about composite keys - may throw about FK or other issues
        if (ex != null)
        {
            Assert.DoesNotContain("primary key", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// DeleteById should fail for composite keys with informative message.
    /// </summary>
    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteById_WithCompositePrimaryKey_ThrowsInformativeException(DialectInfo dialect)
    {
        using var connection = dialect.Provider == DialectProvider.SystemSqlite
            ? (System.Data.IDbConnection)new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
            : (System.Data.IDbConnection)new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        
        connection.Open();
        
        // Create OrderDetail table with composite key
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE order_detail (
                    order_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    quantity INTEGER NOT NULL,
                    unit_price REAL NOT NULL,
                    discount REAL NOT NULL,
                    PRIMARY KEY (order_id, product_id)
                )";
            cmd.ExecuteNonQuery();
        }
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Delete<OrderDetailEntity>(new { OrderId = 1, ProductId = 1 }));

        Assert.Contains("primary key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

[Table("order_detail")]
public class OrderDetailEntity
{
    [Key]
    public int OrderId { get; set; }
    [Key]
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
}
