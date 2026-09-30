namespace Extrode.Jaunty.SourceGenerator.Tests;

public sealed class GeneratedShadowedSystemTypeReadTests
{
    private const string Source = """
        using Extrode.Jaunty.Attributes;
        using Extrode.Jaunty.Interfaces;

        namespace System
        {
            public class Guid { }
            public class DateTime { }
            public class TimeSpan { }
            public class DateTimeOffset { }
        }

        namespace Shadow
        {
            [Table("shadowed")]
            public partial class Shadowed : IMapped<Shadowed>
            {
                [Key] public int Id { get; set; }
                public global::System.Guid G { get; set; } = new();
                public global::System.DateTime D { get; set; } = new();
                public global::System.TimeSpan T { get; set; } = new();
                public global::System.DateTimeOffset O { get; set; } = new();
            }
        }
        """;

    private static readonly string[] Properties = ["G", "D", "T", "O"];

    [Theory]
    [InlineData("G")]
    [InlineData("D")]
    [InlineData("T")]
    [InlineData("O")]
    public void AUserDeclaredSystemTypeIsAssignedWithoutANullGuard(string property)
    {
        var (sources, _, _) = GeneratorHarness.RunAndCompile(Source);

        string text = string.Join("\n", sources);
        int index = Array.IndexOf(Properties, property) + 1;

        Assert.Contains($"entity.{property} = ", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"IsDBNull(ord[{index}])", text, StringComparison.Ordinal);
    }
}
