using Xunit;

namespace Jaunty.SourceGenerator.Tests;

// These assertions go by file name on purpose: a typeof(System.Data.IDbCommand) would load
// System.Data.Common into the test process and make the loaded-assembly scan find it, which is the
// very order dependence under test.
public sealed class GeneratorHarnessReferenceTests
{
    [Fact]
    public void ReferenceAssemblyPaths_AlwaysIncludeSystemDataCommon()
    {
        Assert.Contains(GeneratorHarness.ReferenceAssemblyPaths(), p => FileNameIs(p, "System.Data.Common.dll"));
    }

    [Fact]
    public void ReferenceAssemblyPaths_AlwaysIncludeJaunty()
    {
        Assert.Contains(GeneratorHarness.ReferenceAssemblyPaths(), p => FileNameIs(p, "Jaunty.dll"));
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

    private static bool FileNameIs(string path, string name)
        => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);
}
