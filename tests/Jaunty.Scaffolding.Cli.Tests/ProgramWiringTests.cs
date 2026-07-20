using Microsoft.Data.Sqlite;

using System.CommandLine;

using Jaunty.Scaffolding.Cli.Commands;

namespace Jaunty.Scaffolding.Cli.Tests;

// Program.cs is top-level statements, so it can't be invoked directly from a test. These tests
// replicate its RootCommand composition (description + both subcommands) to verify the wiring
// itself, since the commands' own behavior is already covered by ListTablesCommandTests/ScaffoldCommandTests.
[Collection("Cli Console")]
public class ProgramWiringTests
{
    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("Jaunty Scaffolding CLI - Generate C# entity classes from database schema");
        rootCommand.Subcommands.Add(new ScaffoldCommand());
        rootCommand.Subcommands.Add(new ListTablesCommand());
        return rootCommand;
    }

    [Fact]
    public void RootCommand_HasBothSubcommands()
    {
        RootCommand root = BuildRootCommand();
        Assert.Contains(root.Subcommands, c => c.Name == "scaffold");
        Assert.Contains(root.Subcommands, c => c.Name == "list-tables");
    }

    [Fact]
    public void RootCommand_UnknownSubcommand_ProducesParseError()
    {
        RootCommand root = BuildRootCommand();
        ParseResult result = root.Parse(["not-a-real-subcommand"]);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task RootCommand_DispatchesToListTables_EndToEnd()
    {
        var connectionString = $"Data Source=ProgramWiringTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE widgets (id INTEGER PRIMARY KEY)";
            cmd.ExecuteNonQuery();
        }

        RootCommand root = BuildRootCommand();
        ParseResult result = root.Parse(["list-tables", "--connection", connectionString, "--provider", "SQLite"]);

        var outWriter = new StringWriter();
        Console.SetOut(outWriter);
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("widgets", outWriter.ToString());
    }
}
