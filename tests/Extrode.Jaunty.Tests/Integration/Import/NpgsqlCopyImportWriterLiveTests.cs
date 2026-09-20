using System.Text;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Npgsql;
using Extrode.Jaunty.Import;
using Extrode.Jaunty.Tests.Helpers;

using Npgsql;

using Xunit;

namespace Extrode.Jaunty.Tests.Integration.Import;

/// <summary>
/// coverage-gaps-2026-09-20: <see cref="NpgsqlCopyImportWriter"/> itself (as opposed to
/// <c>CsvImportPostgreSqlStdinTests</c>, which fakes out the factory with a recording writer) had
/// no test that ever opened a real <see cref="NpgsqlConnection"/> and drove <c>Writer</c>,
/// <c>Cancel</c>/<c>CancelAsync</c>, and <c>Dispose</c> against a live server.
/// </summary>
[Collection("Copy Import Provider")]
public class NpgsqlCopyImportWriterLiveTests : IDisposable
{
    private readonly CopyImportFactory? _previousFactory = JauntyConfig.CopyImportFactory;

    public void Dispose()
    {
        JauntyConfig.CopyImportFactory = _previousFactory;
        GC.SuppressFinalize(this);
    }

    private static NpgsqlConnection OpenOrSkip()
    {
        if (!TestConfiguration.HasPostgreSql)
        {
            Assert.Skip("PostgreSQL not configured. Set JAUNTY_TEST_POSTGRESQL or ConnectionStrings:PostgreSql.");
        }

        var conn = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"PostgreSQL not reachable: {ex.Message}");
        }
        return conn;
    }

    private static void CreateTable(NpgsqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS "{name}";
            CREATE TABLE "{name}" (id INTEGER PRIMARY KEY, note TEXT NOT NULL)
            """;
        cmd.ExecuteNonQuery();
    }

    private static long Count(NpgsqlConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void Open_WritingThenDisposing_CommitsTheCopiedRows()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_commit");

        using (ICopyImportWriter? writer = NpgsqlCopyImportWriter.Open(conn, "COPY npgsql_copy_writer_commit (id, note) FROM STDIN"))
        {
            Assert.NotNull(writer);
            writer!.Writer.Write("1\tfirst\n");
            writer.Writer.Write("2\tsecond\n");
        }

        Assert.Equal(2, Count(conn, "npgsql_copy_writer_commit"));
    }

    [Fact]
    public void Open_GivenANonNpgsqlConnection_ReturnsNull()
    {
        using var conn = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");

        ICopyImportWriter? writer = NpgsqlCopyImportWriter.Open(conn, "COPY x FROM STDIN");

        Assert.Null(writer);
    }

    [Fact]
    public void Cancel_AfterPartialWrite_LeavesNoRowsCommitted()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_cancel");

        ICopyImportWriter writer = NpgsqlCopyImportWriter.Open(conn, "COPY npgsql_copy_writer_cancel (id, note) FROM STDIN")!;
        writer.Writer.Write("1\tfirst\n");
        writer.Cancel();

        Assert.Equal(0, Count(conn, "npgsql_copy_writer_cancel"));
    }

    [Fact]
    public async Task CancelAsync_AfterPartialWrite_LeavesNoRowsCommitted()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_cancelasync");

        ICopyImportWriter writer = NpgsqlCopyImportWriter.Open(conn, "COPY npgsql_copy_writer_cancelasync (id, note) FROM STDIN")!;
        await writer.Writer.WriteAsync("1\tfirst\n");
        await writer.CancelAsync();

        Assert.Equal(0, Count(conn, "npgsql_copy_writer_cancelasync"));
    }

    /// <summary>
    /// Builds a CSV file large enough to span more than one <c>ImportCsv</c> read buffer
    /// (<c>CopyBufferChars</c> is 4096), with valid rows in the earlier buffers and an invalid
    /// UTF-8 byte sequence only in the last one, so some rows are genuinely streamed and written
    /// to the live COPY before the decode failure hits - not merely a same-buffer failure with
    /// nothing written yet.
    /// </summary>
    private static byte[] BuildMidFileInvalidUtf8Csv()
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes("id,note\n"));
        for (int i = 0; i < 300; i++)
            bytes.AddRange(Encoding.ASCII.GetBytes($"{i},row-{i}\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("9999,"));
        bytes.AddRange(new byte[] { 0xC3, 0x28, 0xA0, 0xA1 });
        bytes.AddRange(Encoding.ASCII.GetBytes("\n"));
        return bytes.ToArray();
    }

    private static CsvImportOptions StrictUtf8Options() => new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
    };

    /// <summary>
    /// <c>CsvImportPostgreSqlStdinTests</c>'s fake <c>CopyWriter</c> treats <c>Cancel()</c> as a
    /// no-op recorder, so it never reproduces what the real Npgsql driver does: once
    /// <c>Cancel()</c> succeeds, the underlying stream is already ended, and the subsequent
    /// <c>Dispose()</c> from <c>ImportPostgreSql</c>'s enclosing scope used to call
    /// <c>Dispose()</c> a second time via an implicit <c>using (copy)</c> finally, which throws
    /// <see cref="ObjectDisposedException"/> - which, thrown from a <c>finally</c> while the
    /// original decode failure is propagating, replaces it. A caller would see "the COPY operation
    /// has already ended" instead of the decode error that actually caused the import to fail, and
    /// the connection would be left mid-copy since nothing cancelled it correctly either.
    /// </summary>
    [Fact]
    public void ImportCsv_WhenTheFileFailsToDecodeMidFile_SurfacesTheDecodeFailureNotAnObjectDisposedException()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_decode_failure");
        JauntyNpgsql.Use();

        string dir = Path.Combine(Path.GetTempPath(), $"jaunty_pg_live_decode_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "bad.csv");
            File.WriteAllBytes(path, BuildMidFileInvalidUtf8Csv());

            Exception thrown = Assert.ThrowsAny<Exception>(
                () => conn.ImportCsv("npgsql_copy_writer_decode_failure", path, StrictUtf8Options()));

            Assert.IsType<DecoderFallbackException>(thrown);
            Assert.Equal(0, Count(conn, "npgsql_copy_writer_decode_failure"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task ImportCsvAsync_WhenTheFileFailsToDecodeMidFile_SurfacesTheDecodeFailureNotAnObjectDisposedException()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_decode_failure_async");
        JauntyNpgsql.Use();

        string dir = Path.Combine(Path.GetTempPath(), $"jaunty_pg_live_decode_async_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "bad-async.csv");
            File.WriteAllBytes(path, BuildMidFileInvalidUtf8Csv());

            Exception thrown = await Assert.ThrowsAnyAsync<Exception>(
                async () => await conn.ImportCsvAsync("npgsql_copy_writer_decode_failure_async", path, StrictUtf8Options()));

            Assert.IsType<DecoderFallbackException>(thrown);
            Assert.Equal(0, Count(conn, "npgsql_copy_writer_decode_failure_async"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void JauntyNpgsqlUse_InstalledFactory_RoundTripsThroughOpen()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_factory");
        JauntyNpgsql.Use();

        using (ICopyImportWriter? writer = JauntyConfig.CopyImportFactory!(conn, "COPY npgsql_copy_writer_factory (id, note) FROM STDIN"))
        {
            Assert.NotNull(writer);
            writer!.Writer.Write("1\tonly\n");
        }

        Assert.Equal(1, Count(conn, "npgsql_copy_writer_factory"));
    }
}
