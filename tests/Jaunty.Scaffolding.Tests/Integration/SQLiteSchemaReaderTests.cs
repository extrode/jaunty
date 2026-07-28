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

        // Composite primary key declared in the *opposite* order to the physical columns.
        // PRAGMA table_info reports rows in physical order, so anything that derives the key
        // order by filtering the column list gets [col_a, col_b] instead of [col_b, col_a].
        cmd.CommandText = @"
            CREATE TABLE reversed_composite_pk (
                col_a INTEGER NOT NULL,
                col_b INTEGER NOT NULL,
                payload TEXT,
                PRIMARY KEY (col_b, col_a)
            )";
        cmd.ExecuteNonQuery();

        // Plain "INTEGER PRIMARY KEY" (no AUTOINCREMENT keyword) - still a rowid alias and
        // therefore still auto-generated.
        cmd.CommandText = @"
            CREATE TABLE plain_rowid_pk (
                item_id INTEGER PRIMARY KEY,
                item_name TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();

        // WITHOUT ROWID table - INTEGER PRIMARY KEY does NOT get rowid aliasing here.
        cmd.CommandText = @"
            CREATE TABLE without_rowid_pk (
                code_id INTEGER PRIMARY KEY,
                code_name TEXT NOT NULL
            ) WITHOUT ROWID";
        cmd.ExecuteNonQuery();

        // Table name containing a single quote, requiring a quoted identifier. Exercises
        // GetCreateTableSqlAsync's WHERE name = @TableName lookup with a value that would have
        // needed manual quote-doubling under the old string-interpolated query.
        cmd.CommandText = @"
            CREATE TABLE ""order's notes"" (
                note_id INTEGER PRIMARY KEY,
                note_text TEXT NOT NULL
            ) WITHOUT ROWID";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsAllTables()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        Assert.Equal(8, schema.Tables.Count);
        Assert.Equal(
            new[] { "customers", "order's notes", "order_details", "orders", "plain_rowid_pk", "products", "reversed_composite_pk", "without_rowid_pk" }.OrderBy(t => t, StringComparer.Ordinal),
            schema.Tables.Select(t => t.TableName).OrderBy(t => t, StringComparer.Ordinal));
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
        Assert.Equal(new[] { "order_id", "product_id" }, orderDetailsTable.PrimaryKey!.Columns);
    }

    // R24: the key order came from filtering the columns (physical order) rather than from the
    // PRAGMA table_info pk ordinal, so a key declared against the grain of the column order was
    // reported reversed. The order-independent Assert.Contains in the test above can't see it,
    // and PrimaryKeyInfo.Columns order is what generated key lookups/parameter order depend on.
    [Fact]
    public async Task ReadSchemaAsync_CompositePrimaryKey_UsesDeclarationOrderNotColumnOrder()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var table = schema.Tables.First(t => t.TableName == "reversed_composite_pk");
        Assert.NotNull(table.PrimaryKey);
        Assert.Equal(new[] { "col_b", "col_a" }, table.PrimaryKey!.Columns);

        // The columns themselves stay in physical order - only the key is re-ordered.
        Assert.Equal(new[] { "col_a", "col_b", "payload" }, table.Columns.Select(c => c.ColumnName));
    }

    [Fact]
    public async Task ReadSchemaAsync_PlainIntegerPrimaryKeyWithoutAutoincrement_IsIdentity()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var table = schema.Tables.First(t => t.TableName == "plain_rowid_pk");
        var idColumn = table.Columns.First(c => c.ColumnName == "item_id");

        // A single-column "INTEGER PRIMARY KEY" is a rowid alias and is always
        // auto-generated, even without the AUTOINCREMENT keyword.
        Assert.True(idColumn.IsPrimaryKey);
        Assert.True(idColumn.IsIdentity);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithoutRowidTable_IntegerPrimaryKeyIsNotIdentity()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var table = schema.Tables.First(t => t.TableName == "without_rowid_pk");
        var idColumn = table.Columns.First(c => c.ColumnName == "code_id");

        // WITHOUT ROWID suppresses rowid aliasing, so this INTEGER PRIMARY KEY is not
        // auto-generated even though it's a single-column INTEGER PK.
        Assert.True(idColumn.IsPrimaryKey);
        Assert.False(idColumn.IsIdentity);
    }

    [Fact]
    public async Task ReadSchemaAsync_CompositePrimaryKey_ColumnsAreNotIdentity()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var table = schema.Tables.First(t => t.TableName == "order_details");

        // A composite primary key doesn't get rowid aliasing, even though each column
        // individually has type affinity INTEGER.
        Assert.All(table.Columns.Where(c => c.IsPrimaryKey), c => Assert.False(c.IsIdentity));
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

        Assert.Equal(7, schema.Tables.Count);
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

    [Fact]
    public async Task ReadSchemaAsync_TableNameWithSingleQuote_ReadsWithoutRowidCorrectly()
    {
        var reader = new SQLiteSchemaReader();
        var options = new SchemaReaderOptions();

        var schema = await reader.ReadSchemaAsync(_connectionString, options);

        var table = schema.Tables.First(t => t.TableName == "order's notes");
        var idColumn = table.Columns.First(c => c.ColumnName == "note_id");

        // WITHOUT ROWID suppresses rowid aliasing; correctly detecting this requires
        // GetCreateTableSqlAsync's WHERE name = @TableName lookup to have matched the
        // apostrophe-containing table name exactly (not truncated/misescaped).
        Assert.True(idColumn.IsPrimaryKey);
        Assert.False(idColumn.IsIdentity);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}