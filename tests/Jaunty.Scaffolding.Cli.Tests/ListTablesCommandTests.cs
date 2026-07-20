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
        var command = new ListTablesCommand();
        ParseResult result = command.Parse([
            "--connection", @"Data Source=Z:\jaunty-cli-test-nonexistent-dir\test.db",
            "--provider", "SQLite"
        ]);

        (_, StringWriter errWriter) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Error:", errWriter.ToString());
    }
}
