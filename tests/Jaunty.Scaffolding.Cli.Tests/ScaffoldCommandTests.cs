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

    // ==========================================
    // AUD-R19 batch-8: ScaffoldCommandTests only exercised --dry-run, --no-table-attribute,
    // --partial, and --verbose end-to-end - a wiring mistake in ScaffoldCommand's SetAction
    // lambda for any other option (e.g. mapped to the wrong ScaffoldOptions property) would
    // not have been caught by any existing test.
    // ==========================================

    private void AddCategoriesTable()
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE categories (category_id INTEGER PRIMARY KEY, description TEXT)";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task Invoke_TablesOption_FiltersToSpecifiedTable()
    {
        AddCategoriesTable();
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--tables", "products"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Generated 1 entity file(s)", outWriter.ToString());
        Assert.True(File.Exists(Path.Combine(_outputDir, "Product.cs")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "Category.cs")));
    }

    [Fact]
    public async Task Invoke_ExcludeTablesOption_OmitsSpecifiedTable()
    {
        AddCategoriesTable();
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--exclude-tables", "categories"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Generated 1 entity file(s)", outWriter.ToString());
        Assert.True(File.Exists(Path.Combine(_outputDir, "Product.cs")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "Category.cs")));
    }

    [Fact]
    public void Parse_SchemasOption_MapsToOptionValue()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse(["--connection", "x", "--schemas", "dbo", "sales"]);
        Assert.Equal(["dbo", "sales"], result.GetValue<string[]>("--schemas"));
    }

    [Fact]
    public async Task Invoke_NoColumnAttribute_OmitsColumnAttribute()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--no-column-attribute"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.DoesNotContain("Jaunty.Attributes.Column(", code);
    }

    [Fact]
    public async Task Invoke_NoKeyAttribute_OmitsKeyAttribute()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--no-key-attribute"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.DoesNotContain("[Jaunty.Attributes.Key]", code);
    }

    [Fact]
    public async Task Invoke_NoDatabaseGenerated_OmitsDatabaseGeneratedAttribute()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--no-database-generated"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.DoesNotContain("Jaunty.Attributes.DatabaseGenerated(", code);
    }

    [Fact]
    public async Task Invoke_DatabaseGenerated_IsIncludedByDefault()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.Contains("Jaunty.Attributes.DatabaseGenerated(Jaunty.Attributes.DatabaseGeneratedOption.Identity)", code);
    }

    [Fact]
    public async Task Invoke_NoNullable_OmitsNullableAnnotation()
    {
        AddCategoriesTable();
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--tables", "categories", "--no-nullable"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Category.cs"));
        Assert.Contains("string Description", code);
        Assert.DoesNotContain("string? Description", code);
    }

    [Fact]
    public async Task Invoke_NullableColumn_UsesNullableReferenceTypeByDefault()
    {
        AddCategoriesTable();
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--tables", "categories"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Category.cs"));
        Assert.Contains("string? Description", code);
    }

    [Fact]
    public async Task Invoke_NoSingularize_KeepsPluralClassName()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--no-singularize"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_outputDir, "Products.cs")));
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Products.cs"));
        Assert.Contains("public class Products", code);
    }

    [Fact]
    public async Task Invoke_ClassPrefix_PrependsToClassName()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--class-prefix", "Db"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_outputDir, "DbProduct.cs")));
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "DbProduct.cs"));
        Assert.Contains("public class DbProduct", code);
    }

    [Fact]
    public async Task Invoke_ClassSuffix_AppendsToClassName()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--class-suffix", "Entity"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_outputDir, "ProductEntity.cs")));
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "ProductEntity.cs"));
        Assert.Contains("public class ProductEntity", code);
    }

    [Fact]
    public async Task Invoke_DataAnnotations_AddsRequiredAttribute()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--data-annotations"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.Contains("[Required]", code);
        Assert.Contains("using System.ComponentModel.DataAnnotations;", code);
    }

    [Fact]
    public async Task Invoke_BlockNamespace_UsesBlockScopedNamespace()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--namespace", "My.Entities", "--block-namespace"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        var code = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Product.cs"));
        Assert.DoesNotContain("namespace My.Entities;", code);
        Assert.Contains("namespace My.Entities", code);
    }

    [Fact]
    public async Task Invoke_ForceFlag_OverwritesExistingFile()
    {
        var command1 = new ScaffoldCommand();
        ParseResult result1 = command1.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir
        ]);
        RedirectConsole();
        Assert.Equal(0, await result1.InvokeAsync());

        var command2 = new ScaffoldCommand();
        ParseResult result2 = command2.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir
        ]);
        (_, StringWriter errWriter2) = RedirectConsole();
        Assert.Equal(1, await result2.InvokeAsync());
        Assert.Contains("already exists", errWriter2.ToString());

        var command3 = new ScaffoldCommand();
        ParseResult result3 = command3.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--force"
        ]);
        (StringWriter outWriter3, _) = RedirectConsole();
        Assert.Equal(0, await result3.InvokeAsync());
        Assert.Contains("Generated 1 entity file(s)", outWriter3.ToString());
    }

    [Fact]
    public async Task Invoke_IncludeForeignKeys_ScaffoldsSuccessfully()
    {
        var command = new ScaffoldCommand();
        ParseResult result = command.Parse([
            "--connection", _connectionString, "--provider", "SQLite",
            "--output", _outputDir, "--include-foreign-keys"
        ]);

        (StringWriter outWriter, _) = RedirectConsole();
        var exitCode = await result.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Generated 1 entity file(s)", outWriter.ToString());
    }
}
