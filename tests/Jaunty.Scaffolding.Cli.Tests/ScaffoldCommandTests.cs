using Microsoft.Data.Sqlite;

using System.CommandLine;

using Jaunty.Scaffolding.Cli.Commands;
using Jaunty.Scaffolding.Configuration;

namespace Jaunty.Scaffolding.Cli.Tests;

[Collection("Cli Console")]
public class ScaffoldCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly string _outputDir;

    public ScaffoldCommandTests()
    {
        _connectionString = $"Data Source=ScaffoldCliTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE products (product_id INTEGER PRIMARY KEY, product_name TEXT NOT NULL)";
        cmd.ExecuteNonQuery();

        _outputDir = Path.Combine(Path.GetTempPath(), $"JauntyCliScaffoldTest_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

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
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([]);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_DefaultsMatchDocumentedOptions()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse(["--connection", "x"]);
        Assert.Equal(DatabaseProvider.AutoDetect, result.GetValue<DatabaseProvider>("--provider"));
        Assert.Equal("./Entities", result.GetValue<string>("--output"));
        Assert.Equal("Generated.Entities", result.GetValue<string>("--namespace"));
    }

    [Fact]
    public async Task Invoke_DryRun_ReportsWouldGenerate_WithoutWritingFiles()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--dry-run"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Would generate 1 entity file(s)", outWriter.ToString());
        Assert.False(Directory.Exists(_outputDir));
    }

    [Fact]
    public async Task Invoke_WritesEntityFile_WithDefaultAttributes()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Generated 1 entity file(s)", outWriter.ToString());

        var generatedFile = Path.Combine(_outputDir, "Product.cs");
        Assert.True(File.Exists(generatedFile));
        var code = await File.ReadAllTextAsync(generatedFile);
        Assert.Contains("[Jaunty.Attributes.Table(\"products\")]", code);
        Assert.Contains("[Jaunty.Attributes.Key]", code);
        Assert.Contains("public class Product", code);
    }

    [Fact]
    public async Task Invoke_NoTableAttribute_OmitsTableAttribute()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--no-table-attribute"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.DoesNotContain("Jaunty.Attributes.Table(", code);
    }

    [Fact]
    public async Task Invoke_PartialFlag_GeneratesPartialClass()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--partial"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.Contains("public partial class Product", code);
    }

    [Fact]
    public async Task Invoke_VerboseFlag_PrintsExtraDetails()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--namespace", "My.Entities", "--dry-run", "--verbose"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var output = outWriter.ToString();
        Assert.Contains("Namespace: My.Entities", output);
        Assert.Contains("(Dry run - no files will be written)", output);
    }

    [Fact]
    public async Task Invoke_ScaffoldFailure_ReturnsErrorExitCodeAndMessage()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", @"Data Source=Z:\jaunty-cli-test-nonexistent-dir\test.db",
            "--provider", "SQLite", "--output", _outputDir
        ]);

        (_, StringWriter errWriter) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Error:", errWriter.ToString());
    }
}
