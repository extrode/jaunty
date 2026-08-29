using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Npgsql;
using Jaunty.Import;

using Npgsql;

using Xunit;

namespace Jaunty.Tests.Integration.Import;

/// <summary>
/// The contract that replaced the five <c>connection.GetType().GetMethod(...)</c> probes on the
/// PostgreSQL client-side COPY path. The probes returned null under trimming; the
/// <c>BeginTextImport</c> one then fell through to server-side COPY, and the <c>Cancel</c> ones
/// committed the rows written before a mid-file failure while the caller saw an exception saying
/// the import had failed.
/// </summary>
[Collection("Copy Import Provider")]
public class CopyImportProviderTests : IDisposable
{
    private readonly CopyImportFactory? _previousFactory = JauntyConfig.CopyImportFactory;

    public void Dispose()
    {
        JauntyConfig.CopyImportFactory = _previousFactory;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void UseInstallsAFactory()
    {
        JauntyConfig.CopyImportFactory = null;

        JauntyNpgsql.Use();

        Assert.NotNull(JauntyConfig.CopyImportFactory);
    }

    [Fact]
    public void TheNpgsqlFactoryDeclinesConnectionsItDoesNotOwn()
    {
        JauntyNpgsql.Use();
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

        ICopyImportWriter? writer = JauntyConfig.CopyImportFactory!(connection, "COPY x FROM STDIN");

        Assert.Null(writer);
    }

    [Fact]
    public void TheNpgsqlFactoryAcceptsAnNpgsqlConnection()
    {
        JauntyNpgsql.Use();
        using var connection = new NpgsqlConnection("Host=localhost");

        // Not open, so it cannot start a copy - but it must recognise the type and try, rather than
        // decline the way it declines SQLite above. Declining is what the old reflection probe did
        // when trimming removed BeginTextImport, and it was silent.
        Assert.ThrowsAny<Exception>(() =>
            JauntyConfig.CopyImportFactory!(connection, "COPY x FROM STDIN"));
    }

    [Fact]
    public void WithNoProviderRegisteredAPostgresImportSaysSoInsteadOfFallingBack()
    {
        JauntyConfig.CopyImportFactory = null;
        using var connection = new NpgsqlConnection("Host=localhost");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CsvImportExtensions.RequireCopyImportProvider(connection));

        Assert.Contains("Extrode.Jaunty.Extensions.Npgsql", ex.Message);
        Assert.Contains("JauntyNpgsql.Use()", ex.Message);
    }

    [Fact]
    public void TheGuardStaysOutOfTheWayOfEveryOtherProvider()
    {
        JauntyConfig.CopyImportFactory = null;
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

        // Only Npgsql connections need a provider; the server-side COPY FROM fallback that other
        // PostgreSQL-compatible connections take is still a supported path.
        CsvImportExtensions.RequireCopyImportProvider(connection);
    }

    [Fact]
    public void TheGuardIsSilentOnceAProviderIsRegistered()
    {
        JauntyNpgsql.Use();
        using var connection = new NpgsqlConnection("Host=localhost");

        CsvImportExtensions.RequireCopyImportProvider(connection);
    }

    [Fact]
    public void TheImportPathCarriesNoReflectionAnyMore()
    {
        string source = File.ReadAllText(FindCsvImportSource());

        Assert.DoesNotContain("GetMethod(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Reflection;", source, StringComparison.Ordinal);
        Assert.Contains("CopyImportFactory", source, StringComparison.Ordinal);
    }

    private static string FindCsvImportSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "Jaunty", "Import", "CsvImport.cs");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("src/Jaunty/Import/CsvImport.cs was not found above the test binary.");
    }
}
