using System.Data.Common;
using System.Data.SQLite;
using System.Reflection;

using Jaunty;
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

    [Fact]
    public void ImportViaSqliteCli_DbPathWithQuote_IsRejected()
    {
        var csvPath = ResolveCsvPath();

        // ExtractSqliteDbPath just parses "Data Source=..." out of the connection string as
        // plain text and never opens a real file, so a quote character used to reach
        // ImportViaSqliteCli unvalidated - it would break out of the quoted sqlite3 CLI process
        // argument and let extra command-line switches be injected. Invoked directly via
        // reflection (ImportViaSqliteCli is private) rather than through the public ImportCsv
        // entry point, because System.Data.SQLite's own SQLiteConnection connection-string
        // parser already rejects unbalanced quote characters before Jaunty's code ever runs -
        // that's a property of this specific ADO.NET provider, not proof that Jaunty's own
        // dbPath validation (defense-in-depth for other providers/programmatically-built
        // connection strings) is doing anything.
        MethodInfo method = typeof(CsvImportExtensions).GetMethod(
            "ImportViaSqliteCli", BindingFlags.NonPublic | BindingFlags.Static)!;

        var maliciousDbPath = Path.GetTempPath() + "jaunty_csv_sec\"evil.db";

        var ex = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, [maliciousDbPath, TableName, csvPath, new CsvImportOptions()]));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void ImportCsv_SqliteCli_KeywordTableName_ImportsSuccessfully()
    {
        // "GROUP" is a SQLite reserved keyword (SQLiteDialect.Keywords). ImportViaPreparedStatements
        // already escapes it via the dialect; ImportViaSqliteCli's .import dot-command must match
        // that behavior instead of embedding the bare keyword and failing with a syntax error.
        const string keywordTableName = "GROUP";
        var csvPath = ResolveCsvPath();
        var tempDb = Path.Combine(Path.GetTempPath(), $"jaunty_csv_kw_{Guid.NewGuid():N}.db");

        try
        {
            using (var setup = new SQLiteConnection($"Data Source={tempDb}"))
            {
                setup.Open();
                using var cmd = setup.CreateCommand();
                cmd.CommandText = "CREATE TABLE \"GROUP\" (Name TEXT, Age INTEGER, City TEXT, Email TEXT);";
                cmd.ExecuteNonQuery();
            }

            using var importConn = new SQLiteConnection($"Data Source={tempDb}");
            long rows = importConn.ImportCsv(keywordTableName, csvPath);

            Assert.Equal(ExpectedRowCount, rows);

            importConn.Open();
            using var countCmd = importConn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM \"GROUP\"";
            Assert.Equal((long)ExpectedRowCount, Convert.ToInt64(countCmd.ExecuteScalar()));
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
        if (connString.IndexOf("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase) < 0)
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
        if (connString.IndexOf("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase) < 0)
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
        // A private ":memory:" connection destroys its database on Close(), so reopening the
        // same connection object would hit an empty database. A shared-cache in-memory database
        // ("file::memory:?cache=shared") survives as long as at least one connection to it stays
        // open - keepAlive holds that open connection for the test's duration while `connection`
        // itself is closed and reopened, genuinely exercising ImportViaPreparedStatements' closed-
        // connection auto-open path. Uses "FullUri=" rather than "Data Source=" because
        // System.Data.SQLite path-validates a "Data Source" value (rejecting the colons in this
        // URI on Windows); "FullUri" bypasses that and is parsed as a raw SQLite URI. ExtractSqliteDbPath
        // (CsvImport.cs) only recognizes "Data Source=", so "FullUri=" also still correctly falls
        // through to the prepared-statement path rather than the sqlite3 CLI path.
        const string sharedCacheDataSource = "FullUri=file::memory:?cache=shared";
        var csvPath = ResolveCsvPath();
        using var keepAlive = new SQLiteConnection(sharedCacheDataSource);
        keepAlive.Open();
        using var connection = new SQLiteConnection(sharedCacheDataSource);
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);
        connection.Close();
        Assert.Equal(ConnectionState.Closed, connection.State);

        long rows = connection.ImportCsv(TableName, csvPath);

        Assert.Equal(ExpectedRowCount, rows);
        Assert.Equal(ConnectionState.Closed, connection.State);
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

    // =============================================
    // Security & correctness regression tests
    // =============================================

    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"jaunty_csv_reg_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ImportCsv_InjectionShapedTableName_IsRejectedNotExecuted()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        Assert.Throws<ArgumentException>(() =>
            connection.ImportCsv("csv_import_test\"); DROP TABLE csv_import_test;--", csvPath));

        // The table must still exist: the malicious name was rejected, not executed as SQL.
        Assert.Equal(0L, GetRowCount(connection, DialectProvider.SystemSqlite));
    }

    [Fact]
    public void ImportCsv_InjectionShapedColumnHeader_IsRejectedNotExecuted()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var csv =
            "Name,Age,City,x); DROP TABLE csv_import_test;--\n" +
            "Alice,30,NYC,alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            Assert.Throws<ArgumentException>(() => connection.ImportCsv(TableName, path));

            // Table untouched: the injection-shaped header never reached executable SQL.
            Assert.Equal(0L, GetRowCount(connection, DialectProvider.SystemSqlite));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportCsv_SqliteCli_TableNameWithShellInjection_IsRejected()
    {
        var csvPath = ResolveCsvPath();
        var tempDb = Path.Combine(Path.GetTempPath(), $"jaunty_csv_sec_{Guid.NewGuid():N}.db");
        try
        {
            using (var setup = new SQLiteConnection($"Data Source={tempDb}"))
            {
                setup.Open();
                CreateTable(setup, DialectProvider.SystemSqlite);
            }

            using var connection = new SQLiteConnection($"Data Source={tempDb}");
            // A newline would turn the sqlite3 CLI .import token into extra dot-commands (e.g. .shell);
            // it must be rejected before any CLI process is started.
            var malicious = "csv_import_test\n.shell echo pwned";
            Assert.Throws<ArgumentException>(() => connection.ImportCsv(malicious, csvPath));
        }
        finally
        {
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    [Fact]
    public void ImportCsv_ShortRow_FillsNullInsteadOfReusingPreviousValue()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var csv =
            "Name,Age,City,Email\n" +
            "Alice,30,NYC,alice@example.com\n" +
            "Bob,25\n";
        var path = WriteTempCsv(csv);
        try
        {
            long rows = connection.ImportCsv(TableName, path);
            Assert.Equal(2L, rows);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT City FROM csv_import_test WHERE Name = 'Bob'";
            var city = cmd.ExecuteScalar();

            // Bob's row was short; City must be NULL, not a reuse of Alice's 'NYC'.
            Assert.True(city is null || city == DBNull.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportCsv_NoHeader_TreatsFirstRowAsDataNotHeaders()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var csv =
            "Alice,30,NYC,alice@example.com\n" +
            "Bob,25,LA,bob@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            var options = new CsvImportOptions { HasHeader = false };
            long rows = connection.ImportCsv(TableName, path, options);

            // Both lines are data; the first must not be swallowed as a header row.
            Assert.Equal(2L, rows);
            Assert.Equal(2L, GetRowCount(connection, DialectProvider.SystemSqlite));

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM csv_import_test WHERE Name = 'Alice'";
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
        }
        finally
        {
            File.Delete(path);
        }
    }
}