using System.Data;
using System.Data.Common;

using DuckDB.NET.Data;

namespace Jaunty.FlatFiles.DuckDB.Tests.Core;

/// <summary>
/// Integration tests verifying DuckDB.NET ADO.NET compatibility.
/// These tests use a real in-memory DuckDB instance.
/// </summary>
public class DuckDbConnectionTests : IDisposable
{
    private readonly DuckDBConnection _connection;

    public DuckDbConnectionTests()
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void Connection_CanOpenAndClose()
    {
        using var conn = new DuckDBConnection("DataSource=:memory:");
        Assert.Equal(ConnectionState.Closed, conn.State);
        conn.Open();
        Assert.Equal(ConnectionState.Open, conn.State);
        conn.Close();
        Assert.Equal(ConnectionState.Closed, conn.State);
    }

    [Fact]
    public void Connection_TypeName_IsDuckDBConnection()
    {
        Assert.Equal("DuckDBConnection", _connection.GetType().Name);
    }

    [Fact]
    public void Connection_IsDbConnection()
    {
        Assert.IsAssignableFrom<DbConnection>(_connection);
    }

    [Fact]
    public void ExecuteDdl_CreateTable_Succeeds()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_ddl (id INTEGER PRIMARY KEY, name VARCHAR)";
        cmd.ExecuteNonQuery();

        // Verify by querying the table
        cmd.CommandText = "SELECT COUNT(*) FROM test_ddl";
        var count = cmd.ExecuteScalar();
        Assert.Equal(0L, Convert.ToInt64(count));
    }

    [Fact]
    public void ExecuteDml_InsertAndSelect_RoundTrips()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_dml (id INTEGER, name VARCHAR, amount DECIMAL(10,2))";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO test_dml VALUES (1, 'Alice', 100.50), (2, 'Bob', 200.75)";
        var rowsAffected = cmd.ExecuteNonQuery();
        Assert.Equal(2, rowsAffected);

        cmd.CommandText = "SELECT id, name, amount FROM test_dml ORDER BY id";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("Alice", reader.GetString(1));
        Assert.Equal(100.50m, reader.GetDecimal(2));

        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.Equal("Bob", reader.GetString(1));
        Assert.Equal(200.75m, reader.GetDecimal(2));

        Assert.False(reader.Read());
    }

    [Fact]
    public void DbDataReader_FieldCount_ReturnsCorrectCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS a, 'hello' AS b, 3.14 AS c";
        using var reader = cmd.ExecuteReader();

        Assert.Equal(3, reader.FieldCount);
        Assert.Equal("a", reader.GetName(0));
        Assert.Equal("b", reader.GetName(1));
        Assert.Equal("c", reader.GetName(2));
    }

    [Fact]
    public void DbDataReader_GetOrdinal_ReturnsCorrectIndex()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS id, 'test' AS name";
        using var reader = cmd.ExecuteReader();

        Assert.Equal(0, reader.GetOrdinal("id"));
        Assert.Equal(1, reader.GetOrdinal("name"));
    }

    [Fact]
    public void NullHandling_IsDBNull_WorksCorrectly()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_null (id INTEGER, nullable_col VARCHAR)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO test_null VALUES (1, 'has value'), (2, NULL)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT id, nullable_col FROM test_null ORDER BY id";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.False(reader.IsDBNull(1));
        Assert.Equal("has value", reader.GetString(1));

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(1));
    }

    [Fact]
    public void ParameterBinding_UsingDollarSyntax_Works()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_params (id INTEGER, name VARCHAR)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO test_params VALUES ($1, $2)";
        cmd.Parameters.Add(new DuckDBParameter { Value = 1 });
        cmd.Parameters.Add(new DuckDBParameter { Value = "Alice" });
        cmd.ExecuteNonQuery();

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT name FROM test_params WHERE id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = 1 });
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Alice", reader.GetString(0));
    }

    [Fact]
    public void ReadCsvAuto_LoadsCsvFile_Successfully()
    {
        // Get the path to the test CSV file
        var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "sales.csv");
        Assert.True(File.Exists(csvPath), $"Test CSV file not found at: {csvPath}");

        var escapedPath = csvPath.Replace("\\", "/").Replace("'", "''");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{escapedPath}', auto_detect = true)";
        var count = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Equal(10, count);
    }

    [Fact]
    public void ReadJsonAuto_LoadsJsonFile_Successfully()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "data", "json", "customers.json");
        Assert.True(File.Exists(jsonPath), $"Test JSON file not found at: {jsonPath}");

        var escapedPath = jsonPath.Replace("\\", "/").Replace("'", "''");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_json_auto('{escapedPath}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Equal(3, count);
    }

    [Fact]
    public void ReadCsv_WithTsvFile_TabDelimited_Works()
    {
        var tsvPath = Path.Combine(AppContext.BaseDirectory, "data", "tsv", "sales.tsv");
        Assert.True(File.Exists(tsvPath), $"Test TSV file not found at: {tsvPath}");

        var escapedPath = tsvPath.Replace("\\", "/").Replace("'", "''");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{escapedPath}', delim = '\t', auto_detect = true)";
        var count = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Equal(5, count);
    }

    [Fact]
    public void CreateView_OverCsv_IsQueryable()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "sales.csv");
        var escapedPath = csvPath.Replace("\\", "/").Replace("'", "''");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CREATE OR REPLACE VIEW sales_view AS SELECT * FROM read_csv('{escapedPath}', auto_detect = true)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT COUNT(*) FROM sales_view";
        var count = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Equal(10, count);
    }

    [Fact]
    public void DateTimeHandling_RoundTrips_Correctly()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_dates (id INTEGER, created_at TIMESTAMP)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO test_dates VALUES (1, '2024-01-15 10:30:00')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT created_at FROM test_dates WHERE id = 1";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        var dt = reader.GetDateTime(0);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(15, dt.Day);
        Assert.Equal(10, dt.Hour);
        Assert.Equal(30, dt.Minute);
    }

    [Fact]
    public void DecimalHandling_PreservesPrecision()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_decimal (id INTEGER, amount DECIMAL(10,2))";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO test_decimal VALUES (1, 15000.50)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT amount FROM test_decimal WHERE id = 1";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        var amount = reader.GetDecimal(0);
        Assert.Equal(15000.50m, amount);
    }
}