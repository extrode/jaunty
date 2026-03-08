using DuckDB.NET.Data;

using Jaunty.Dialects;
using Jaunty.FlatFiles.DuckDB.Dialects;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Integration;

/// <summary>
/// M0 Exit Gate Tests: Proves that Jaunty's materialization pipeline works correctly
/// with DuckDB connections — the core requirement for Milestone 0.
/// </summary>
public class JauntyMaterializationTests : IDisposable
{
    private readonly DuckDBConnection _connection;

    public JauntyMaterializationTests()
    {
        // Register the DuckDB dialect so Jaunty's SqlDialectFactory can resolve it
        SqlDialectFactory.RegisterDialect("DuckDBConnection", DuckDbDialect.Instance);

        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        // Create a test table and insert data
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE sales (
                Id INTEGER PRIMARY KEY,
                product_name VARCHAR,
                Revenue DECIMAL(10,2),
                Quantity INTEGER,
                Date TIMESTAMP,
                region VARCHAR
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            INSERT INTO sales VALUES
            (1, 'Widget A', 15000.50, 120, '2024-01-15 00:00:00', 'Northeast'),
            (2, 'Widget B', 8500.00, 85, '2024-02-20 00:00:00', 'Southeast'),
            (3, 'Gadget X', 22000.75, 200, '2024-03-10 00:00:00', 'Midwest')";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void QueryPartial_WithDuckDb_MaterializesEntities()
    {
        // M0 EXIT GATE: Jaunty's QueryPartial<T> works with DuckDB
        var results = _connection.QueryPartial<SalesRecord>(
            "SELECT Id, product_name, Revenue, Quantity, Date, region FROM sales ORDER BY Id");

        Assert.Equal(3, results.Count);

        Assert.Equal(1, results[0].Id);
        Assert.Equal("Widget A", results[0].ProductName);
        Assert.Equal(15000.50m, results[0].Revenue);
        Assert.Equal(120, results[0].Quantity);
        Assert.Equal("Northeast", results[0].Region);

        Assert.Equal(2, results[1].Id);
        Assert.Equal("Widget B", results[1].ProductName);

        Assert.Equal(3, results[2].Id);
        Assert.Equal("Gadget X", results[2].ProductName);
        Assert.Equal(22000.75m, results[2].Revenue);
    }

    [Fact]
    public void QueryPartial_WithColumnAttribute_MapsCorrectly()
    {
        // SalesRecord has [Column("product_name")] on ProductName
        // and [Column("region")] on Region
        var results = _connection.QueryPartial<SalesRecord>(
            "SELECT Id, product_name, Revenue, Quantity, Date, region FROM sales WHERE Id = 1");

        Assert.Single(results);
        var record = results[0];
        Assert.Equal("Widget A", record.ProductName);
        Assert.Equal("Northeast", record.Region);
    }

    [Fact]
    public void QueryPartial_NullableColumns_HandlesNull()
    {
        // Insert a row with NULL region
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO sales VALUES (99, 'Test', 0.00, 0, '2024-01-01 00:00:00', NULL)";
        cmd.ExecuteNonQuery();

        var results = _connection.QueryPartial<SalesRecord>(
            "SELECT Id, product_name, Revenue, Quantity, Date, region FROM sales WHERE Id = 99");

        Assert.Single(results);
        Assert.Null(results[0].Region);
    }

    [Fact]
    public void QueryPartial_CustomerProfile_MaterializesFromTable()
    {
        // Create a customers table matching CustomerProfile entity
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE customers (
                CustomerId INTEGER PRIMARY KEY,
                Name VARCHAR,
                Email VARCHAR,
                Phone VARCHAR,
                CreatedAt TIMESTAMP
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            INSERT INTO customers VALUES
            (1, 'John Doe', 'john@example.com', '555-0101', '2024-01-15 10:30:00'),
            (2, 'Jane Smith', 'jane@example.com', NULL, '2024-02-20 14:15:00')";
        cmd.ExecuteNonQuery();

        var results = _connection.QueryPartial<CustomerProfile>(
            "SELECT CustomerId, Name, Email, Phone, CreatedAt FROM customers ORDER BY CustomerId");

        Assert.Equal(2, results.Count);

        Assert.Equal(1, results[0].CustomerId);
        Assert.Equal("John Doe", results[0].Name);
        Assert.Equal("john@example.com", results[0].Email);
        Assert.Equal("555-0101", results[0].Phone);

        Assert.Equal(2, results[1].CustomerId);
        Assert.Equal("Jane Smith", results[1].Name);
        Assert.Null(results[1].Phone);
    }

    [Fact]
    public void QueryPartial_FromCsvView_MaterializesViaJaunty()
    {
        // Load CSV file as a DuckDB view, then materialize via Jaunty
        var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "sales.csv");
        Assert.True(File.Exists(csvPath), $"Test CSV file not found at: {csvPath}");

        var escapedPath = csvPath.Replace("\\", "/").Replace("'", "''");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CREATE OR REPLACE VIEW csv_sales AS SELECT * FROM read_csv('{escapedPath}', auto_detect = true)";
        cmd.ExecuteNonQuery();

        var results = _connection.QueryPartial<SalesRecord>(
            "SELECT Id, product_name, Revenue, Quantity, CAST(Date AS TIMESTAMP) AS Date, region FROM csv_sales ORDER BY Id");

        Assert.Equal(10, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Widget A", results[0].ProductName);
        Assert.Equal(15000.50m, results[0].Revenue);
    }

    [Fact]
    public void QueryPartial_FromJsonView_MaterializesViaJaunty()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "data", "json", "customers.json");
        Assert.True(File.Exists(jsonPath), $"Test JSON file not found at: {jsonPath}");

        var escapedPath = jsonPath.Replace("\\", "/").Replace("'", "''");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CREATE OR REPLACE VIEW json_customers AS SELECT * FROM read_json_auto('{escapedPath}')";
        cmd.ExecuteNonQuery();

        var results = _connection.QueryPartial<CustomerProfile>(
            "SELECT CustomerId, Name, Email, Phone, CreatedAt FROM json_customers ORDER BY CustomerId");

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0].CustomerId);
        Assert.Equal("John Doe", results[0].Name);
        Assert.Equal("john@example.com", results[0].Email);
        Assert.Equal("555-0101", results[0].Phone);

        // Jane Smith has null phone
        Assert.Equal(2, results[1].CustomerId);
        Assert.Null(results[1].Phone);
    }

    [Fact]
    public void DuckDb_RegisterSource_AndQuery()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "sales.csv");
        Assert.True(File.Exists(csvPath), $"Test CSV file not found at: {csvPath}");

        using var db = new DuckDb();

        db.RegisterSource(new CsvFileSource("sales", csvPath, typeof(SalesRecord)));

        var results = db.Connection.QueryPartial<SalesRecord>(
            "SELECT Id, product_name, Revenue, Quantity, CAST(Date AS TIMESTAMP) AS Date, region FROM sales ORDER BY Id");

        Assert.Equal(10, results.Count);
        Assert.Equal("Widget A", results[0].ProductName);
    }

    [Fact]
    public void DuckDb_RegisterJsonSource_AndQuery()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "data", "json", "customers.json");
        Assert.True(File.Exists(jsonPath), $"Test JSON file not found at: {jsonPath}");

        using var db = new DuckDb();

        db.RegisterSource(new JsonFileSource("customers", jsonPath, typeof(CustomerProfile))
        {
            JsonFormat = JsonFileFormat.Array
        });

        var results = db.Connection.QueryPartial<CustomerProfile>(
            "SELECT CustomerId, Name, Email, Phone, CreatedAt FROM customers ORDER BY CustomerId");

        Assert.Equal(3, results.Count);
        Assert.Equal("John Doe", results[0].Name);
    }

    [Fact]
    public void DuckDb_RegisterTsvSource_AndQuery()
    {
        var tsvPath = Path.Combine(AppContext.BaseDirectory, "data", "tsv", "sales.tsv");
        Assert.True(File.Exists(tsvPath), $"Test TSV file not found at: {tsvPath}");

        using var db = new DuckDb();

        db.RegisterSource(new TsvFileSource("sales", tsvPath, typeof(SalesRecord)));

        var results = db.Connection.QueryPartial<SalesRecord>(
            "SELECT Id, product_name, Revenue, Quantity, CAST(Date AS TIMESTAMP) AS Date, region FROM sales ORDER BY Id");

        Assert.Equal(5, results.Count);
        Assert.Equal("Widget A", results[0].ProductName);
    }

    [Fact]
    public void DuckDb_GetSource_ReturnsRegisteredSource()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "sales.csv");

        using var db = new DuckDb();

        var source = new CsvFileSource("sales", csvPath, typeof(SalesRecord));
        db.RegisterSource(source);

        var retrieved = db.GetSource<SalesRecord>();
        Assert.NotNull(retrieved);
        Assert.Equal("sales", retrieved!.TableName);
        Assert.Equal(csvPath, retrieved.FilePath);
    }

    [Fact]
    public void DuckDb_GetSource_ReturnsNull_WhenNotRegistered()
    {
        using var db = new DuckDb();
        Assert.Null(db.GetSource<SalesRecord>());
    }

    [Fact]
    public void SqlDialectFactory_Resolves_DuckDbDialect()
    {
        // Verify that SqlDialectFactory correctly resolves our registered dialect
        SqlDialectFactory.RegisterDialect("DuckDBConnection", DuckDbDialect.Instance);

        using var conn = new DuckDBConnection("DataSource=:memory:");
        var dialect = SqlDialectFactory.GetDialect(conn);

        Assert.NotNull(dialect);
        Assert.IsType<DuckDbDialect>(dialect);
    }
}
