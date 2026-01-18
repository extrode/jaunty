using Microsoft.Data.Sqlite;
using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.SQLite;

namespace Jaunty.Scaffolding.Tests.Integration;

public class SQLiteSchemaReaderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SQLiteSchemaReaderTests()
    {
        // Use in-memory database with shared cache so it persists across connections
        _connectionString = "Data Source=InMemorySchemaTest;Mode=Memory;Cache=Shared";
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

        schema.Tables.Should().HaveCount(4);
        schema.Tables.Select(t => t.TableName).Should()
            .Contain(["products", "customers", "orders", "order_details"]);
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsCorrectColumns()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var productsTable = schema.Tables.First(t => t.TableName == "products");
        productsTable.Columns.Should().HaveCount(4);

        var productIdColumn = productsTable.Columns.First(c => c.ColumnName == "product_id");
        productIdColumn.IsPrimaryKey.Should().BeTrue();
        productIdColumn.IsIdentity.Should().BeTrue();
        productIdColumn.DataType.Should().Be("INTEGER");

        var productNameColumn = productsTable.Columns.First(c => c.ColumnName == "product_name");
        productNameColumn.IsNullable.Should().BeFalse();
        productNameColumn.DataType.Should().Be("TEXT");

        var unitPriceColumn = productsTable.Columns.First(c => c.ColumnName == "unit_price");
        unitPriceColumn.IsNullable.Should().BeTrue();
        unitPriceColumn.DataType.Should().Be("REAL");
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesPrimaryKey()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var productsTable = schema.Tables.First(t => t.TableName == "products");
        productsTable.PrimaryKey.Should().NotBeNull();
        productsTable.PrimaryKey!.Columns.Should().Contain("product_id");
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesCompositePrimaryKey()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var orderDetailsTable = schema.Tables.First(t => t.TableName == "order_details");
        orderDetailsTable.PrimaryKey.Should().NotBeNull();
        orderDetailsTable.PrimaryKey!.Columns.Should().HaveCount(2);
        orderDetailsTable.PrimaryKey!.Columns.Should().Contain("order_id");
        orderDetailsTable.PrimaryKey!.Columns.Should().Contain("product_id");
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

        schema.Tables.Should().HaveCount(2);
        schema.Tables.Select(t => t.TableName).Should()
            .BeEquivalentTo(["products", "customers"]);
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

        schema.Tables.Should().HaveCount(3);
        schema.Tables.Select(t => t.TableName).Should()
            .NotContain("order_details");
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
        ordersTable.ForeignKeys.Should().HaveCount(1);

        var fk = ordersTable.ForeignKeys.First();
        fk.ForeignKeyColumn.Should().Be("customer_id");
        fk.ReferencedTable.Should().Be("customers");
        fk.ReferencedColumn.Should().Be("customer_id");
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
        ordersTable.ForeignKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadSchemaAsync_ColumnsInCorrectOrder()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var customersTable = schema.Tables.First(t => t.TableName == "customers");
        var columnNames = customersTable.Columns.OrderBy(c => c.OrdinalPosition).Select(c => c.ColumnName).ToList();

        columnNames.Should().BeEquivalentTo(
            ["customer_id", "first_name", "last_name", "email", "birth_date", "balance", "is_active"],
            options => options.WithStrictOrdering());
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
