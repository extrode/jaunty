using System.Data;
using System.Data.Common;
using System.Text;

namespace Jaunty.Tests.Integration.Import;

/// <summary>
/// AUD-R34-010 and AUD-R34-011, the two defects on the PostgreSQL <c>COPY ... FROM STDIN</c> path.
/// <para>
/// The path is reached by dialect - <c>SqlDialectFactory</c> resolves by connection type
/// <em>name</em> - and streams the file through whatever <c>BeginTextImport(string)</c> returns,
/// found by reflection. A type named <c>NpgsqlConnection</c> exposing that method therefore
/// exercises the real code with no server: the assertions here are about what Jaunty writes to the
/// copy stream and what it does to that stream when the read fails, both of which are Jaunty's
/// alone.
/// </para>
/// </summary>
public class CsvImportPostgreSqlStdinTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"jaunty_pg_stdin_{Guid.NewGuid():N}");

    public CsvImportPostgreSqlStdinTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string WriteFile(string name, string content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private string WriteBytes(string name, byte[] content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private const string EmbeddedLf = "id,note\n1,\"first line\nsecond line\"\n2,plain\n";
    private const string EmbeddedCrLf = "id,note\r\n1,\"first line\r\nsecond line\"\r\n2,plain\r\n";

    [Fact]
    public void ImportCsv_EmbeddedLineFeed_IsStreamedVerbatim()
    {
        string path = WriteFile("lf.csv", EmbeddedLf);
        using var connection = new NpgsqlConnection();

        connection.ImportCsv("notes", path);

        Assert.Equal(EmbeddedLf, connection.LastWriter!.Written);
    }

    [Fact]
    public void ImportCsv_EmbeddedCarriageReturnLineFeed_IsStreamedVerbatim()
    {
        string path = WriteFile("crlf.csv", EmbeddedCrLf);
        using var connection = new NpgsqlConnection();

        connection.ImportCsv("notes", path);

        Assert.Equal(EmbeddedCrLf, connection.LastWriter!.Written);
    }

    [Fact]
    public async Task ImportCsvAsync_EmbeddedLineFeed_IsStreamedVerbatim()
    {
        string path = WriteFile("lf-async.csv", EmbeddedLf);
        using var connection = new NpgsqlConnection();

        await connection.ImportCsvAsync("notes", path);

        Assert.Equal(EmbeddedLf, connection.LastWriter!.Written);
    }

    [Fact]
    public async Task ImportCsvAsync_EmbeddedCarriageReturnLineFeed_IsStreamedVerbatim()
    {
        string path = WriteFile("crlf-async.csv", EmbeddedCrLf);
        using var connection = new NpgsqlConnection();

        await connection.ImportCsvAsync("notes", path);

        Assert.Equal(EmbeddedCrLf, connection.LastWriter!.Written);
    }

    [Fact]
    public void ImportCsv_LongerThanOneBuffer_IsStreamedVerbatim()
    {
        var builder = new StringBuilder("id,note\n");
        for (int i = 0; i < 500; i++) builder.Append(i).Append(",\"line ").Append(i).Append('\n').Append("cont\"\n");
        string content = builder.ToString();

        string path = WriteFile("big.csv", content);
        using var connection = new NpgsqlConnection();

        connection.ImportCsv("notes", path);

        Assert.True(content.Length > 4096, "the file must span more than one copy buffer to be worth asserting");
        Assert.Equal(content, connection.LastWriter!.Written);
    }

    [Fact]
    public void ImportCsv_WhenSuccessful_DoesNotCancelTheCopy()
    {
        string path = WriteFile("ok.csv", EmbeddedLf);
        using var connection = new NpgsqlConnection();

        connection.ImportCsv("notes", path);

        Assert.False(connection.LastWriter!.Cancelled);
    }

    /// <summary>
    /// A decoding failure part-way through the file under a strict encoding. Npgsql's copy writer
    /// completes the COPY on Dispose, so unless Jaunty cancels it the rows written before the
    /// failure are committed while the caller only sees the exception.
    /// </summary>
    [Fact]
    public void ImportCsv_WhenTheFileFailsToDecode_CancelsTheCopy()
    {
        string path = WriteInvalidUtf8("bad.csv");
        using var connection = new NpgsqlConnection();

        Assert.ThrowsAny<Exception>(() =>
            connection.ImportCsv("notes", path, StrictUtf8Options()));

        Assert.True(connection.LastWriter!.Cancelled,
            "the copy was disposed without being cancelled, so the rows written before the failure are committed");
    }

    [Fact]
    public async Task ImportCsvAsync_WhenTheFileFailsToDecode_CancelsTheCopy()
    {
        string path = WriteInvalidUtf8("bad-async.csv");
        using var connection = new NpgsqlConnection();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.ImportCsvAsync("notes", path, StrictUtf8Options()));

        Assert.True(connection.LastWriter!.Cancelled,
            "the copy was disposed without being cancelled, so the rows written before the failure are committed");
    }

    [Fact]
    public void ImportCsv_WhenTheFileFailsToDecode_StillSurfacesTheOriginalFailure()
    {
        string path = WriteInvalidUtf8("bad-rethrow.csv");
        using var connection = new NpgsqlConnection();
        connection.CancelThrows = true;

        Exception thrown = Assert.ThrowsAny<Exception>(() =>
            connection.ImportCsv("notes", path, StrictUtf8Options()));

        Assert.IsNotType<InvalidOperationException>(thrown);
    }

    private static CsvImportOptions StrictUtf8Options() => new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
    };

    private string WriteInvalidUtf8(string name)
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes("id,note\n1,first\n2,"));
        bytes.AddRange(new byte[] { 0xC3, 0x28, 0xA0, 0xA1 });
        bytes.AddRange(Encoding.ASCII.GetBytes("\n"));
        return WriteBytes(name, bytes.ToArray());
    }

    /// <summary>
    /// Named for the type <c>SqlDialectFactory</c> resolves PostgreSQL by, and exposing the
    /// <c>BeginTextImport(string)</c> that <c>ImportPostgreSql</c> probes for.
    /// </summary>
    private sealed class NpgsqlConnection : DbConnection
    {
        public CopyWriter? LastWriter { get; private set; }
        public bool CancelThrows { get; set; }

        public TextWriter BeginTextImport(string copyCommand)
        {
            CopyCommand = copyCommand;
            LastWriter = new CopyWriter(CancelThrows);
            return LastWriter;
        }

        public string? CopyCommand { get; private set; }

#pragma warning disable CS8765
        public override string ConnectionString { get; set; } = "Host=fake";
#pragma warning restore CS8765

        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotSupportedException("the STDIN path must not fall through to server-side COPY FROM");
    }

    /// <summary>
    /// Stands in for <c>NpgsqlCopyTextWriter</c>: it records what was written, completes on
    /// Dispose, and aborts only on the explicit <c>Cancel()</c> the real one requires.
    /// </summary>
    private sealed class CopyWriter : TextWriter
    {
        private readonly StringBuilder _written = new();
        private readonly bool _cancelThrows;

        public CopyWriter(bool cancelThrows) => _cancelThrows = cancelThrows;

        public string Written => _written.ToString();
        public bool Cancelled { get; private set; }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => _written.Append(value);

        public void Cancel()
        {
            if (_cancelThrows) throw new InvalidOperationException("cancel refused by the test");
            Cancelled = true;
        }
    }
}
