using System.Data;
using System.Text;

using Xunit;

namespace Jaunty.Tests.Unit.Import;

/// <summary>
/// AUD-R26 (batch 4, medium/consistency). <c>ImportCsv</c> validated the file
/// <em>client-side</em> - <c>File.Exists(filePath)</c>, with <c>filePath</c> documented as "the
/// absolute path to the CSV file" - but the four engine paths do not agree on which machine reads
/// it. SQLite and MySQL read it on the client, PostgreSQL reads it on the client when Npgsql's
/// <c>BeginTextImport</c> is available and on the server when it is not, and SQL Server's
/// <c>BULK INSERT ... FROM '&lt;path&gt;'</c> resolves it on the database server, always. Nothing
/// in the API said so.
///
/// <para>
/// The finding described the consequence as false assurance, and it is that: against a remote
/// server the client-side check passes and the statement then fails on the server, or worse reads a
/// <em>different</em> file that happens to exist at that path there. But the check was unconditional
/// and ran before dispatch, which makes it more than misleading - it made a legitimate server-side
/// import <b>impossible</b>. A file that exists on the database server and not on the calling
/// machine is exactly what <c>BULK INSERT</c> is for, and Jaunty rejected it with
/// <c>FileNotFoundException</c> before sending anything.
/// </para>
///
/// <para>
/// This suite's own integration tests are an instance of the defect. Four
/// <c>CsvImportTests</c> SQL Server cases fail locally because they hand a path on the developer's
/// machine to a SQL Server running in a container that cannot see it; the client-side check passes,
/// so nothing catches the mismatch until the provider reports a file it cannot open.
/// </para>
/// </summary>
public class CsvImportFileLocationTests
{
    private static string WriteTempCsv()
    {
        string path = Path.Combine(Path.GetTempPath(), $"jaunty_csv_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Name,Age\nAda,36\n");
        return path;
    }

    private static string NonexistentPath() =>
        Path.Combine(Path.GetTempPath(), $"jaunty_absent_{Guid.NewGuid():N}.csv");

    // ------------------------------------------------------------------
    // Server-side paths must not be gated on the client's filesystem
    // ------------------------------------------------------------------

    /// <summary>
    /// The case the old precondition made unreachable. <c>BULK INSERT</c> reads the server's
    /// filesystem, so a path that does not exist here is not an error Jaunty can diagnose - it has
    /// to reach the server. Observed through the generated statement: the command must be built and
    /// executed rather than rejected up front.
    /// </summary>
    [Fact]
    public void SqlServerImportIsNotBlockedByAPathThatDoesNotExistOnTheClient()
    {
        var connection = new SqlConnection();

        // Not FileNotFoundException: the stub throws from ExecuteNonQuery, which proves the
        // statement was built and sent rather than refused on the caller's filesystem.
        Assert.Throws<InvalidOperationException>(
            () => connection.ImportCsv("t", NonexistentPath()));

        Assert.Contains("BULK INSERT", connection.LastCommandText, StringComparison.Ordinal);
    }

    /// <summary>
    /// And when the server rejects the path, the error must say where the path was resolved. The
    /// provider's own message ("does not exist or you don't have file access rights") is accurate
    /// but describes the server's filesystem while the caller is looking at their own.
    /// </summary>
    [Fact]
    public void AServerSideFailureExplainsThatThePathIsResolvedOnTheServer()
    {
        var connection = new SqlConnection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => connection.ImportCsv("t", NonexistentPath()));

        Assert.Contains("database server", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// The same message must distinguish the two cases a caller confuses. When the file does exist
    /// on the calling machine, saying so is the whole diagnosis: the path is real here and absent
    /// there, which means the two machines are different.
    /// </summary>
    [Fact]
    public void TheFailureSaysWhetherThePathExistedOnTheCallingMachine()
    {
        string path = WriteTempCsv();
        try
        {
            var connection = new SqlConnection();

            var exception = Assert.Throws<InvalidOperationException>(
                () => connection.ImportCsv("t", path));

            Assert.Contains("exists on the calling machine", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------
    // Client-side paths keep their precondition
    // ------------------------------------------------------------------

    /// <summary>
    /// SQLite reads the file itself, so a missing file is something Jaunty can and should diagnose
    /// before touching the database. Moving the check out of the shared entry point must not lose
    /// it on the paths where it was correct.
    /// </summary>
    [Fact]
    public void SqliteImportStillRejectsAMissingFileUpFront()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        Assert.Throws<FileNotFoundException>(() => connection.ImportCsv("t", NonexistentPath()));
    }

    // ------------------------------------------------------------------
    // Options that cannot be honoured now fail loudly - AUD-R26 batch 4, low
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>CsvImportOptions.Encoding</c> is read only where .NET opens the file. On
    /// <c>BULK INSERT</c> the server opens it, so the setting was silently discarded - a caller who
    /// set Latin-1 for a Latin-1 file got UTF-8 behaviour and mojibake, with nothing to indicate
    /// why. <c>NullValue</c> already threw on this path; <c>Encoding</c> now does too, so the type's
    /// two unhonoured options behave alike.
    /// </summary>
    [Fact]
    public void ANonDefaultEncodingIsRejectedRatherThanSilentlyIgnoredOnServerSidePaths()
    {
        var connection = new SqlConnection();

        var exception = Assert.Throws<NotSupportedException>(() => connection.ImportCsv(
            "t", NonexistentPath(), new CsvImportOptions { Encoding = Encoding.GetEncoding("iso-8859-1") }));

        Assert.Contains("Encoding", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Leaving the default must not throw. The option is only unhonourable when it has been set to
    /// something the path cannot deliver, and UTF-8 is what these paths produce anyway.
    /// </summary>
    [Fact]
    public void TheDefaultEncodingIsNotRejected()
    {
        var connection = new SqlConnection();

        // Reaches the command rather than the option guard.
        Assert.Throws<InvalidOperationException>(() => connection.ImportCsv(
            "t", NonexistentPath(), new CsvImportOptions { Encoding = Encoding.UTF8 }));
    }

    // ------------------------------------------------------------------
    // Test double. It has to be named exactly SqlConnection: dialect resolution keys on
    // Type.Name, so StubSqlConnection resolves to nothing and never reaches the import at all.
    // ------------------------------------------------------------------

    private sealed class SqlConnection : IDbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;

        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() => State = ConnectionState.Closed;
        public void Dispose() { }
        public void Open() => State = ConnectionState.Open;

        public IDbCommand CreateCommand() => new StubCommand(text => LastCommandText = text);

        /// <summary>
        /// Stands in for the provider refusing the file. The real exception is a
        /// <c>SqlException</c>, which cannot be constructed from test code; what matters to these
        /// tests is that Jaunty wraps whatever the provider threw and keeps it as the inner
        /// exception.
        /// </summary>
        private sealed class StubCommand : IDbCommand
        {
            private readonly Action<string> _record;

            public StubCommand(Action<string> record) => _record = record;

            public string CommandText { get; set; } = "";
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; }
            public IDbConnection? Connection { get; set; }
            public IDataParameterCollection Parameters { get; } = new StubParameters();
            public IDbTransaction? Transaction { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public IDbDataParameter CreateParameter() => throw new NotSupportedException();
            public void Dispose() { }
            public IDataReader ExecuteReader() => throw new NotSupportedException();
            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
            public object? ExecuteScalar() => throw new NotSupportedException();
            public void Prepare() { }

            public int ExecuteNonQuery()
            {
                _record(CommandText);
                throw new StubProviderException(
                    "Cannot bulk load. The file does not exist or you don't have file access rights.");
            }
        }

        private sealed class StubProviderException : Exception
        {
            public StubProviderException(string message) : base(message) { }
        }

        private sealed class StubParameters : List<object>, IDataParameterCollection
        {
            public object this[string parameterName] { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }
        }
    }
}
