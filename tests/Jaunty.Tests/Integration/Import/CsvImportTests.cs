using System.Data.Common;
using System.Data.SQLite;
using System.Reflection;
using System.Text;

using Jaunty;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

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

    private static string GetFirstEmail(IDbConnection connection, DialectProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP 1 Email FROM dbo.csv_import_test ORDER BY Name",
            DialectProvider.Postgres => @"SELECT ""Email"" FROM csv_import_test ORDER BY ""Name"" LIMIT 1",
            _ => "SELECT Email FROM csv_import_test ORDER BY Name LIMIT 1"
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

    // AUD-R22: sqlite3 doesn't report a row count itself, so ImportViaSqliteCli falls back to
    // CountCsvRows, which used to count physical lines via raw StreamReader.ReadLine() instead of
    // the RFC4180-aware ReadCsvRecord helper. A quoted field with an embedded newline was therefore
    // miscounted as two rows even though sqlite3's own .import correctly imported it as one.
    [Fact]
    public void ImportCsv_SqliteFile_QuotedFieldWithEmbeddedNewline_ReturnsCorrectRowCount()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"jaunty_csv_test_{Guid.NewGuid():N}.db");

        var csv =
            "Name,Age,City,Email\n" +
            "\"Alice\nSmith\",30,NYC,alice@example.com\n" +
            "Bob,25,LA,bob@example.com\n";
        var path = WriteTempCsv(csv);

        try
        {
            using (var setup = new SQLiteConnection($"Data Source={tempDb}"))
            {
                setup.Open();
                CreateTable(setup, DialectProvider.SystemSqlite);
            }

            using var importConn = new SQLiteConnection($"Data Source={tempDb}");
            long rows = importConn.ImportCsv(TableName, path);

            // Without the fix, the embedded newline inflates the count to 3 rows instead of 2.
            Assert.Equal(2L, rows);

            importConn.Open();
            Assert.Equal(2L, GetRowCount(importConn, DialectProvider.SystemSqlite));
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    // AUD-R23: CountCsvRows used to always open the file with a plain new StreamReader(filePath)
    // (default lenient UTF-8 decoding) instead of the caller-supplied CsvImportOptions.Encoding
    // that every other read path in this file honors. Proven here by configuring a strict
    // (throwing) UTF-8 decoder as options.Encoding and feeding it a byte that is invalid on its
    // own in UTF-8: pre-fix, the encoding argument was silently ignored and the built-in lenient
    // StreamReader(filePath) default swallowed the bad byte without complaint; post-fix,
    // CountCsvRows actually decodes with the configured strict encoding and throws.
    [Fact]
    public void CountCsvRows_UsesConfiguredEncoding_ThrowsOnInvalidByteForStrictEncoding()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jaunty_csv_reg_{Guid.NewGuid():N}.csv");
        // 'N','a','m','e','\n', then a lone UTF-8 continuation byte (0x80) - invalid on its own.
        File.WriteAllBytes(path, [(byte)'N', (byte)'a', (byte)'m', (byte)'e', (byte)'\n', 0x80, (byte)'\n']);

        try
        {
            Encoding strictUtf8 = Encoding.GetEncoding(
                "utf-8", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

            MethodInfo method = typeof(CsvImportExtensions).GetMethod(
                "CountCsvRows", BindingFlags.NonPublic | BindingFlags.Static)!;

            var ex = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, [path, false, '"', strictUtf8]));
            Assert.IsType<DecoderFallbackException>(ex.InnerException);
        }
        finally
        {
            File.Delete(path);
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

    [Fact]
    public void ImportCsv_SqliteCli_DotQualifiedTableName_ImportsSuccessfully()
    {
        // Regression test: the top-level ImportCsv/ImportCsvAsync entry point's own
        // ValidateIdentifier regex explicitly accepts a dot-qualified name (e.g. "main.MyTable",
        // for importing into an ATTACHed database), and every other import path
        // (ImportViaPreparedStatements, Postgres, MySQL, SQL Server) correctly splits it via
        // EscapeQualifiedTableName before escaping. ImportViaSqliteCli used to pass the whole
        // dot-qualified name unsplit to EscapeTableName, which rejects dots and throws
        // ArgumentException - the only import path where this same, explicitly-accepted input
        // shape failed. "main" is SQLite's always-present schema name for the primary database
        // file, so no ATTACH is needed to exercise the dot-qualified path.
        const string dotQualifiedTableName = "main." + TableName;
        var csvPath = ResolveCsvPath();
        var tempDb = Path.Combine(Path.GetTempPath(), $"jaunty_csv_dotqualified_{Guid.NewGuid():N}.db");

        try
        {
            using (var setup = new SQLiteConnection($"Data Source={tempDb}"))
            {
                setup.Open();
                CreateTable(setup, DialectProvider.SystemSqlite);
            }

            using var importConn = new SQLiteConnection($"Data Source={tempDb}");
            long rows = importConn.ImportCsv(dotQualifiedTableName, csvPath);

            Assert.Equal(ExpectedRowCount, rows);

            importConn.Open();
            Assert.Equal(ExpectedRowCount, GetRowCount(importConn, DialectProvider.SystemSqlite));
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
        // Written under the test output directory rather than the OS temp dir: SQL Server's
        // BULK INSERT reads the file server-side, and CI only bind-mounts the repo workspace
        // (which the output directory is under) into the mssql container - not /tmp.
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"jaunty_csv_reg_{Guid.NewGuid():N}.csv");
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

    // AUD-R24: the short-row case above is handled deliberately and safely, but the mirror-image
    // case had no handling at all - the binding loop only ran parameters.Length times, so a row
    // with *more* fields than the header imported as a truncated row with the surplus dropped
    // and no error, warning or log. An unescaped delimiter inside an unquoted value is the
    // everyday way to produce one.
    [Fact]
    public void ImportCsv_LongRow_ThrowsInsteadOfSilentlyDiscardingExtraFields()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var csv =
            "Name,Age,City,Email\n" +
            "Alice,30,NYC,alice@example.com\n" +
            "Bob,25,LA,bob@example.com,extra\n";
        var path = WriteTempCsv(csv);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => connection.ImportCsv(TableName, path));

            // The message has to identify which record and by how much, or a large file is
            // undiagnosable. Record 3 == header + Alice + Bob.
            Assert.Contains("record 3", ex.Message);
            Assert.Contains("5 fields", ex.Message);
            Assert.Contains("defines 4", ex.Message);

            // The whole import is rolled back - Alice's good row must not survive a failed batch.
            Assert.Equal(0L, GetRowCount(connection, DialectProvider.SystemSqlite));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Same guard via Microsoft.Data.Sqlite, mirroring
    // ImportCsv_MicrosoftSqliteInMemory_ImportsAllRows: the check lives in the shared
    // prepared-statement path, so it must not depend on which SQLite provider got there.
    [Fact]
    public void ImportCsv_MicrosoftSqliteInMemory_LongRow_Throws()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.MicrosoftSqlite);

        var csv =
            "Name,Age,City,Email\n" +
            "Bob,25,LA,bob@example.com,extra\n";
        var path = WriteTempCsv(csv);
        try
        {
            Assert.Throws<InvalidDataException>(() => connection.ImportCsv(TableName, path));
            Assert.Equal(0L, GetRowCount(connection, DialectProvider.MicrosoftSqlite));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportCsv_NoHeader_LongRow_ThrowsAgainstFirstRecordWidth()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        // Without a header the first data row defines the layout, so the second row is the
        // ragged one.
        var csv =
            "Alice,30,NYC,alice@example.com\n" +
            "Bob,25,LA,bob@example.com,extra\n";
        var path = WriteTempCsv(csv);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => connection.ImportCsv(TableName, path, new CsvImportOptions { HasHeader = false }));

            Assert.Contains("first record", ex.Message);
            Assert.Equal(0L, GetRowCount(connection, DialectProvider.SystemSqlite));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportCsv_QuotedValueContainingDelimiter_IsNotTreatedAsARaggedRow()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        // The remedy the error message points at must actually work: a properly quoted value
        // containing the delimiter parses as one field and imports normally.
        var csv =
            "Name,Age,City,Email\n" +
            "Alice,30,\"NYC, NY\",alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            long rows = connection.ImportCsv(TableName, path);
            Assert.Equal(1L, rows);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT City FROM csv_import_test WHERE Name = 'Alice'";
            Assert.Equal("NYC, NY", cmd.ExecuteScalar() as string);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // AUD-R12: CsvImportOptions.Quote had no validation equivalent to ValidateDelimiter. A
    // caller-supplied Quote = '\\' reached ImportMySql/ImportSqlServer/BuildPostgresCopyExtraOptions
    // as an unescaped single-quoted SQL string literal char; under MySQL's default sql_mode, the
    // backslash escapes the literal's closing quote instead of terminating it, breaking the
    // generated statement. ValidateQuote runs before any dialect dispatch, so an unopened
    // connection is enough to reach it.
    [Fact]
    public void ImportCsv_BackslashQuote_ThrowsArgumentException()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        var options = new CsvImportOptions { Quote = '\\' };

        Assert.Throws<ArgumentException>(() => connection.ImportCsv(TableName, csvPath, options));
    }

    [Fact]
    public async Task ImportCsvAsync_BackslashQuote_ThrowsArgumentException()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SQLiteConnection("Data Source=:memory:");
        var options = new CsvImportOptions { Quote = '\\' };

        await Assert.ThrowsAsync<ArgumentException>(() => connection.ImportCsvAsync(TableName, csvPath, options).AsTask());
    }

    // AUD-R12: ImportViaPreparedStatements (the fallback path for in-memory SQLite connections)
    // read the CSV with StreamReader.ReadLine() and parsed each physical line independently, so a
    // quoted field containing an embedded newline was silently split across two "rows" instead of
    // being read as one field, corrupting the imported data without any error or warning.
    [Fact]
    public void ImportCsv_QuotedFieldWithEmbeddedNewline_ImportsAsSingleRow()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var csv =
            "Name,Age,City,Email\n" +
            "\"Alice\nSmith\",30,NYC,alice@example.com\n" +
            "Bob,25,LA,bob@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            long rows = connection.ImportCsv(TableName, path);

            // Without the fix, the embedded newline splits Alice's row into two: 3 rows instead of 2.
            Assert.Equal(2L, rows);
            Assert.Equal(2L, GetRowCount(connection, DialectProvider.SystemSqlite));

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Name FROM csv_import_test WHERE City = 'NYC'";
            Assert.Equal("Alice\nSmith", cmd.ExecuteScalar());
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

    // =============================================
    // CsvImportOptions.NullValue / Quote coverage
    // AUD-R11 batch-04: NullValue was silently ignored by every native import path except
    // ImportViaPreparedStatements, and had zero test coverage anywhere. Native paths that can't
    // honor it now throw NotSupportedException instead of silently importing the sentinel as
    // literal text; paths that can honor it (sqlite3 CLI .nullvalue, Postgres COPY NULL/QUOTE,
    // MySQL OPTIONALLY ENCLOSED BY, SQL Server FIELDQUOTE) now do.
    // =============================================

    [Fact]
    public void ImportCsv_SqliteInMemory_NullValue_MapsToNull()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        CreateTable(connection, DialectProvider.SystemSqlite);

        var csv =
            "Name,Age,City,Email\n" +
            "Alice,30,N/A,alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            var options = new CsvImportOptions { NullValue = "N/A" };
            long rows = connection.ImportCsv(TableName, path, options);
            Assert.Equal(1L, rows);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT City FROM csv_import_test WHERE Name = 'Alice'";
            var city = cmd.ExecuteScalar();

            Assert.True(city is null || city == DBNull.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportCsv_SqliteFileBased_NullValueSet_ThrowsNotSupportedException()
    {
        // A file-based (non-":memory:") data source routes through ImportViaSqliteCli rather than
        // ImportViaPreparedStatements. The sqlite3 CLI's ".nullvalue" dot-command only affects
        // output formatting, not ".import" (verified directly against the sqlite3 CLI), so there is
        // no way to honor NullValue on this path - it must fail loudly instead of silently importing
        // the sentinel as literal text.
        var tempDb = Path.Combine(Path.GetTempPath(), $"jaunty_csv_nullval_{Guid.NewGuid():N}.db");
        var csv =
            "Name,Age,City,Email\n" +
            "Alice,30,N/A,alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            using (var setup = new SQLiteConnection($"Data Source={tempDb}"))
            {
                setup.Open();
                CreateTable(setup, DialectProvider.SystemSqlite);
            }

            using var connection = new SQLiteConnection($"Data Source={tempDb}");
            var options = new CsvImportOptions { NullValue = "N/A" };

            Assert.Throws<NotSupportedException>(() => connection.ImportCsv(TableName, path, options));
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    [Fact]
    public void ImportViaSqliteCli_QuoteNotDoubleQuote_ThrowsNotSupportedException()
    {
        // sqlite3's CSV mode has no dot-command to override its quote character, unlike the other
        // providers' native import commands. Invoked directly via reflection (same pattern as
        // ImportViaSqliteCli_DbPathWithQuote_IsRejected) since the throw happens before any file
        // or process I/O, so a real dbPath/filePath/table setup isn't needed to reach it.
        var csvPath = ResolveCsvPath();
        MethodInfo method = typeof(CsvImportExtensions).GetMethod(
            "ImportViaSqliteCli", BindingFlags.NonPublic | BindingFlags.Static)!;

        var dbPath = Path.Combine(Path.GetTempPath(), $"jaunty_csv_quote_{Guid.NewGuid():N}.db");
        var options = new CsvImportOptions { Quote = '\'' };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, [dbPath, TableName, csvPath, options]));
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    [Fact]
    public void ImportCsv_SqlServer_NullValueSet_ThrowsNotSupportedException()
    {
        // SQL Server's BULK INSERT has no clause for substituting an arbitrary string as NULL.
        // ThrowIfNullValueUnsupported is the first statement in ImportSqlServer, before the
        // connection is ever opened, so an unopened connection is enough to reach it.
        var csvPath = ResolveCsvPath();
        using var connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        var options = new CsvImportOptions { NullValue = "N/A" };

        Assert.Throws<NotSupportedException>(() => connection.ImportCsv(TableName, csvPath, options));
    }

    [Fact]
    public async Task ImportCsvAsync_SqlServer_NullValueSet_ThrowsNotSupportedException()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        var options = new CsvImportOptions { NullValue = "N/A" };

        await Assert.ThrowsAsync<NotSupportedException>(() => connection.ImportCsvAsync(TableName, csvPath, options).AsTask());
    }

    [Fact]
    public void ImportCsv_MySql_NullValueSet_ThrowsNotSupportedException()
    {
        // MySQL's LOAD DATA has no clause for substituting an arbitrary string as NULL.
        var csvPath = ResolveCsvPath();
        using var connection = new MySqlConnection(TestConfiguration.MariaDbConnectionString);
        var options = new CsvImportOptions { NullValue = "N/A" };

        Assert.Throws<NotSupportedException>(() => connection.ImportCsv(TableName, csvPath, options));
    }

    [Fact]
    public async Task ImportCsvAsync_MySql_NullValueSet_ThrowsNotSupportedException()
    {
        var csvPath = ResolveCsvPath();
        using var connection = new MySqlConnection(TestConfiguration.MariaDbConnectionString);
        var options = new CsvImportOptions { NullValue = "N/A" };

        await Assert.ThrowsAsync<NotSupportedException>(() => connection.ImportCsvAsync(TableName, csvPath, options).AsTask());
    }

    [Theory]
    [Postgres]
    public void ImportCsv_Postgres_NullValueAndQuote_AppliedNatively(DialectInfo dialect)
    {
        using var connection = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
        connection.Open();
        CreateTable(connection, DialectProvider.Postgres);

        var csv =
            "Name,Age,City,Email\n" +
            "'Alice',30,N/A,alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            var options = new CsvImportOptions { NullValue = "N/A", Quote = '\'' };
            long rows = connection.ImportCsv(TableName, path, options);
            Assert.Equal(1L, rows);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT ""Name"", ""City"" FROM csv_import_test";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("Alice", reader.GetString(0));
            Assert.True(reader.IsDBNull(1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MariaDB]
    public void ImportCsv_MariaDb_CustomQuote_AppliedNatively(DialectInfo dialect)
    {
        var connString = TestConfiguration.MariaDbConnectionString;
        if (connString.IndexOf("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase) < 0)
            connString += ";AllowLoadLocalInfile=true";

        using var connection = new MySqlConnection(connString);
        connection.Open();
        CreateTable(connection, DialectProvider.MariaDb);

        var csv =
            "Name,Age,City,Email\n" +
            "'Alice',30,NYC,alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            var options = new CsvImportOptions { Quote = '\'' };
            long rows = connection.ImportCsv(TableName, path, options);
            Assert.Equal(1L, rows);
            Assert.Equal("Alice", GetFirstName(connection, DialectProvider.MariaDb));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [SqlServer]
    public void ImportCsv_SqlServer_CustomQuote_AppliedNatively(DialectInfo dialect)
    {
        using var connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        connection.Open();
        CreateTable(connection, DialectProvider.SqlServer);

        var csv =
            "Name,Age,City,Email\n" +
            "'Alice',30,NYC,alice@example.com\n";
        var path = WriteTempCsv(csv);
        try
        {
            var options = new CsvImportOptions { Quote = '\'' };
            long rows = connection.ImportCsv(TableName, path, options);
            Assert.Equal(1L, rows);
            Assert.Equal("Alice", GetFirstName(connection, DialectProvider.SqlServer));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [SqlServer]
    public void ImportCsv_SqlServer_CrlfLineEndings_TrailingCarriageReturnStripped(DialectInfo dialect)
    {
        using var connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
        connection.Open();
        CreateTable(connection, DialectProvider.SqlServer);

        var csv =
            "Name,Age,City,Email\r\n" +
            "Alice,30,NYC,alice@example.com\r\n";
        var path = WriteTempCsv(csv);
        try
        {
            long rows = connection.ImportCsv(TableName, path, new CsvImportOptions());
            Assert.Equal(1L, rows);
            Assert.Equal("Alice", GetFirstName(connection, DialectProvider.SqlServer));
            Assert.Equal("alice@example.com", GetFirstEmail(connection, DialectProvider.SqlServer));
        }
        finally
        {
            File.Delete(path);
        }
    }
}