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
    /// <c>CsvImportPostgreSqlStdinTests</c>'s fake <c>CopyWriter</c> treats <c>Cancel()</c> as a
    /// no-op recorder, so it never reproduces what the real Npgsql driver does: once
    /// <c>Cancel()</c> succeeds, the underlying stream is already ended, and the subsequent
    /// <c>Dispose()</c> from <c>ImportPostgreSqlAsync</c>'s enclosing <c>using (copy)</c> throws
    /// <see cref="ObjectDisposedException"/> - which, thrown from a <c>finally</c> while the
    /// original decode failure is propagating, replaces it. A caller would see "the COPY operation
    /// has already ended" instead of the decode error that actually caused the import to fail.
    /// </summary>
    [Fact]
    public void ImportCsv_WhenTheFileFailsToDecode_SurfacesTheDecodeFailureNotAnObjectDisposedException()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateTable(conn, "npgsql_copy_writer_decode_failure");
        JauntyNpgsql.Use();

        string dir = Path.Combine(Path.GetTempPath(), $"jaunty_pg_live_decode_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("id,note\n1,first\n2,"));
            bytes.AddRange(new byte[] { 0xC3, 0x28, 0xA0, 0xA1 });
            bytes.AddRange(Encoding.ASCII.GetBytes("\n"));
            string path = Path.Combine(dir, "bad.csv");
            File.WriteAllBytes(path, bytes.ToArray());

            var options = new CsvImportOptions
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            };

            Exception thrown = Assert.ThrowsAny<Exception>(
                () => conn.ImportCsv("npgsql_copy_writer_decode_failure", path, options));

            Assert.IsNotType<ObjectDisposedException>(thrown);
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
