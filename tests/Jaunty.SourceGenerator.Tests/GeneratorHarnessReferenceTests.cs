using Xunit;

namespace Jaunty.SourceGenerator.Tests;

public sealed class GeneratorHarnessReferenceTests
{
    [Fact]
    public void ReferenceAssemblyPaths_AlwaysIncludeSystemDataCommon()
    {
        string expected = typeof(System.Data.IDbCommand).Assembly.Location;

        Assert.Contains(expected, GeneratorHarness.ReferenceAssemblyPaths(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenceAssemblyPaths_AlwaysIncludeJauntyAndItsInterfaces()
    {
        IEnumerable<string> paths = GeneratorHarness.ReferenceAssemblyPaths();

        Assert.Contains(typeof(global::Jaunty.Attributes.TableAttribute).Assembly.Location, paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(typeof(global::Jaunty.Interfaces.IGeneratedAccessors<>).Assembly.Location, paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenceAssemblyPaths_HaveNoDuplicates()
    {
        List<string> paths = GeneratorHarness.ReferenceAssemblyPaths().ToList();

        Assert.Equal(paths.Count, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void RunAndCompile_CompilesAMapperBeforeAnyOtherTestLoadedSystemData()
    {
        var (sources, _, compileErrors) = GeneratorHarness.RunAndCompile("""
            using Jaunty.Attributes;

            namespace HarnessProbe;

            [Table("orders")]
            public partial class Order
            {
                public int Id { get; set; }
            }
            """);

        Assert.Single(sources);
        Assert.Empty(compileErrors);
    }
}
