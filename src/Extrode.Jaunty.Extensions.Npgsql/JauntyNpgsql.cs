using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Import;

namespace Extrode.Jaunty.Extensions.Npgsql;

/// <summary>
/// Registers PostgreSQL client-side <c>COPY ... FROM STDIN</c> support with Extrode.Jaunty.
/// </summary>
/// <remarks>
/// <para>
/// Extrode.Jaunty core declares no dependency on a database driver, so it cannot open a copy stream
/// itself. Call <see cref="UseNpgsqlCopy"/> inside <c>JauntyConfig.Configure</c> and <c>CsvImport</c>
/// will stream the file from this process into PostgreSQL; without it, a PostgreSQL CSV import
/// throws and says so rather than falling back silently.
/// </para>
/// <para>
/// There is no reflection here and none left in core for this path. The reference to Npgsql is a
/// real one, the calls are direct, and both trim and NativeAOT keep them.
/// </para>
/// </remarks>
public static class JauntyNpgsql
{
    /// <summary>
    /// Installs the PostgreSQL copy provider into <see cref="JauntyConfigBuilder.CopyImportFactory"/>.
    /// </summary>
    /// <param name="config">The builder passed to <c>JauntyConfig.Configure</c>.</param>
    /// <returns><paramref name="config"/>, for chaining.</returns>
    /// <remarks>
    /// The factory returns <see langword="null"/> for any connection that is not an
    /// <c>NpgsqlConnection</c>, so installing it does not change behaviour on other providers.
    /// </remarks>
    public static JauntyConfigBuilder UseNpgsqlCopy(this JauntyConfigBuilder config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));

        config.CopyImportFactory = NpgsqlCopyImportWriter.Open;
        return config;
    }
}
