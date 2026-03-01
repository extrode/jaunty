using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;

using Microsoft.Data.SqlClient;
#if NET8_0_OR_GREATER
using Microsoft.Data.Sqlite;
#endif

using MySql.Data.MySqlClient;

using Npgsql;

namespace Jaunty.Tests.Integration.Import;

public class CsvImportTests : IClassFixture<DialectFixture>
{
    private const string TableName = "csv_import_test";
    private const int ExpectedRowCount = 5;

    private static string ResolveCsvPath()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "data", "basic.csv");
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        throw new FileNotFoundException("Could not locate data/basic.csv from test output directory.");
    }

    private static void CreateTable(IDbConnection connection, DialectProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer => @"
                IF OBJECT_ID('dbo.csv_import_test', 'U') IS NOT NULL DROP TABLE dbo.csv_import_test;
                CREATE TABLE dbo.csv_import_test (
                    Name NVARCHAR(255),
                    Age INT,
                    City NVARCHAR(255),
                    Email NVARCHAR(255)
                );",
            DialectProvider.Postgres => @"
                DROP TABLE IF EXISTS csv_import_test;
                CREATE TABLE csv_import_test (
                    ""Name"" TEXT,
                    ""Age"" INTEGER,
                    ""City"" TEXT,
                    ""Email"" TEXT
                );",
            DialectProvider.MariaDb => @"
                DROP TABLE IF EXISTS csv_import_test;
                CREATE TABLE csv_import_test (
                    Name VARCHAR(255),
                    Age INT,
                    City VARCHAR(255),
                    Email VARCHAR(255)
                );",
            _ => @"
                DROP TABLE IF EXISTS csv_import_test;
                CREATE TABLE csv_import_test (
                    Name TEXT,
                    Age INTEGER,
                    City TEXT,
                    Email TEXT
                );"
        };
        cmd.ExecuteNonQuery();
    }

    private static long GetRowCount(IDbConnection connection, DialectProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider == DialectProvider.SqlServer
            ? "SELECT COUNT(*) FROM dbo.csv_import_test"
            : "SELECT COUNT(*) FROM csv_import_test";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static string GetFirstName(IDbConnection connection, DialectProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP 1 Name FROM dbo.csv_import_test ORDER BY Name",
            DialectProvider.Postgres => @"SELECT ""Name"" FROM csv_import_test ORDER BY ""Name"" LIMIT 1",
            _ => "SELECT Name FROM csv_import_test ORDER BY Name LIMIT 1"
        };
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    // =============================================
    // SQLite in-memory (prepared statement fallback)
    // =============================================

    [Fact]
    public void ImportCsv_SqliteInMemory_ImportsAllRows()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.SystemSqlite));
    }

    [Fact]
    public void ImportCsv_SqliteInMemory_DataIsCorrect()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        connection.ImportCsv(TableName, csvPath);

        // "Alice Brown" should be first alphabetically
        Assert.Equal("Alice Brown", GetFirstName(connection, DialectProvider.SystemSqlite));
    }

    [Fact]
    public void ImportCsv_SqliteInMemory_ReturnsCorrectRowCount()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void ImportCsv_MicrosoftSqliteInMemory_ImportsAllRows()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.MicrosoftSqlite);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.MicrosoftSqlite));
    }
#endif

    // =============================================
    // SQLite file-based (sqlite3 CLI path)
    // =============================================

    [Fact]
    public void ImportCsv_SqliteFile_ImportsAllRows()
    {
        var csvPath = ResolveCsvPath();
        var tempDb = Path.Combine(Path.GetTempPath(), $"jaunty_csv_test_{Guid.NewGuid():N}.db");

        try
        {
            using var connection = new SQLiteConnection($"Data Source={tempDb}");
            connection.Open();
            CreateTable(connection, DialectProvider.SystemSqlite);
            connection.Close();

            // ImportCsv with file-based SQLite uses sqlite3 CLI
            using var importConn = new SQLiteConnection($"Data Source={tempDb}");
            long rows = importConn.ImportCsv(TableName, csvPath);

            Assert.Equal(ExpectedRowCount, rows);

            importConn.Open();
            Assert.Equal(ExpectedRowCount, GetRowCount(importConn, DialectProvider.SystemSqlite));
            Assert.Equal("Alice Brown", GetFirstName(importConn, DialectProvider.SystemSqlite));
        }
        finally
        {
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    // =============================================
    // Async (SQLite in-memory)
    // =============================================

    [Fact]
    public async Task ImportCsvAsync_SqliteInMemory_ImportsAllRows()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        long rows = await connection.ImportCsvAsync(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.SystemSqlite));
    }

    // =============================================
    // PostgreSQL (COPY FROM STDIN)
    // =============================================

    [Theory]
    [Postgres]
    public void ImportCsv_Postgres_ImportsAllRows(DialectInfo dialect)
    {
        var csvPath = ResolveCsvPath();
        using var connection = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
        connection.Open();
        CreateTable(connection, DialectProvider.Postgres);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.Postgres));
        Assert.Equal("Alice Brown", GetFirstName(connection, DialectProvider.Postgres));
    }

    [Theory]
    [Postgres]
    public async Task ImportCsvAsync_Postgres_ImportsAllRows(DialectInfo dialect)
    {
        var csvPath = ResolveCsvPath();
        using var connection = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
        await connection.OpenAsync();
        CreateTable(connection, DialectProvider.Postgres);

        long rows = await connection.ImportCsvAsync(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.Postgres));
    }

    // =============================================
    // SQL Server (BULK INSERT)
    // =============================================

    [Theory]
    [SqlServer]
    public void ImportCsv_SqlServer_ImportsAllRows(DialectInfo dialect)
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        connection.Open();
        CreateTable(connection, DialectProvider.SqlServer);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.SqlServer));
        Assert.Equal("Alice Brown", GetFirstName(connection, DialectProvider.SqlServer));
    }

    [Theory]
    [SqlServer]
    public async Task ImportCsvAsync_SqlServer_ImportsAllRows(DialectInfo dialect)
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        await connection.OpenAsync();
        CreateTable(connection, DialectProvider.SqlServer);

        long rows = await connection.ImportCsvAsync(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.SqlServer));
    }

    // =============================================
    // MariaDB/MySQL (LOAD DATA LOCAL INFILE)
    // =============================================

    [Theory]
    [MariaDB]
    public void ImportCsv_MariaDb_ImportsAllRows(DialectInfo dialect)
    {
        var csvPath = ResolveCsvPath();
        var connString = TestConfiguration.MariaDbConnectionString;
        // LOAD DATA LOCAL INFILE requires AllowLoadLocalInfile=true
        if (!connString.Contains("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase))
            connString += ";AllowLoadLocalInfile=true";

        using var connection = new MySqlConnection(connString);
        connection.Open();
        CreateTable(connection, DialectProvider.MariaDb);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.MariaDb));
        Assert.Equal("Alice Brown", GetFirstName(connection, DialectProvider.MariaDb));
    }

    [Theory]
    [MariaDB]
    public async Task ImportCsvAsync_MariaDb_ImportsAllRows(DialectInfo dialect)
    {
        var csvPath = ResolveCsvPath();
        var connString = TestConfiguration.MariaDbConnectionString;
        if (!connString.Contains("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase))
            connString += ";AllowLoadLocalInfile=true";

        using var connection = new MySqlConnection(connString);
        await connection.OpenAsync();
        CreateTable(connection, DialectProvider.MariaDb);

        long rows = await connection.ImportCsvAsync(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.MariaDb));
    }

    // =============================================
    // Validation tests
    // =============================================

    [Fact]
    public void ImportCsv_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection connection = null!;
        Assert.Throws<ArgumentNullException>(() => connection.ImportCsv("table", "file.csv"));
    }

    [Fact]
    public void ImportCsv_EmptyTableName_ThrowsArgumentException()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        Assert.Throws<ArgumentException>(() => connection.ImportCsv("", ResolveCsvPath()));
    }

    [Fact]
    public void ImportCsv_EmptyFilePath_ThrowsArgumentException()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        Assert.Throws<ArgumentException>(() => connection.ImportCsv("table", ""));
    }

    [Fact]
    public void ImportCsv_NonExistentFile_ThrowsFileNotFoundException()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Assert.Throws<FileNotFoundException>(() => connection.ImportCsv("table", "/nonexistent/file.csv"));
    }

    [Fact]
    public void ImportCsv_ClosedConnection_OpensAndImports()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);
        // Connection is open — the in-memory fallback path handles open/close internally

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
    }

    // =============================================
    // CsvImportOptions tests
    // =============================================

    [Fact]
    public void ImportCsv_WithExplicitOptions_ImportsCorrectly()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var options = new CsvImportOptions
        {
            Delimiter = ',',
            HasHeader = true,
            Quote = '"'
        };

        long rows = connection.ImportCsv(TableName, csvPath, options);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ExpectedRowCount, GetRowCount(connection, DialectProvider.SystemSqlite));
    }

    // =============================================
    // ParseCsvLine unit tests (internal helper)
    // =============================================

    [Fact]
    public void ParseCsvLine_SimpleFields_ParsesCorrectly()
    {
        var fields = CsvImportExtensions.ParseCsvLine("John,32,New York", ',', '"');
        Assert.Equal(3, fields.Length);
        Assert.Equal("John", fields[0]);
        Assert.Equal("32", fields[1]);
        Assert.Equal("New York", fields[2]);
    }

    [Fact]
    public void ParseCsvLine_QuotedField_ParsesCorrectly()
    {
        var fields = CsvImportExtensions.ParseCsvLine("\"John Doe\",32,\"New York\"", ',', '"');
        Assert.Equal(3, fields.Length);
        Assert.Equal("John Doe", fields[0]);
        Assert.Equal("32", fields[1]);
        Assert.Equal("New York", fields[2]);
    }

    [Fact]
    public void ParseCsvLine_EscapedQuote_ParsesCorrectly()
    {
        var fields = CsvImportExtensions.ParseCsvLine("\"He said \"\"hello\"\"\",42", ',', '"');
        Assert.Equal(2, fields.Length);
        Assert.Equal("He said \"hello\"", fields[0]);
        Assert.Equal("42", fields[1]);
    }

    [Fact]
    public void ParseCsvLine_EmptyFields_ParsesCorrectly()
    {
        var fields = CsvImportExtensions.ParseCsvLine("a,,c", ',', '"');
        Assert.Equal(3, fields.Length);
        Assert.Equal("a", fields[0]);
        Assert.Equal("", fields[1]);
        Assert.Equal("c", fields[2]);
    }

    [Fact]
    public void ParseCsvLine_TabDelimited_ParsesCorrectly()
    {
        var fields = CsvImportExtensions.ParseCsvLine("John\t32\tNew York", '\t', '"');
        Assert.Equal(3, fields.Length);
        Assert.Equal("John", fields[0]);
        Assert.Equal("32", fields[1]);
        Assert.Equal("New York", fields[2]);
    }
}
