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
    // Suffixed per process so the net8/net10/net472 legs of one `dotnet test` run - which execute
    // concurrently against the same MySQL instance - cannot drop and recreate each other's
    // fixtures mid-assertion. Same fix as SqlServerSchemaReaderTests.
    private static readonly string Products = $"scaffold_test_products_{Environment.ProcessId}";
    private static readonly string Orders = $"scaffold_test_orders_{Environment.ProcessId}";
    private static readonly string OrderItems = $"scaffold_test_order_items_{Environment.ProcessId}";

    private static MySqlConnection OpenOrSkip()
    {
        if (!TestConfiguration.HasMySql)
        {
            RequiredEngine.SkipOrFail(RequiredEngine.MySql, "MySQL not configured. Set JAUNTY_TEST_MYSQL/JAUNTY_TEST_MARIADB or ConnectionStrings:MySql.");
        }

        var conn = new MySqlConnection(TestConfiguration.MySqlConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            RequiredEngine.SkipOrFail(RequiredEngine.MySql, $"MySQL not reachable: {ex.Message}");
        }
        return conn;
    }

    private static void CreateProductsAndOrders(MySqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS {Orders};
            DROP TABLE IF EXISTS {Products};
            CREATE TABLE {Products} (
                product_id INT AUTO_INCREMENT PRIMARY KEY,
                product_name VARCHAR(100) NOT NULL,
                unit_price DECIMAL(10,2) NULL,
                full_label VARCHAR(150) GENERATED ALWAYS AS (CONCAT(product_name, '!')) VIRTUAL
            );
            CREATE TABLE {Orders} (
                order_id INT AUTO_INCREMENT PRIMARY KEY,
                product_id INT NOT NULL,
                FOREIGN KEY (product_id) REFERENCES {Products}(product_id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateCompositeKeyTable(MySqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS {OrderItems};
            CREATE TABLE {OrderItems} (
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
            TestConfiguration.MySqlConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

        var table = Assert.Single(schema.Tables);
        Assert.Equal(Products, table.TableName);
        Assert.Equal(4, table.Columns.Count);
        Assert.Contains(table.Columns, c => c.ColumnName == "product_name" && c.DataType == "varchar");
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesPrimaryKeyAndIdentity()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            TestConfiguration.MySqlConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

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
            TestConfiguration.MySqlConnectionString, new SchemaReaderOptions { IncludeTables = [OrderItems] });

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
            TestConfiguration.MySqlConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

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
            TestConfiguration.MySqlConnectionString,
            new SchemaReaderOptions { IncludeTables = [Products, Orders] });

        Assert.Equal(2, schema.Tables.Count(t => t.TableName == Products || t.TableName == Orders));
    }

    [Fact]
    public async Task ReadSchemaAsync_WithForeignKeys_ReadsForeignKeyInfo()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            TestConfiguration.MySqlConnectionString,
            new SchemaReaderOptions { IncludeTables = [Orders], IncludeForeignKeys = true });

        var ordersTable = schema.Tables.Single(t => t.TableName == Orders);
        var fk = Assert.Single(ordersTable.ForeignKeys);

        Assert.Equal("product_id", fk.ForeignKeyColumn);
        Assert.Equal(Products, fk.ReferencedTable);
        Assert.Equal("product_id", fk.ReferencedColumn);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithoutForeignKeys_DoesNotReadForeignKeys()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            TestConfiguration.MySqlConnectionString,
            new SchemaReaderOptions { IncludeTables = [Orders], IncludeForeignKeys = false });

        var ordersTable = schema.Tables.Single(t => t.TableName == Orders);
        Assert.Empty(ordersTable.ForeignKeys);
    }

    [Fact]
    public async Task ReadSchemaAsync_DatabaseName_MatchesConnectionDatabase()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new MySqlSchemaReader().ReadSchemaAsync(
            TestConfiguration.MySqlConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

        Assert.Equal(conn.Database, schema.DatabaseName);
    }
}
