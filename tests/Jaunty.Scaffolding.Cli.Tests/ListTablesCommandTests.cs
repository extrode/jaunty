using Microsoft.Data.Sqlite;

using System.CommandLine;

using Jaunty.Scaffolding.Cli.Commands;
using Jaunty.Scaffolding.Configuration;

namespace Jaunty.Scaffolding.Cli.Tests;

[Collection("Cli Console")]
public class ListTablesCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ListTablesCommandTests()
    {
        _connectionString = $"Data Source=ListTablesCliTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE products (id INTEGER PRIMARY KEY, name TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE categories (id INTEGER PRIMARY KEY, name TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private static (StringWriter Out, StringWriter Error) RedirectConsole()
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        return (outWriter, errWriter);
    }

    [Fact]
    public void Parse_MissingRequiredConnection_ProducesError()
    {
        var command = new ListTablesCommand();
        ParseResult result = command.Parse([]);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_ProviderDefaultsToAutoDetect()
    {
        var command = new ListTablesCommand();
        ParseResult result = command.Parse(["--connection", "x"]);
        Assert.Equal(DatabaseProvider.AutoDetect, result.GetValue<DatabaseProvider>("--provider"));
    }

    // AUD-R35-266: the twin of ScaffoldCommandTests' help-text check - both commands carried the
    // identical string with the identical omission.
    [Fact]
    public void ProviderOption_HelpText_NamesAutoDetectAndEveryOtherValue()
    {
        var command = new ListTablesCommand();
        Option provider = command.Options.Single(o => o.Name == "--provider");

        Assert.NotNull(provider.Description);
        foreach (var name in Enum.GetNames<DatabaseProvider>())
            Assert.Contains(name, provider.Description, StringComparison.Ordinal);
    }

    // AUD-R35-267: cancellation is caught by name here, not swallowed by the catch-all, so both
    // commands report a Ctrl-C the same way.
    [Fact]
    public async Task Invoke_Cancelled_ReportsTheCancellation()
    {
        var command = new ListTablesCommand();
        ParseResult result = command.Parse(["--connection", _connectionString, "--provider", "SQLite"]);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        (_, StringWriter errWriter) = RedirectConsole();
        var exitCode = await result.InvokeAsync(cancellationToken: cts.Token);

        Assert.Equal(1, exitCode);
        Assert.Contains("Error: Operation canceled.", errWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SchemasOption_AcceptsMultipleValues()
    {
        var command = new ListTablesCommand();
        ParseResult result = command.Parse(["--connection", "x", "--schemas", "sales", "hr"]);
        Assert.Equal(["sales", "hr"], result.GetValue<string[]>("--schemas"));
    }

    [Fact]
    public async Task Invoke_ListsAllTables_WhenNoSchemaFilter()
    {
        var command = new ListTablesCommand();
        ParseResult result = command.Parse(["--connection", _connectionString, "--provider", "SQLite"]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Found 2 table(s)", output);
        Assert.Contains("categories", output);
        Assert.Contains("products", output);
    }

    [Fact]
    public async Task Invoke_FiltersBySchema_ExcludesNonMatchingTables()
    {
        // SQLite has no real schemas, so every table reports an empty schema - filtering by
        // a schema name that isn't "" excludes everything.
        var command = new ListTablesCommand();
        ParseResult result = command.Parse(["--connection", _connectionString, "--provider", "SQLite", "--schemas", "nonexistent"]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Found 0 table(s)", outWriter.ToString());
    }

    [Fact]
    public async Task Invoke_ConnectionFailure_ReturnsErrorExitCodeAndMessage()
    {
        // The unopenable path has to be unopenable on the platform running the test. This was
        // hardcoded to Z:\jaunty-cli-test-nonexistent-dir\test.db, which is a missing drive on
        // Windows but an ordinary (if oddly named) relative filename on macOS and Linux - SQLite
        // created it in the working directory, the command succeeded, and the test asserting exit
        // code 1 got 0. Building the path from a real temp root plus a directory that does not
        // exist fails to open everywhere.
        string missingDirectory = Path.Combine(
            Path.GetTempPath(), "jaunty-cli-test-nonexistent-" + Guid.NewGuid().ToString("N"));

        var command = new ListTablesCommand();
        ParseResult result = command.Parse([
            "--connection", $"Data Source={Path.Combine(missingDirectory, "test.db")}",
            "--provider", "SQLite"
        ]);

        (_, StringWriter errWriter) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Error:", errWriter.ToString());
    }
}
