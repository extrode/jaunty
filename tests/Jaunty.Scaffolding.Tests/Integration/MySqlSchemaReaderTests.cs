using MySqlConnector;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Tests.Helpers;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R14 batch-8 coverage gap: MySqlSchemaReader had only ShouldSkipDatabase unit tested;
/// ReadSchemaAsync/GetTableNamesAsync/ReadTableSchemaAsync/ReadColumnsAsync/ReadPrimaryKeyAsync/
/// ReadForeignKeysAsync/CreateConnection/ToClampedInt32 were never exercised anywhere in the repo.
/// Skipped dynamically when no local MySQL/MariaDB server is reachable (mirrors
/// PostgreSqlBulkCopyProviderTests.cs's OpenOrSkip pattern).
/// </summary>
public class MySqlSchemaReaderTests
{
    private static MySqlConnection OpenOrSkip()
    {
        if (!TestConfiguration.HasMySql)
        {
            Assert.Skip("MySQL not configured. Set JAUNTY_TEST_MYSQL/JAUNTY_TEST_MARIADB or ConnectionStrings:MySql.");
        }

        var conn = new MySqlConnection(TestConfiguration.MySqlConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"MySQL not reachable: {ex.Message}");
        }
        return conn;
    }

    private static void CreateProductsAndOrders(MySqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS scaffold_test_orders;
            DROP TABLE IF EXISTS scaffold_test_products;
            CREATE TABLE scaffold_test_products (
                product_id INT AUTO_INCREMENT PRIMARY KEY,
                product_name VARCHAR(100) NOT NULL,
                unit_price DECIMAL(10,2) NULL,
                full_label VARCHAR(150) GENERATED ALWAYS AS (CONCAT(product_name, '!')) VIRTUAL
            );
            CREATE TABLE scaffold_test_orders (
                order_id INT AUTO_INCREMENT PRIMARY KEY,
                product_id INT NOT NULL,
                FOREIGN KEY (product_id) REFERENCES scaffold_test_products(product_id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateCompositeKeyTable(MySqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS scaffold_test_order_items;
            CREATE TABLE scaffold_test_order_items (
                order_id INT NOT NULL,
                line_number INT NOT NULL,
                quantity INT NOT NULL,
                PRIMARY KEY (order_id, line_number)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsTableWithColumns()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        var table = Assert.Single(schema.Tables);
        Assert.Equal("scaffold_test_products", table.TableName);
        Assert.Equal(4, table.Columns.Count);
        Assert.Contains(table.Columns, c => c.ColumnName == "product_name" && c.DataType == "varchar");
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesPrimaryKeyAndIdentity()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        var table = schema.Tables.Single();
        var idColumn = table.Columns.Single(c => c.ColumnName == "product_id");

        Assert.True(idColumn.IsPrimaryKey);
        Assert.True(idColumn.IsIdentity);
        Assert.NotNull(table.PrimaryKey);
        Assert.Contains("product_id", table.PrimaryKey!.Columns);
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesCompositePrimaryKey()
    {
        using var conn = OpenOrSkip();
        CreateCompositeKeyTable(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_order_items"] });

        var table = schema.Tables.Single();
        Assert.NotNull(table.PrimaryKey);
        Assert.Equal(2, table.PrimaryKey!.Columns.Count);
        Assert.All(table.Columns.Where(c => c.ColumnName is "order_id" or "line_number"), c => Assert.True(c.IsPrimaryKey));
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesGeneratedColumnAsComputed()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        var table = schema.Tables.Single();
        var computed = table.Columns.Single(c => c.ColumnName == "full_label");
        var plain = table.Columns.Single(c => c.ColumnName == "product_name");

        Assert.True(computed.IsComputed);
        Assert.False(plain.IsComputed);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithIncludeTables_FiltersCorrectly()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString,
            new SchemaReaderOptions { IncludeTables = ["scaffold_test_products", "scaffold_test_orders"] });

        Assert.Equal(2, schema.Tables.Count(t => t.TableName is "scaffold_test_products" or "scaffold_test_orders"));
    }

    [Fact]
    public async Task ReadSchemaAsync_WithForeignKeys_ReadsForeignKeyInfo()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString,
            new SchemaReaderOptions { IncludeTables = ["scaffold_test_orders"], IncludeForeignKeys = true });

        var ordersTable = schema.Tables.Single(t => t.TableName == "scaffold_test_orders");
        var fk = Assert.Single(ordersTable.ForeignKeys);

        Assert.Equal("product_id", fk.ForeignKeyColumn);
        Assert.Equal("scaffold_test_products", fk.ReferencedTable);
        Assert.Equal("product_id", fk.ReferencedColumn);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithoutForeignKeys_DoesNotReadForeignKeys()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString,
            new SchemaReaderOptions { IncludeTables = ["scaffold_test_orders"], IncludeForeignKeys = false });

        var ordersTable = schema.Tables.Single(t => t.TableName == "scaffold_test_orders");
        Assert.Empty(ordersTable.ForeignKeys);
    }

    [Fact]
    public async Task ReadSchemaAsync_DatabaseName_MatchesConnectionDatabase()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            conn.ConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        Assert.Equal(conn.Database, schema.DatabaseName);
    }
}
