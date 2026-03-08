using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.Core;

public class FlatFileOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new FlatFileOptions();

        Assert.Equal(":memory:", options.DatabasePath);
        Assert.True(options.AutoOpen);
        Assert.True(options.RegisterDialect);
        Assert.NotNull(options.Sources);
        Assert.Empty(options.Sources);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new FlatFileOptions
        {
            DatabasePath = "/tmp/test.duckdb",
            AutoOpen = false,
            RegisterDialect = false
        };

        Assert.Equal("/tmp/test.duckdb", options.DatabasePath);
        Assert.False(options.AutoOpen);
        Assert.False(options.RegisterDialect);
    }

    [Fact]
    public void Sources_CanBeAdded()
    {
        var options = new FlatFileOptions();
        var source = new CsvFileSource("sales", "sales.csv", typeof(object));

        options.Sources.Add(source);

        Assert.Single(options.Sources);
        Assert.Same(source, options.Sources[0]);
    }
}