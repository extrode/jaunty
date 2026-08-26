using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R35-228 (a <c>[Column]</c> whose first constructor argument is not a string), AUD-R35-230
/// (the JAUNTYGEN002 location, which now travels as a <c>LocationInfo</c>), AUD-R35-231
/// (the emitted type argument for Guid/DateTime/TimeSpan/DateTimeOffset was unqualified) and
/// AUD-R35-233 (<c>TypeKeyword</c>'s record, record struct and interface arms). Each is a defect
/// whose symptom is a <c>.g.cs</c> that does not compile or maps the wrong column, so these run the
/// real generator and compile its output.
/// </summary>
public class GeneratorNamespaceAndAttributeShapeTests
{
    [Fact]
    public void AForeignColumnAttributeWithANonStringArgument_LeavesThePropertyNameStanding()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace ColumnProbe;

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class ColumnAttribute : System.Attribute
            {
                public ColumnAttribute(int ordinal) => Ordinal = ordinal;
                public int Ordinal { get; }
            }

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }

                [Column(3)]
                public string? Name { get; set; }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.DoesNotContain("\"3\"", sources[0]);
        Assert.Contains("\"Name\"", sources[0]);
    }

    [Fact]
    public void AStringColumnArgument_IsStillHonoured()
    {
        (ImmutableArray<string> sources, _, _) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace ColumnProbe;

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }

                [Column("order_name")]
                public string? Name { get; set; }
            }
            """);

        Assert.Contains("\"order_name\"", sources[0]);
    }

    [Fact]
    public void AnEntityInANamespaceThatDeclaresItsOwnGuid_StillCompiles()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace ShadowProbe;

            public sealed class Guid { }
            public sealed class TimeSpan { }
            public sealed class DateTimeOffset { }
            public sealed class DateTime { }

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
                public System.Guid Key { get; set; }
                public System.TimeSpan Elapsed { get; set; }
                public System.DateTimeOffset At { get; set; }
                public System.DateTime On { get; set; }
                public System.Guid? MaybeKey { get; set; }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void TheEmittedTypeArguments_AreFullyQualified()
    {
        (ImmutableArray<string> sources, _, _) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace QualifyProbe;

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
                public System.Guid Key { get; set; }
            }
            """);

        Assert.Contains("global::System.Guid", sources[0]);
        Assert.DoesNotContain("<Guid>", sources[0]);
    }

    [Fact]
    public void AnEntityNestedInAPartialRecord_ReDeclaresItAsARecord()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace RecordProbe;

            public partial record Outer
            {
                [Table("orders")]
                public partial class Order
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("partial record Outer", sources[0]);
    }

    [Fact]
    public void AnEntityNestedInAPartialRecordStruct_ReDeclaresItAsARecordStruct()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace RecordProbe;

            public partial record struct Outer
            {
                [Table("orders")]
                public partial class Order
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("partial record struct Outer", sources[0]);
    }

    [Fact]
    public void AnEntityNestedInAPartialInterface_ReDeclaresItAsAnInterface()
    {
        (ImmutableArray<string> sources, _, ImmutableArray<Diagnostic> compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace InterfaceProbe;

            public partial interface IOuter
            {
                [Table("orders")]
                public partial class Order
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
        Assert.Contains("partial interface IOuter", sources[0]);
    }

    /// <summary>
    /// AUD-R35-230: the diagnostic's location now comes from a <c>LocationInfo</c> rather than the
    /// Roslyn <c>Location</c> the model used to hold, so it has to still point at the declaration.
    /// </summary>
    [Fact]
    public void AHandWrittenMapper_ReportsJauntyGen002AtItsOwnDeclaration()
    {
        (_, ImmutableArray<Diagnostic> generatorDiagnostics, _) = GeneratorHarness.RunAndCompile("""
            using System.Data;
            using Jaunty.Interfaces;

            namespace LocationProbe;

            public class HandRolled : IMapped<HandRolled>
            {
                public static HandRolled ReadEntity(IDataReader reader) => new HandRolled();
            }
            """);

        Diagnostic warning = Assert.Single(generatorDiagnostics, d => d.Id == "JAUNTYGEN002");

        Assert.Equal(5, warning.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Contains("HandRolled", warning.GetMessage(), StringComparison.Ordinal);
    }
}
