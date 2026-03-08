using Microsoft.Data.Sqlite;
using Jaunty.Scaffolding.Configuration;
using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

public class ScaffolderIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly string _tempOutputDir;

    public ScaffolderIntegrationTests()
    {
        _connectionString = "Data Source=InMemoryScaffoldTest;Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        _tempOutputDir = Path.Combine(Path.GetTempPath(), $"JauntyScaffoldTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempOutputDir);

        CreateTestTables();
    }

    private void CreateTestTables()
    {
        using var cmd = _connection.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE products (
                product_id INTEGER PRIMARY KEY AUTOINCREMENT,
                product_name TEXT NOT NULL,
                unit_price REAL,
                discontinued INTEGER NOT NULL DEFAULT 0
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            )";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ScaffoldAsync_GeneratesEntityFiles()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities"
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);
        Assert.Equal(2, result.GeneratedFiles.Count);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        Assert.True(File.Exists(productFile));
        Assert.True(File.Exists(categoryFile));
    }

    [Fact]
    public async Task ScaffoldAsync_GeneratesCorrectCode()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            Singularize = true,
            GenerateTableAttribute = true,
            GenerateColumnAttribute = true,
            GenerateKeyAttribute = true,
            GenerateDatabaseGeneratedAttribute = true,
            UseNullableReferenceTypes = true
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var content = await File.ReadAllTextAsync(productFile);

        // Check namespace
        Assert.Contains("namespace Test.Entities;", content);

        // Check class declaration
        Assert.Contains("public class Product", content);

        // Check table attribute
        Assert.Contains("[Table(\"products\")]", content);

        // Check key attribute on primary key
        Assert.Contains("[Key]", content);

        // Check database generated attribute on identity column
        Assert.Contains("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]", content);

        // Check column attributes for snake_case columns
        Assert.Contains("[Column(\"product_id\")]", content);
        Assert.Contains("[Column(\"product_name\")]", content);
        Assert.Contains("[Column(\"unit_price\")]", content);

        // Check nullable type for nullable column
        Assert.Contains("public double? UnitPrice", content);

        // Check non-nullable string has default value
        Assert.Contains("public string ProductName { get; set; } = string.Empty;", content);
    }

    [Fact]
    public async Task ScaffoldAsync_WithIncludeTables_OnlyScaffoldsSpecifiedTables()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            IncludeTables = ["products"]
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);
        Assert.Single(result.GeneratedFiles);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        Assert.True(File.Exists(productFile));
        Assert.False(File.Exists(categoryFile));
    }

    [Fact]
    public async Task ScaffoldAsync_WithExcludeTables_ExcludesSpecifiedTables()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            ExcludeTables = ["categories"]
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);
        Assert.Single(result.GeneratedFiles);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        Assert.True(File.Exists(productFile));
        Assert.False(File.Exists(categoryFile));
    }

    [Fact]
    public async Task ScaffoldAsync_DryRun_DoesNotWriteFiles()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            DryRun = true
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);
        Assert.Equal(2, result.GeneratedFiles.Count);

        // Files should NOT exist because DryRun = true
        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        Assert.False(File.Exists(productFile));
        Assert.False(File.Exists(categoryFile));
    }

    [Fact]
    public async Task ScaffoldAsync_WithClassSuffix_AddsClassSuffix()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            IncludeTables = ["products"],
            ClassSuffix = "Entity"
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);

        var entityFile = Path.Combine(_tempOutputDir, "ProductEntity.cs");
        Assert.True(File.Exists(entityFile));

        var content = await File.ReadAllTextAsync(entityFile);
        Assert.Contains("public class ProductEntity", content);
    }

    [Fact]
    public async Task ScaffoldAsync_NoSingularize_KeepsPluralName()
    {
        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            IncludeTables = ["products"],
            Singularize = false
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);

        var pluralFile = Path.Combine(_tempOutputDir, "Products.cs");
        Assert.True(File.Exists(pluralFile));

        var content = await File.ReadAllTextAsync(pluralFile);
        Assert.Contains("public class Products", content);
    }

    [Fact]
    public async Task ListTablesAsync_ReturnsAllTables()
    {
        var scaffolder = new Scaffolder();

        var tables = await scaffolder.ListTablesAsync(_connectionString, DatabaseProvider.SQLite);

        Assert.Equal(2, tables.Count);
        Assert.Contains(tables, t => t.Table == "products");
        Assert.Contains(tables, t => t.Table == "categories");
    }

    [Fact]
    public async Task ScaffoldAsync_ExistingFile_WithoutForce_Fails()
    {
        // First, create an existing file
        var existingFile = Path.Combine(_tempOutputDir, "Product.cs");
        await File.WriteAllTextAsync(existingFile, "// existing content");

        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            IncludeTables = ["products"],
            Force = false
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);

        // Original file should be unchanged
        var content = await File.ReadAllTextAsync(existingFile);
        Assert.Equal("// existing content", content);
    }

    [Fact]
    public async Task ScaffoldAsync_ExistingFile_WithForce_Overwrites()
    {
        // First, create an existing file
        var existingFile = Path.Combine(_tempOutputDir, "Product.cs");
        await File.WriteAllTextAsync(existingFile, "// existing content");

        var scaffolder = new Scaffolder();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _tempOutputDir,
            Namespace = "Test.Entities",
            IncludeTables = ["products"],
            Force = true
        };

        var result = await scaffolder.ScaffoldAsync(options);

        Assert.True(result.Success);

        // File should be overwritten with new content
        var content = await File.ReadAllTextAsync(existingFile);
        Assert.Contains("public class Product", content);
        Assert.DoesNotContain("// existing content", content);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();

        // Clean up temp directory
        if (Directory.Exists(_tempOutputDir))
        {
            try
            {
                Directory.Delete(_tempOutputDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}