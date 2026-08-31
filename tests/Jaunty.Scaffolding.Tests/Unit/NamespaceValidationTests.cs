using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Configuration;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R26: the namespace is interpolated straight into <c>namespace {0};</c> and was never
/// validated. Malformed values produced a file the user could not compile, and a value containing
/// a semicolon injected arbitrary top-level C# into every generated file while still parsing
/// cleanly.
/// </summary>
public class NamespaceValidationTests
{
    private static TableSchema Table() => new()
    {
        SchemaName = "",
        TableName = "items",
        Columns =
        [
            new ColumnSchema
            {
                ColumnName = "id",
                DataType = "INTEGER",
                IsNullable = false,
                IsPrimaryKey = true,
                IsIdentity = true,
                IsComputed = false,
                OrdinalPosition = 1
            }
        ],
        PrimaryKey = null,
        ForeignKeys = []
    };

    // ------------------------------------------------------------------
    // IsValidNamespace
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Generated.Entities")]
    [InlineData("MyApp")]
    [InlineData("_Leading.Underscore")]
    [InlineData("With1Digits2.Inside3")]
    [InlineData("@class.@namespace")]   // verbatim identifiers are the legal way to use a keyword
    public void WellFormedNamespaces_AreAccepted(string ns)
        => Assert.True(NamingHelper.IsValidNamespace(ns));

    [Theory]
    [InlineData("My Namespace")]
    [InlineData("123Bad")]
    [InlineData("Foo-Bar")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Foo..Bar")]
    [InlineData(".Leading")]
    [InlineData("Trailing.")]
    [InlineData("namespace")]           // an unescaped keyword
    [InlineData("Foo.class.Bar")]
    [InlineData("Foo.Bar;public class Pwn{}//")]
    [InlineData("Foo.Bar{}")]
    [InlineData("Foo/Bar")]
    [InlineData("Foo\nBar")]
    public void MalformedNamespaces_AreRejected(string ns)
        => Assert.False(NamingHelper.IsValidNamespace(ns));

    // ------------------------------------------------------------------
    // The generator refuses rather than emitting
    // ------------------------------------------------------------------

    /// <summary>
    /// Measured before the fix: this generated <c>namespace Foo.Bar;public class Pwn{}//;</c>
    /// with **zero** parse errors, so every scaffolded file silently declared a public type the
    /// user never asked for. That is the case that makes this more than a typo guard -
    /// <c>ScaffoldOptions</c> is public library API, so a host may pass a namespace it derived
    /// from configuration rather than one a developer typed.
    /// </summary>
    [Fact]
    public void ASemicolonInTheNamespace_IsRejectedRatherThanInjected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new EntityCodeGenerator(new SQLiteTypeMapper())
                .GenerateEntity(Table(), new CodeGeneratorOptions { Namespace = "Foo.Bar;public class Pwn{}//" }));

        Assert.Contains("not a valid C# namespace", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("My Namespace")]
    [InlineData("123Bad")]
    [InlineData("Foo-Bar")]
    public void AMalformedNamespace_IsRejectedByTheGenerator(string ns)
        => Assert.Throws<ArgumentException>(() =>
            new EntityCodeGenerator(new SQLiteTypeMapper())
                .GenerateEntity(Table(), new CodeGeneratorOptions { Namespace = ns }));

    [Fact]
    public void AValidNamespace_StillGeneratesParseableCode()
    {
        var code = new EntityCodeGenerator(new SQLiteTypeMapper())
            .GenerateEntity(Table(), new CodeGeneratorOptions { Namespace = "Generated.Entities" });

        Diagnostic[] errors = CSharpSyntaxTree.ParseText(code).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("namespace Generated.Entities;", code, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Reported before a connection is opened
    // ------------------------------------------------------------------

    /// <summary>
    /// The connection string here is deliberately unusable. The namespace must be rejected first,
    /// so the user is told about the option they actually got wrong rather than about a database
    /// they cannot reach.
    /// </summary>
    [Fact]
    public async Task ScaffoldAsync_ReportsTheBadNamespace_BeforeTouchingTheDatabase()
    {
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(
            new ScaffoldOptions
            {
                ConnectionString = "Data Source=/nonexistent-directory/x.db;Mode=ReadOnly",
                Provider = DatabaseProvider.SQLite,
                OutputDirectory = Path.Combine(Path.GetTempPath(), "jaunty-scaffold-should-not-exist"),
                Namespace = "My Namespace",
                DryRun = true
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("not a valid C# namespace", result.Error!, StringComparison.Ordinal);
        Assert.DoesNotContain("unable to open database file", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
