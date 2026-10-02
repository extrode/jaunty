using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Extrode.Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R38 generator audit, then AUD-R38 Q3. The generated members used to sit on the entity, so a
/// <c>TableName</c> column property gave CS0102 and the entity fell back to reflection with
/// JAUNTYGEN004. They now live in the nested <c>Jaunty</c> class, and only that name and
/// <c>ReadEntity</c> remain reserved. The sweep covers the other half of the hazard: every bare name
/// the generated code references must survive an entity property of the same name, or be one the
/// generator refuses.
/// </summary>
public class GeneratedMemberNameCollisionTests
{
    private static string Entity(string extra, string baseMembers = "", string baseAttribute = "") => $$"""
        using Extrode.Jaunty.Attributes;

        namespace CollisionProbe;

        public enum Kind { A }

        {{baseAttribute}}
        public partial class EntityBase
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

    public static TheoryData<string> FormerlyReservedNames =>
    [
        "ColumnInfo", "ReadFallback", "CreateRowMapper", "ThrowIfNonNullableColumnIsNull",
        "BindInsert", "BindUpdate", "BindDelete", "AddParam", "AddEnumParam", "OrdinalMap",
        "TableName", "SchemaName", "PrimaryKeyColumnNames", "InsertColumns", "UpdateColumns",
        "DeleteColumns", "ParameterMap", "EntityColumns", "NameHolder", "Named",
    ];

    [Theory]
    [MemberData(nameof(FormerlyReservedNames))]
    public void OwnPropertyNamedLikeANestedMember_IsGenerated(string name)
    {
        AssertGenerated(Entity($"    public int {name} {{ get; set; }}"));
    }

    [Theory]
    [InlineData("Jaunty")]
    [InlineData("ReadEntity")]
    public void OwnPropertyNamedLikeAnEntityLevelMember_FallsBackWithADiagnostic(string name)
    {
        AssertRefused(Entity($"    public int {name} {{ get; set; }}"), name);
    }

    [Theory]
    [InlineData("    public void Jaunty() { }", "declares a member named 'Jaunty'")]
    [InlineData("    public class Jaunty { }", "declares a member named 'Jaunty'")]
    [InlineData("    public const string Jaunty = \"s\";", "declares a member named 'Jaunty'")]
    [InlineData("    public Order ReadEntity(System.Data.IDataReader reader) => this;", "declares a member named 'ReadEntity'")]
    [InlineData("    public static void BindInsert(System.Data.IDbCommand command, Order entity) { }", "its own 'BindInsert'")]
    [InlineData("    public static void BindUpdate(System.Data.IDbCommand command, Order entity) { }", "its own 'BindUpdate'")]
    [InlineData("    public static void BindDelete(System.Data.IDbCommand command, Order entity) { }", "its own 'BindDelete'")]
    [InlineData("    public static System.Func<System.Data.IDataReader, Order> CreateRowMapper(System.Data.IDataReader reader) => r => new Order();", "its own 'CreateRowMapper'")]
    public void OwnMemberTheGeneratedMapperWouldClashWithOrReplace_FallsBackWithADiagnostic(string member, string reason)
    {
        AssertRefused(Entity(member), reason);
    }

    [Theory]
    [InlineData("    public void TableName() { }")]
    [InlineData("    public const string SchemaName = \"s\";")]
    [InlineData("    public class OrdinalMap { }")]
    [InlineData("    private static void AddParam(int value) { }")]
    [InlineData("    public static string ParameterMap() => \"\";")]
    public void OwnNonPropertyMemberNamedLikeANestedMember_IsGenerated(string member)
    {
        AssertGenerated(Entity(member));
    }

    [Theory]
    [InlineData("    public static void BindInsert(System.Data.IDbCommand command, object entity) { }")]
    [InlineData("    public static void BindUpdate(System.Data.IDbCommand command) { }")]
    [InlineData("    public static void BindDelete(System.Data.IDataReader command, Order entity) { }")]
    [InlineData("    public static Order ReadEntity(System.Data.IDataReader reader, int skip) => new Order();")]
    [InlineData("    public static int CreateRowMapper(string text) => 0;")]
    [InlineData("    public int BindInsert { get; set; }")]
    public void OverloadOrPropertyNamedLikeAPublicGeneratedMethod_IsStillGenerated(string member)
    {
        AssertGenerated(Entity(member));
    }

    [Theory]
    [InlineData("Jaunty")]
    [InlineData("ReadEntity")]
    public void MappedBasePropertyNamedLikeAnEntityLevelMember_FallsBackWithADiagnostic(string name)
    {
        AssertRefused(Entity("", $"    public string {name} {{ get; set; }} = \"\";"), $"'{name}'");
    }

    [Fact]
    public void MappedBasePropertyNamedTableName_IsGenerated()
    {
        AssertGenerated(Entity("", "    public string TableName { get; set; } = \"\";"));
    }

    [Fact]
    public void EntityNamedJaunty_FallsBackWithADiagnostic()
    {
        const string source = """
            using Extrode.Jaunty.Attributes;

            namespace CollisionProbe;

            [Table("t")]
            public partial class Jaunty
            {
                public int Id { get; set; }
            }
            """;

        AssertRefused(source, "it is named 'Jaunty'");
    }

    [Fact]
    public void InheritedMethodNamedJaunty_IsHiddenWithNew()
    {
        string generated = AssertGenerated(Entity("", "    public static string Jaunty() => \"x\";"))[0];

        Assert.Contains("public new static class Jaunty", generated, StringComparison.Ordinal);
        Assert.Contains("public static Order ReadEntity(", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedBaseEntity_IsHiddenWithNewOnBothEntityLevelMembers()
    {
        ImmutableArray<string> sources = AssertGenerated(Entity("", "    public int BaseId { get; set; }", "[Table(\"b\")]"), expectedSources: 2);
        string derived = Assert.Single(sources, s => s.Contains("partial class Order", StringComparison.Ordinal));
        string baseSource = Assert.Single(sources, s => s.Contains("partial class EntityBase", StringComparison.Ordinal));

        Assert.Contains("public new static class Jaunty", derived, StringComparison.Ordinal);
        Assert.Contains("public new static Order ReadEntity(", derived, StringComparison.Ordinal);
        Assert.Contains("public static class Jaunty", baseSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new static", baseSource, StringComparison.Ordinal);
    }

    [Fact]
    public void UnrelatedBaseClass_GetsNoNewModifier()
    {
        string generated = AssertGenerated(Entity(""))[0];

        Assert.DoesNotContain("new static", generated, StringComparison.Ordinal);
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

    private static ImmutableArray<string> AssertGenerated(string source, int expectedSources = 1)
    {
        (ImmutableArray<string> sources, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<Diagnostic> compileErrors, Compilation output) =
            GeneratorHarness.Run(source);

        Assert.Equal(expectedSources, sources.Length);
        Assert.Empty(compileErrors);
        Assert.DoesNotContain(generatorDiagnostics, d => d.Id == "JAUNTYGEN004");
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Id is "CS0108" or "CS0109" or "CS0114");
        return sources;
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
