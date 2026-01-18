using Microsoft.Data.Sqlite;
using Jaunty.Scaffolding.Configuration;

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

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(2);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        File.Exists(productFile).Should().BeTrue();
        File.Exists(categoryFile).Should().BeTrue();
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

        result.Success.Should().BeTrue();

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var content = await File.ReadAllTextAsync(productFile);

        // Check namespace
        content.Should().Contain("namespace Test.Entities;");

        // Check class declaration
        content.Should().Contain("public class Product");

        // Check table attribute
        content.Should().Contain("[Table(\"products\")]");

        // Check key attribute on primary key
        content.Should().Contain("[Key]");

        // Check database generated attribute on identity column
        content.Should().Contain("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]");

        // Check column attributes for snake_case columns
        content.Should().Contain("[Column(\"product_id\")]");
        content.Should().Contain("[Column(\"product_name\")]");
        content.Should().Contain("[Column(\"unit_price\")]");

        // Check nullable type for nullable column
        content.Should().Contain("public double? UnitPrice");

        // Check non-nullable string has default value
        content.Should().Contain("public string ProductName { get; set; } = string.Empty;");
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

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        File.Exists(productFile).Should().BeTrue();
        File.Exists(categoryFile).Should().BeFalse();
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

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);

        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        File.Exists(productFile).Should().BeTrue();
        File.Exists(categoryFile).Should().BeFalse();
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

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(2);

        // Files should NOT exist because DryRun = true
        var productFile = Path.Combine(_tempOutputDir, "Product.cs");
        var categoryFile = Path.Combine(_tempOutputDir, "Category.cs");

        File.Exists(productFile).Should().BeFalse();
        File.Exists(categoryFile).Should().BeFalse();
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

        result.Success.Should().BeTrue();

        var entityFile = Path.Combine(_tempOutputDir, "ProductEntity.cs");
        File.Exists(entityFile).Should().BeTrue();

        var content = await File.ReadAllTextAsync(entityFile);
        content.Should().Contain("public class ProductEntity");
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

        result.Success.Should().BeTrue();

        var pluralFile = Path.Combine(_tempOutputDir, "Products.cs");
        File.Exists(pluralFile).Should().BeTrue();

        var content = await File.ReadAllTextAsync(pluralFile);
        content.Should().Contain("public class Products");
    }

    [Fact]
    public async Task ListTablesAsync_ReturnsAllTables()
    {
        var scaffolder = new Scaffolder();

        var tables = await scaffolder.ListTablesAsync(_connectionString, DatabaseProvider.SQLite);

        tables.Should().HaveCount(2);
        tables.Select(t => t.Table).Should().Contain("products");
        tables.Select(t => t.Table).Should().Contain("categories");
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

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already exists");

        // Original file should be unchanged
        var content = await File.ReadAllTextAsync(existingFile);
        content.Should().Be("// existing content");
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

        result.Success.Should().BeTrue();

        // File should be overwritten with new content
        var content = await File.ReadAllTextAsync(existingFile);
        content.Should().Contain("public class Product");
        content.Should().NotContain("// existing content");
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
