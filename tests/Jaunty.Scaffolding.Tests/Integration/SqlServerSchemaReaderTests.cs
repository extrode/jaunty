using Microsoft.Data.SqlClient;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Tests.Helpers;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R14 batch-8 coverage gap: SqlServerSchemaReader only had MarkPrimaryKeyColumns and
/// NormalizeMaxLength unit tested. ReadSchemaAsync/GetTableInfosAsync/ReadTableSchemaAsync/
/// ReadColumnsAsync/ReadPrimaryKeyAsync/ReadForeignKeysAsync/CreateConnection were never
/// exercised against a real database. Skipped dynamically when no local SQL Server is reachable
/// (mirrors PostgreSqlBulkCopyProviderTests.cs's OpenOrSkip pattern); CI already sets
/// JAUNTY_TEST_SQLSERVER for a live mssql service container, so these run there too.
/// </summary>
public class SqlServerSchemaReaderTests
{
    private static SqlConnection OpenOrSkip()
    {
        if (!TestConfiguration.HasSqlServer)
        {
            Assert.Skip("SQL Server not configured. Set JAUNTY_TEST_SQLSERVER or ConnectionStrings:SqlServer.");
        }

        var conn = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"SQL Server not reachable: {ex.Message}");
        }
        return conn;
    }

    private static void CreateProductsAndOrders(SqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS scaffold_test_orders;
            DROP TABLE IF EXISTS scaffold_test_products;
            CREATE TABLE scaffold_test_products (
                product_id INT IDENTITY(1,1) PRIMARY KEY,
                product_name NVARCHAR(100) NOT NULL,
                unit_price DECIMAL(10,2) NULL,
                full_label AS (product_name + '!')
            );
            CREATE TABLE scaffold_test_orders (
                order_id INT IDENTITY(1,1) PRIMARY KEY,
                product_id INT NOT NULL REFERENCES scaffold_test_products(product_id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateCompositeKeyTable(SqlConnection conn)
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

    /// <summary>
    /// AUD-R33-008. The three CLR-backed system types share <c>system_type_id</c> 240, so the old
    /// <c>TYPE_NAME(ty.system_type_id)</c> could not tell them apart; the alias column is the
    /// control that the fix did not swap that bug for the opposite one, where a user-defined alias
    /// reports its own name instead of the base type <c>SqlServerTypeMapper</c> understands.
    /// </summary>
    private static void CreateTypeNameTable(SqlConnection conn)
    {
        // Two commands, not one batch: SQL Server compiles a batch in full before running any of it,
        // so a CREATE TABLE naming a type the same batch creates fails to compile ("Cannot find data
        // type scaffold_test_code") even though the CREATE TYPE precedes it.
        using (var typeCmd = conn.CreateCommand())
        {
            typeCmd.CommandText = """
                DROP TABLE IF EXISTS scaffold_test_typenames;
                IF TYPE_ID('scaffold_test_code') IS NULL CREATE TYPE scaffold_test_code FROM NVARCHAR(20);
                """;
            typeCmd.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE scaffold_test_typenames (
                id INT IDENTITY(1,1) PRIMARY KEY,
                area GEOGRAPHY NULL,
                shape GEOMETRY NULL,
                node HIERARCHYID NULL,
                code scaffold_test_code NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ReadSchemaAsync_ReportsEachClrTypeUnderItsOwnName()
    {
        using var conn = OpenOrSkip();
        CreateTypeNameTable(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_typenames"] });

        var table = schema.Tables.Single();

        Assert.Equal("geography", table.Columns.Single(c => c.ColumnName == "area").DataType);
        Assert.Equal("geometry", table.Columns.Single(c => c.ColumnName == "shape").DataType);
        Assert.Equal("hierarchyid", table.Columns.Single(c => c.ColumnName == "node").DataType);
    }

    [Fact]
    public async Task ReadSchemaAsync_ReportsAnAliasTypeAsItsBaseType()
    {
        using var conn = OpenOrSkip();
        CreateTypeNameTable(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_typenames"] });

        var table = schema.Tables.Single();

        Assert.Equal("nvarchar", table.Columns.Single(c => c.ColumnName == "code").DataType);
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsTableWithColumns()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        var table = Assert.Single(schema.Tables);
        Assert.Equal("scaffold_test_products", table.TableName);
        Assert.Equal("dbo", table.SchemaName);
        Assert.Equal(4, table.Columns.Count);
        Assert.Contains(table.Columns, c => c.ColumnName == "product_name" && c.DataType == "nvarchar");
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesPrimaryKeyAndIdentity()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

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

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_order_items"] });

        var table = schema.Tables.Single();
        Assert.NotNull(table.PrimaryKey);
        Assert.Equal(2, table.PrimaryKey!.Columns.Count);
        Assert.All(table.Columns.Where(c => c.ColumnName is "order_id" or "line_number"), c => Assert.True(c.IsPrimaryKey));
    }

    [Fact]
    public async Task ReadSchemaAsync_IdentifiesComputedColumn()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        var table = schema.Tables.Single();
        var computed = table.Columns.Single(c => c.ColumnName == "full_label");
        var plain = table.Columns.Single(c => c.ColumnName == "product_name");

        Assert.True(computed.IsComputed);
        Assert.False(plain.IsComputed);
    }

    [Fact]
    public async Task ReadSchemaAsync_NvarcharMaxLength_IsHalvedFromByteLength()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = ["scaffold_test_products"] });

        var nameColumn = schema.Tables.Single().Columns.Single(c => c.ColumnName == "product_name");

        // NVARCHAR(100) is stored as 200 bytes (sys.columns.max_length); NormalizeMaxLength must
        // report the character length, not the raw byte length.
        Assert.Equal(100, nameColumn.MaxLength);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithIncludeTables_FiltersCorrectly()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString,
            new SchemaReaderOptions { IncludeTables = ["scaffold_test_products", "scaffold_test_orders"] });

        Assert.Equal(2, schema.Tables.Count(t => t.TableName is "scaffold_test_products" or "scaffold_test_orders"));
    }

    [Fact]
    public async Task ReadSchemaAsync_WithForeignKeys_ReadsForeignKeyInfo()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString,
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

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString,
            new SchemaReaderOptions { IncludeTables = ["scaffold_test_orders"], IncludeForeignKeys = false });

        var ordersTable = schema.Tables.Single(t => t.TableName == "scaffold_test_orders");
        Assert.Empty(ordersTable.ForeignKeys);
    }
}
