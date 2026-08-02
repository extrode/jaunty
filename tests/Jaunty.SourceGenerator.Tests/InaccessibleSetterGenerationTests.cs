using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R33-007. The settable-shape guard rejected only get-only and init-only properties, so a
/// property whose setter merely could not be <em>reached</em> from the entity class was mapped -
/// and since AUD-R26 made property discovery walk base types, a base-class
/// <c>public string CreatedBy { get; private set; }</c> is exactly that. The generated
/// <c>entity.CreatedBy = ...</c> then failed with CS0272 inside a <c>.g.cs</c>.
/// <para>
/// The controls matter as much as the failing case here: the guard asks the compilation rather than
/// reading <c>DeclaredAccessibility</c>, so a <c>private set</c> declared on the entity itself, and a
/// <c>protected set</c> inherited from a base, both remain mappable and must stay so.
/// </para>
/// </summary>
public class InaccessibleSetterGenerationTests
{
    private static string Entity(string baseMembers, string ownMembers = "") => $$"""
        using Jaunty.Attributes;

        namespace SetterProbe;

        public abstract class AuditableBase
        {
        {{baseMembers}}
        }

        [Table("orders")]
        public partial class Order : AuditableBase
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        {{ownMembers}}
        }
        """;

    [Fact]
    public void BaseClassPrivateSetter_IsNotMappedAndTheOutputCompiles()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("    public string CreatedBy { get; private set; } = \"\";"));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.DoesNotContain("CreatedBy", sources[0]);
    }

    /// <summary>
    /// An <c>internal set</c> on a base type in the same assembly <em>is</em> reachable, so this one
    /// must still be mapped - the guard is about accessibility at the use site, not about the setter
    /// being non-public.
    /// </summary>
    [Fact]
    public void BaseClassInternalSetterInTheSameAssembly_IsStillMapped()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("    public string CreatedBy { get; internal set; } = \"\";"));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("CreatedBy", sources[0]);
    }

    [Fact]
    public void BaseClassProtectedSetter_IsStillMapped()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("    public string CreatedBy { get; protected set; } = \"\";"));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("CreatedBy", sources[0]);
    }

    /// <summary>
    /// The entity's own <c>private set</c> is reachable from inside the entity, which is where the
    /// generated partial lives - so rejecting it would be a regression, not a fix.
    /// </summary>
    [Fact]
    public void OwnPrivateSetter_IsStillMapped()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("", "    public string Code { get; private set; } = \"\";"));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("Code", sources[0]);
    }

    [Fact]
    public void BaseClassPublicSetter_IsStillMapped()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) =
            GeneratorHarness.RunAndCompile(Entity("    public string CreatedBy { get; set; } = \"\";"));

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("CreatedBy", sources[0]);
    }
}
