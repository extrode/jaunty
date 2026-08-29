using System.Data;
using Jaunty.Import;
using Npgsql;

namespace Jaunty.Extensions.Npgsql;

/// <summary>
/// An <see cref="ICopyImportWriter"/> over Npgsql's client-side
/// <c>COPY ... FROM STDIN</c> writer.
/// </summary>
internal sealed class NpgsqlCopyImportWriter : ICopyImportWriter
{
    private readonly NpgsqlCopyTextWriter _writer;

    private NpgsqlCopyImportWriter(NpgsqlCopyTextWriter writer) => _writer = writer;

    /// <summary>
    /// Opens a copy writer if <paramref name="connection"/> is an <see cref="NpgsqlConnection"/>,
    /// otherwise returns <see langword="null"/> so another factory can handle it.
    /// </summary>
    public static ICopyImportWriter? Open(IDbConnection connection, string copyCommand)
    {
        if (connection is not NpgsqlConnection npgsql)
            return null;

        // Npgsql 8.x declares this as TextWriter and 10.x as NpgsqlCopyTextWriter; the runtime type
        // is NpgsqlCopyTextWriter on both, so the cast is real on one TFM and identity on the other.
        var writer = (NpgsqlCopyTextWriter)npgsql.BeginTextImport(copyCommand);
        return new NpgsqlCopyImportWriter(writer);
    }

    public TextWriter Writer => _writer;

    public void Cancel() => _writer.Cancel();

    public async ValueTask CancelAsync() => await _writer.CancelAsync().ConfigureAwait(false);

    public void Dispose() => _writer.Dispose();
}
