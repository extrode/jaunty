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
    /// <summary>
    /// Fixture table names are suffixed with the process id. They used to be fixed, and every test
    /// here drops and recreates them in the one shared database, so running the net8.0 and net10.0
    /// legs of this suite concurrently made them race: measured on three consecutive combined runs,
    /// 1-2 tests failed at random while each leg alone passed. CI runs the frameworks as separate
    /// jobs, so CI was green either way and only a local combined run saw it.
    /// <para>
    /// The <c>scaffold_test_</c> prefix is kept so the SQL Server cleanup scripts'
    /// <c>LIKE 'scaffold[_]test[_]%'</c> pattern still matches. The <c>scaffold_test_code</c> alias
    /// type is deliberately NOT suffixed: it is created idempotently and never dropped by a test,
    /// so it does not race, and leaving it alone keeps the scripts' exact-name TYPE drop valid.
    /// </para>
    /// </summary>
    private static readonly string Products = $"scaffold_test_products_{Environment.ProcessId}";
    private static readonly string Orders = $"scaffold_test_orders_{Environment.ProcessId}";
    private static readonly string OrderItems = $"scaffold_test_order_items_{Environment.ProcessId}";
    private static readonly string TypeNames = $"scaffold_test_typenames_{Environment.ProcessId}";

    private static SqlConnection OpenOrSkip()
    {
        if (!TestConfiguration.HasSqlServer)
        {
            RequiredEngine.SkipOrFail(RequiredEngine.SqlServer, "SQL Server not configured. Set JAUNTY_TEST_SQLSERVER or ConnectionStrings:SqlServer.");
        }

        var conn = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            RequiredEngine.SkipOrFail(RequiredEngine.SqlServer, $"SQL Server not reachable: {ex.Message}");
        }
        return conn;
    }

    private static void CreateProductsAndOrders(SqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS {Orders};
            DROP TABLE IF EXISTS {Products};
            CREATE TABLE {Products} (
                product_id INT IDENTITY(1,1) PRIMARY KEY,
                product_name NVARCHAR(100) NOT NULL,
                unit_price DECIMAL(10,2) NULL,
                full_label AS (product_name + '!')
            );
            CREATE TABLE {Orders} (
                order_id INT IDENTITY(1,1) PRIMARY KEY,
                product_id INT NOT NULL REFERENCES {Products}(product_id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateCompositeKeyTable(SqlConnection conn)
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
            typeCmd.CommandText = $"""
                DROP TABLE IF EXISTS {TypeNames};
                IF TYPE_ID('scaffold_test_code') IS NULL CREATE TYPE scaffold_test_code FROM NVARCHAR(20);
                """;
            typeCmd.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE {TypeNames} (
                id INT IDENTITY(1,1) PRIMARY KEY,
                area GEOGRAPHY NULL,
                shape GEOMETRY NULL,
                node HIERARCHYID NULL,
                code scaffold_test_code NULL,
                anything SQL_VARIANT NULL,
                object_name SYSNAME NULL
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
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [TypeNames] });

        var table = schema.Tables.Single();

        Assert.Equal("geography", table.Columns.Single(c => c.ColumnName == "area").DataType);
        Assert.Equal("geometry", table.Columns.Single(c => c.ColumnName == "shape").DataType);
        Assert.Equal("hierarchyid", table.Columns.Single(c => c.ColumnName == "node").DataType);
    }

    /// <summary>
    /// AUD-R34-038 (round-33 carry-forward, coverage). SqlServerTypeMapper has a "sql_variant"
    /// arm and a unit test for it, but nothing asserted that the schema reader ever reports that
    /// name - and the mapper's fallback for an unrecognised name is the same "object" the
    /// sql_variant arm returns, so a reader reporting anything else would look identical in the
    /// generated entity and in every existing test.
    /// </summary>
    [Fact]
    public async Task ReadSchemaAsync_ReportsASqlVariantColumnAsSqlVariant()
    {
        using var conn = OpenOrSkip();
        CreateTypeNameTable(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [TypeNames] });

        var table = schema.Tables.Single();

        Assert.Equal("sql_variant", table.Columns.Single(c => c.ColumnName == "anything").DataType);
    }

    /// <summary>
    /// AUD-R35-273, the coverage half of AUD-R35-042. <c>sysname</c> is the one alias type SQL
    /// Server ships and it is
    /// flagged a *system* type (sys.types: name sysname, system_type_id 231, user_type_id 256,
    /// is_user_defined 0), so the AUD-R33-008 CASE's <c>is_user_defined = 1</c> arm never reached
    /// it and it took <c>ELSE ty.name</c>. Nothing in tests/ named the type, which is why that
    /// regression went unseen: DataType became the literal "sysname", the mapper had no arm for it
    /// and answered <c>object</c>, and NormalizeMaxLength - which halves only for nchar/nvarchar -
    /// reported the 256-byte length as 256 characters instead of 128.
    /// </summary>
    [Fact]
    public async Task ReadSchemaAsync_ReportsASysnameColumnAsNvarchar()
    {
        using var conn = OpenOrSkip();
        CreateTypeNameTable(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [TypeNames] });

        var column = schema.Tables.Single().Columns.Single(c => c.ColumnName == "object_name");

        Assert.Equal("nvarchar", column.DataType);
        Assert.Equal(128, column.MaxLength);
        Assert.Equal("string", new SqlServerTypeMapper().MapToCSharpType(column).TypeName);
    }

    [Fact]
    public async Task ReadSchemaAsync_ReportsAnAliasTypeAsItsBaseType()
    {
        using var conn = OpenOrSkip();
        CreateTypeNameTable(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [TypeNames] });

        var table = schema.Tables.Single();

        Assert.Equal("nvarchar", table.Columns.Single(c => c.ColumnName == "code").DataType);
    }

    [Fact]
    public async Task ReadSchemaAsync_ReturnsTableWithColumns()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

        var table = Assert.Single(schema.Tables);
        Assert.Equal(Products, table.TableName);
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
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

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
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [OrderItems] });

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
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

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
            TestConfiguration.SqlServerConnectionString, new SchemaReaderOptions { IncludeTables = [Products] });

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
            new SchemaReaderOptions { IncludeTables = [Products, Orders] });

        Assert.Equal(2, schema.Tables.Count(t => t.TableName == Products || t.TableName == Orders));
    }

    [Fact]
    public async Task ReadSchemaAsync_WithForeignKeys_ReadsForeignKeyInfo()
    {
        using var conn = OpenOrSkip();
        CreateProductsAndOrders(conn);

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString,
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

        var schema = await new SqlServerSchemaReader().ReadSchemaAsync(
            TestConfiguration.SqlServerConnectionString,
            new SchemaReaderOptions { IncludeTables = [Orders], IncludeForeignKeys = false });

        var ordersTable = schema.Tables.Single(t => t.TableName == Orders);
        Assert.Empty(ordersTable.ForeignKeys);
    }
}
