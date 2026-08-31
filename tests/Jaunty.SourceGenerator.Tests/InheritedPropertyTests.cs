using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R26: <c>BuildEntityModel</c> enumerates <c>classSymbol.GetMembers()</c>, which returns only
/// members <em>declared on the type itself</em>. The reflection path enumerates
/// <c>type.GetProperties(BindingFlags.Instance | BindingFlags.Public)</c>
/// (<c>MetadataBuilder.cs:67</c>), which <em>does</em> include inherited properties. A base class
/// holding shared columns - the ordinary audit-columns pattern - therefore maps differently
/// depending on which path is in play.
/// </summary>
public class InheritedPropertyTests
{
    private const string AuditEntitySource = """
        using Jaunty.Attributes;

        namespace InheritProbe;

        public abstract class AuditableEntity
        {
            public System.DateTime CreatedAt { get; set; }
            public string? CreatedBy { get; set; }
        }

        [Table("orders")]
        public partial class Order : AuditableEntity
        {
            public int Id { get; set; }
            public string? Reference { get; set; }
        }
        """;

    /// <summary>
    /// The generator must map what the entity actually exposes, base classes included. Before the
    /// fix CreatedAt and CreatedBy were absent from the generated mapper with no diagnostic, so an
    /// entity that round-tripped under reflection quietly stopped persisting half its columns once
    /// the generator package was referenced.
    /// </summary>
    [Fact]
    public void InheritedProperties_AreMappedByTheGenerator()
    {
        var generated = RunGenerator(AuditEntitySource);

        Assert.Contains("Id", generated, StringComparison.Ordinal);
        Assert.Contains("Reference", generated, StringComparison.Ordinal);
        Assert.Contains("CreatedAt", generated, StringComparison.Ordinal);
        Assert.Contains("CreatedBy", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// object's own members (ToString, GetHashCode, ...) are not properties, but the walk must stop
    /// at object regardless rather than relying on that.
    /// </summary>
    [Fact]
    public void ObjectMembers_AreNotMapped()
    {
        var generated = RunGenerator(AuditEntitySource);

        Assert.DoesNotContain("GetHashCode", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A property redeclared in the derived class must be taken once, from the most derived
    /// declaration - emitting it twice would put a duplicate key in ParameterMap and throw at the
    /// entity's static-constructor time.
    /// </summary>
    [Fact]
    public void AShadowedProperty_IsMappedOnce_FromTheMostDerivedDeclaration()
    {
        var generated = RunGenerator("""
            using Jaunty.Attributes;

            namespace InheritProbe;

            public class BaseRow
            {
                public virtual string? Reference { get; set; }
            }

            [Table("orders")]
            public partial class Order : BaseRow
            {
                public int Id { get; set; }
                public override string? Reference { get; set; }
                public string? Note { get; set; }
            }
            """);

        // Note is an ordinary, singly-declared, non-key property, so it is the right control: the
        // overridden Reference must appear exactly as often as it does. Comparing against the key
        // would not work - a key is emitted through extra constructs of its own.
        var referenceCount = Occurrences(generated, "entity.Reference = ");
        var noteCount = Occurrences(generated, "entity.Note = ");

        Assert.True(noteCount > 0, "control property was not mapped at all:\n" + generated);
        Assert.Equal(noteCount, referenceCount);

        // And the same one-to-one relationship in the column tables, which is where a duplicate
        // would actually throw.
        Assert.Equal(
            Occurrences(generated, "new ColumnInfo(\"Note\""),
            Occurrences(generated, "new ColumnInfo(\"Reference\""));
    }

    private static int Occurrences(string haystack, string needle)
        => haystack.Split([needle], StringSplitOptions.None).Length - 1;

    /// <summary>
    /// The base class is not itself an entity, so it must not get a mapper of its own.
    /// </summary>
    [Fact]
    public void TheBaseClass_DoesNotGetItsOwnMapper()
    {
        var compilation = CSharpCompilation.Create(
            "InheritProbe",
            [CSharpSyntaxTree.ParseText(AuditEntitySource)],
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator())
            .RunGenerators(compilation);

        Assert.Single(driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources));
    }

    /// <summary>
    /// The other half of the comparison, so the divergence is pinned from both sides rather than
    /// asserted about one. Reflection sees the base-class properties that the generator does not.
    /// </summary>
    [Fact]
    public void InheritedProperties_AreSeenByReflection()
    {
        PropertyInfo[] properties = typeof(ProbeOrder)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);

        string[] names = [.. properties.Select(p => p.Name)];

        Assert.Contains("Id", names);
        Assert.Contains("Reference", names);
        Assert.Contains("CreatedAt", names);
        Assert.Contains("CreatedBy", names);
    }

    private abstract class ProbeAuditable
    {
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    private sealed class ProbeOrder : ProbeAuditable
    {
        public int Id { get; set; }
        public string? Reference { get; set; }
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static string RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "InheritProbe",
            [CSharpSyntaxTree.ParseText(source)],
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new global::Jaunty.SourceGenerator.JauntyGenerator().AsSourceGenerator())
            .RunGenerators(compilation);

        return string.Join(
            "\n",
            driver.GetRunResult().Results
                .SelectMany(r => r.GeneratedSources)
                .Select(s => s.SourceText.ToString()));
    }

    /// <summary>
    /// Everything already loaded, plus Jaunty itself - the same approach as
    /// <see cref="GeneratorCachingTests"/>. Without the explicit Jaunty reference the [Table]
    /// attribute resolves to an error type and the generator correctly matches nothing, which
    /// reads as a generator bug rather than a missing reference.
    /// </summary>
    private static IEnumerable<MetadataReference> ReferenceAssemblies()
    {
        IEnumerable<string> loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);

        string[] required = [typeof(global::Jaunty.Attributes.TableAttribute).Assembly.Location];

        foreach (var path in loaded.Concat(required).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return MetadataReference.CreateFromFile(path);
    }
}
