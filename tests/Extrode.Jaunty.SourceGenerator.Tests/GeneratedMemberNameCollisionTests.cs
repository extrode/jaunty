using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R38 generator audit. The generated partial declares fixed member names on the entity, and
/// nothing checked the entity for them: a <c>TableName</c> column property gave CS0102 inside the
/// <c>.g.cs</c>. Such an entity now gets no mapper and JAUNTYGEN004 instead. The sweep covers the
/// other half of the hazard: every bare name the generated code references must survive an entity
/// property of the same name, or be one the generator refuses.
/// </summary>
public class GeneratedMemberNameCollisionTests
{
    private static string Entity(string extra, string baseMembers = "") => $$"""
        using Extrode.Jaunty.Attributes;

        namespace CollisionProbe;

        public enum Kind { A }

        public class EntityBase
        {
        {{baseMembers}}
        }

        [Table("t")]
        public partial class Order : EntityBase
        {
            public int Id { get; set; }
            public Kind Kind { get; set; }
            [EnumStorage(EnumStorage.String)] public Kind Kind2 { get; set; }
            public char Letter { get; set; }
            public global::System.Guid Token { get; set; }
            public decimal Amount { get; set; }
            public global::System.DateOnly Day { get; set; }
            public int? Count { get; set; }
            public string Name { get; set; } = "";
        {{extra}}
        }
        """;

    public static TheoryData<string> GeneratedMemberNames =>
    [
        "ColumnInfo", "ReadFallback", "ReadEntity", "CreateRowMapper", "ThrowIfNonNullableColumnIsNull",
        "BindInsert", "BindUpdate", "BindDelete", "AddParam", "AddEnumParam", "OrdinalMap",
        "TableName", "SchemaName", "PrimaryKeyColumnNames", "InsertColumns", "UpdateColumns",
        "DeleteColumns", "ParameterMap", "EntityColumns",
    ];

    [Theory]
    [MemberData(nameof(GeneratedMemberNames))]
    public void OwnPropertyNamedLikeAGeneratedMember_FallsBackWithADiagnostic(string name)
    {
        AssertRefused(Entity($"    public int {name} {{ get; set; }}"), name);
    }

    [Theory]
    [InlineData("    public void TableName() { }")]
    [InlineData("    public const string SchemaName = \"s\";")]
    [InlineData("    public class OrdinalMap { }")]
    [InlineData("    public static void BindInsert(System.Data.IDbCommand command, Order entity) { }")]
    [InlineData("    public static void BindDelete(System.Data.IDbCommand command, Order entity) { }")]
    [InlineData("    public Order ReadEntity(System.Data.IDataReader reader) => this;")]
    [InlineData("    public static System.Func<System.Data.IDataReader, Order> CreateRowMapper(System.Data.IDataReader reader) => r => new Order();")]
    [InlineData("    private static void AddParam(int value) { }")]
    public void OwnNonPropertyMemberNamedLikeAGeneratedMember_FallsBackWithADiagnostic(string member)
    {
        AssertRefused(Entity(member), "declares a member named");
    }

    [Theory]
    [InlineData("    public static void BindInsert(System.Data.IDbCommand command, object entity) { }")]
    [InlineData("    public static void BindUpdate(System.Data.IDbCommand command) { }")]
    [InlineData("    public static void BindDelete(System.Data.IDataReader command, Order entity) { }")]
    [InlineData("    public static Order ReadEntity(System.Data.IDataReader reader, int skip) => new Order();")]
    [InlineData("    public static int CreateRowMapper(string text) => 0;")]
    public void OverloadOfAPublicGeneratedMethod_IsStillGenerated(string member)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity(member));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.DoesNotContain(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
    }

    [Fact]
    public void MappedBasePropertyNamedLikeAGeneratedMember_FallsBackWithADiagnostic()
    {
        AssertRefused(Entity("", "    public string TableName { get; set; } = \"\";"), "'TableName'");
    }

    [Fact]
    public void InheritedMethodNamedLikeAGeneratedMember_IsOnlyHidden()
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("", "    public static string TableName() => \"x\";"));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.DoesNotContain(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
    }

    [Fact]
    public void EveryNameTheGeneratedCodeReferences_SurvivesASameNamedProperty()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> baseline) = GeneratorHarness.RunAndCompile(Entity(""));
        Assert.Empty(baseline);

        SyntaxNode root = CSharpSyntaxTree.ParseText(sources[0], cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken);
        string[] taken = ["Id", "Kind", "Kind2", "Letter", "Token", "Amount", "Day", "Count", "Name", "Order", "var", "global", "nameof"];
        List<string> names = [.. root.DescendantNodes().OfType<SimpleNameSyntax>()
            .Where(n => n.Parent is not MemberAccessExpressionSyntax access || access.Expression == n)
            .Where(n => n.Parent is not QualifiedNameSyntax qualified || qualified.Left == n)
            .Select(n => n.Identifier.ValueText)
            .Where(n => !taken.Contains(n))
            .Distinct()];
        Assert.True(names.Count > 40, $"Only {names.Count} referenced names found.");

        List<string> broken = [];
        foreach (string name in names)
        {
            (_, _, ImmutableArray<Diagnostic> errors) = GeneratorHarness.RunAndCompile(Entity($"    public int {name} {{ get; set; }}"));
            if (!errors.IsEmpty)
                broken.Add($"{name}: {errors[0].GetMessage()}");
        }

        Assert.Empty(broken);
    }

    private static void AssertRefused(string source, string messageFragment)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(source);

        Assert.Empty(sources);
        Assert.Empty(compileErrors);
        Diagnostic refused = Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
        Assert.Contains(messageFragment, refused.GetMessage(), StringComparison.Ordinal);
    }
}
