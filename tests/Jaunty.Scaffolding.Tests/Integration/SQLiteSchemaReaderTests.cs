using Microsoft.Data.Sqlite;
using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.SQLite;
using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

public class SQLiteSchemaReaderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SQLiteSchemaReaderTests()
    {
        // Use in-memory database with shared cache so it persists across connections.
        // GUID-suffixed per instance so a future test-method-level-parallelism change (or a
        // constructor failure that skips Dispose) can't cause two instances to collide on the
        // same shared-cache name.
        _connectionString = $"Data Source=InMemorySchemaTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        // Create test tables
        CreateTestTables();
    }

    private void CreateTestTables()
    {
        using var cmd = _connection.CreateCommand();

        // Simple table
        cmd.CommandText = @"
            CREATE TABLE products (
                product_id INTEGER PRIMARY KEY AUTOINCREMENT,
                product_name TEXT NOT NULL,
                unit_price REAL,
                discontinued INTEGER NOT NULL DEFAULT 0
            )";
        cmd.ExecuteNonQuery();

        // Table with multiple columns and types
        cmd.CommandText = @"
            CREATE TABLE customers (
                customer_id INTEGER PRIMARY KEY AUTOINCREMENT,
                first_name TEXT NOT NULL,
                last_name TEXT NOT NULL,
                email TEXT,
                birth_date TEXT,
                balance NUMERIC,
                is_active INTEGER DEFAULT 1
            )";
        cmd.ExecuteNonQuery();

        // Table with foreign key
        cmd.CommandText = @"
            CREATE TABLE orders (
                order_id INTEGER PRIMARY KEY AUTOINCREMENT,
                customer_id INTEGER NOT NULL,
                order_date TEXT NOT NULL,
                total_amount REAL,
                FOREIGN KEY (customer_id) REFERENCES customers(customer_id)
            )";
        cmd.ExecuteNonQuery();

        // Table with composite primary key
        cmd.CommandText = @"
            CREATE TABLE order_details (
                order_id INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                quantity INTEGER NOT NULL,
                unit_price REAL NOT NULL,
                PRIMARY KEY (order_id, product_id)
            )";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsAllTables()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        Assert.Equal(4, schema.Tables.Count);
        Assert.Equal(["customers", "order_details", "orders", "products"],
            schema.Tables.Select(t => t.TableName).Order());
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsCorrectColumns()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var productsTable = schema.Tables.First(t => t.TableName == "products");
        Assert.Equal(4, productsTable.Columns.Count);

        var productIdColumn = productsTable.Columns.First(c => c.ColumnName == "product_id");
        Assert.True(productIdColumn.IsPrimaryKey);
        Assert.True(productIdColumn.IsIdentity);
        Assert.Equal("INTEGER", productIdColumn.DataType);

        var productNameColumn = productsTable.Columns.First(c => c.ColumnName == "product_name");
        Assert.False(productNameColumn.IsNullable);
        Assert.Equal("TEXT", productNameColumn.DataType);

        var unitPriceColumn = productsTable.Columns.First(c => c.ColumnName == "unit_price");
        Assert.True(unitPriceColumn.IsNullable);
        Assert.Equal("REAL", unitPriceColumn.DataType);
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesPrimaryKey()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var productsTable = schema.Tables.First(t => t.TableName == "products");
        Assert.NotNull(productsTable.PrimaryKey);
        Assert.Contains("product_id", productsTable.PrimaryKey!.Columns);
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesCompositePrimaryKey()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var orderDetailsTable = schema.Tables.First(t => t.TableName == "order_details");
        Assert.NotNull(orderDetailsTable.PrimaryKey);
        Assert.Equal(2, orderDetailsTable.PrimaryKey!.Columns.Count);
        Assert.Contains("order_id", orderDetailsTable.PrimaryKey!.Columns);
        Assert.Contains("product_id", orderDetailsTable.PrimaryKey!.Columns);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithIncludeTables_FiltersCorrectly()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions
        {
            IncludeTables = ["products", "customers"]
        };

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        Assert.Equal(2, schema.Tables.Count);
        Assert.Equal(["customers", "products"],
            schema.Tables.Select(t => t.TableName).Order());
    }

    [Fact]
    public async Task ReadSchemaAsync_WithExcludeTables_FiltersCorrectly()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions
        {
            ExcludeTables = ["order_details"]
        };

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        Assert.Equal(3, schema.Tables.Count);
        Assert.DoesNotContain("order_details", schema.Tables.Select(t => t.TableName));
    }

    [Fact]
    public async Task ReadSchemaAsync_WithForeignKeys_ReadsForeignKeyInfo()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions
        {
            IncludeForeignKeys = true
        };

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var ordersTable = schema.Tables.First(t => t.TableName == "orders");
        Assert.Single(ordersTable.ForeignKeys);

        var fk = ordersTable.ForeignKeys.First();
        Assert.Equal("customer_id", fk.ForeignKeyColumn);
        Assert.Equal("customers", fk.ReferencedTable);
        Assert.Equal("customer_id", fk.ReferencedColumn);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithoutForeignKeys_DoesNotReadForeignKeys()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions
        {
            IncludeForeignKeys = false
        };

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var ordersTable = schema.Tables.First(t => t.TableName == "orders");
        Assert.Empty(ordersTable.ForeignKeys);
    }

    [Fact]
    public async Task ReadSchemaAsync_ColumnsInCorrectOrder()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var customersTable = schema.Tables.First(t => t.TableName == "customers");
        var columnNames = customersTable.Columns.OrderBy(c => c.OrdinalPosition).Select(c => c.ColumnName).ToList();

        Assert.Equal(
            ["customer_id", "first_name", "last_name", "email", "birth_date", "balance", "is_active"],
            columnNames);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}