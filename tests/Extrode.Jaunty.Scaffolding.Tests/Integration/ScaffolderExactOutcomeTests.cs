using Microsoft.Data.Sqlite;

using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.CodeGeneration;
using Extrode.Jaunty.Scaffolding.Configuration;
using Extrode.Jaunty.Scaffolding.Schema;

namespace Extrode.Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// Exact <c>ScaffoldResult</c> errors, explicit-provider versus auto-detected routing, and how an
/// exception chain thrown during generation is flattened into one message.
/// </summary>
public class ScaffolderExactOutcomeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly string _outputDir;

    public ScaffolderExactOutcomeTests()
    {
        _connectionString = $"Data Source=ExactOutcome_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        _outputDir = Path.Combine(Path.GetTempPath(), $"JauntyExactOutcome_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Exec(string sql)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private ScaffoldOptions Options(DatabaseProvider provider = DatabaseProvider.AutoDetect, string? connectionString = null) =>
        new()
        {
            ConnectionString = connectionString ?? _connectionString,
            Provider = provider,
            OutputDirectory = _outputDir,
            Namespace = "Test.Entities"
        };

    private sealed class ThrowingGenerator(Exception toThrow) : ICodeGenerator
    {
        public string GenerateEntity(TableSchema table, CodeGeneratorOptions options) => throw toThrow;
    }

    private async Task<string?> ErrorFromGeneratorThrowing(Exception ex)
    {
        Exec("CREATE TABLE widgets (id INTEGER PRIMARY KEY)");
        ScaffoldOptions options = Options();
        options.CodeGenerator = new ThrowingGenerator(ex);
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(options);
        Assert.False(result.Success);
        return result.Error;
    }

    private static Exception Chain(params string[] messages)
    {
        Exception? inner = null;
        for (int i = messages.Length - 1; i >= 0; i--)
            inner = new InvalidOperationException(messages[i], inner);
        return inner!;
    }

    [Fact]
    public async Task AutoDetect_ResolvesASqliteConnectionString()
    {
        Exec("CREATE TABLE widgets (id INTEGER PRIMARY KEY)");

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(Options());

        Assert.True(result.Success, result.Error);
        Assert.Equal([Path.Combine(_outputDir, "Widget.cs")], result.GeneratedFiles);
    }

    [Fact]
    public async Task AnExplicitProvider_IsUsedEvenWhenDetectionWouldDisagree()
    {
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(Options(DatabaseProvider.SQLite, "Mode=Memory"));

        Assert.Equal("No tables found matching the specified criteria.", result.Error);
    }

    [Fact]
    public async Task ListTables_WithAnExplicitProvider_SkipsDetection()
    {
        IReadOnlyList<(string Schema, string Table)> tables =
            await new Scaffolder().ListTablesAsync("Mode=Memory", DatabaseProvider.SQLite);

        Assert.Empty(tables);
    }

    [Fact]
    public async Task TwoTablesSingularizingToOneClass_AreBothNamed()
    {
        Exec("CREATE TABLE product (id INTEGER PRIMARY KEY)");
        Exec("CREATE TABLE products (id INTEGER PRIMARY KEY)");

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(Options());

        Assert.Equal(
            "Multiple tables map to the same generated class name: Product <- [product, products]. " +
            "Use ClassPrefix/ClassSuffix, disable Singularize, or exclude one of the tables.",
            result.Error);
    }

    [Fact]
    public async Task AnExistingOutputFile_IsNamedWithTheForceHint()
    {
        Exec("CREATE TABLE widgets (id INTEGER PRIMARY KEY)");
        Directory.CreateDirectory(_outputDir);
        string existing = Path.Combine(_outputDir, "Widget.cs");
        await File.WriteAllTextAsync(existing, "// mine");

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(Options());

        Assert.Equal($"File(s) already exists: {existing}. Use --force to overwrite.", result.Error);
    }

    [Fact]
    public async Task NullOptions_AreReportedAsSuch()
    {
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(null!);

        Assert.Equal("Scaffold options are required. (Parameter 'options')", result.Error);
    }

    [Fact]
    public async Task AnInvalidNamespace_SaysWhatAValidOneIs()
    {
        ScaffoldOptions options = Options();
        options.Namespace = "1bad";

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(options);

        Assert.Equal(
            "Namespace '1bad' is not a valid C# namespace. It must be a dot-separated sequence of " +
            "identifiers, each starting with a letter or underscore. (Parameter 'options')",
            result.Error);
    }

    [Fact]
    public async Task AnUndefinedProvider_IsNamed()
    {
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(Options((DatabaseProvider)99));

        Assert.Equal("Unknown provider: 99", result.Error);
    }

    [Fact]
    public async Task AnExceptionChain_IsJoinedOuterToInner()
    {
        Assert.Equal("outer -> middle -> root", await ErrorFromGeneratorThrowing(Chain("outer", "middle", "root")));
    }

    [Fact]
    public async Task ABlankOrAlreadyQuotedInnerMessage_IsNotRepeated()
    {
        Assert.Equal("failed: root cause -> last", await ErrorFromGeneratorThrowing(Chain("failed: root cause", " ", "root cause", "last")));
    }

    [Fact]
    public async Task AnExceptionChain_StopsAfterFiveInnerLevels()
    {
        Assert.Equal("e0 -> e1 -> e2 -> e3 -> e4 -> e5", await ErrorFromGeneratorThrowing(Chain("e0", "e1", "e2", "e3", "e4", "e5", "e6", "e7")));
    }
}
