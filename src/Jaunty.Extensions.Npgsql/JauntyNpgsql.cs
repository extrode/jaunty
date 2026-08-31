using Jaunty.Configuration;
using Jaunty.Import;

namespace Jaunty.Extensions.Npgsql;

/// <summary>
/// Registers PostgreSQL client-side <c>COPY ... FROM STDIN</c> support with Jaunty.
/// </summary>
/// <remarks>
/// <para>
/// Jaunty core declares no dependency on a database driver, so it cannot open a copy stream
/// itself. Call <see cref="Use"/> once at startup and <c>CsvImport</c> will stream the file from
/// this process into PostgreSQL; without it, a PostgreSQL CSV import throws and says so rather
/// than falling back silently.
/// </para>
/// <para>
/// There is no reflection here and none left in core for this path. The reference to Npgsql is a
/// real one, the calls are direct, and both trim and NativeAOT keep them.
/// </para>
/// </remarks>
public static class JauntyNpgsql
{
    /// <summary>
    /// Installs the PostgreSQL copy provider into <see cref="JauntyConfig.CopyImportFactory"/>.
    /// </summary>
    /// <remarks>
    /// The factory returns <see langword="null"/> for any connection that is not an
    /// <c>NpgsqlConnection</c>, so installing it does not change behaviour on other providers.
    /// </remarks>
    public static void Use() => JauntyConfig.CopyImportFactory = NpgsqlCopyImportWriter.Open;
}
