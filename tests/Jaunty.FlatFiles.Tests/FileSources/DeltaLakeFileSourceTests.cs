using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

public class DeltaLakeFileSourceTests
{
    private sealed class DeltaRow { }

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_SinglePath_SetsProperties()
    {
        var source = new DeltaLakeFileSource("events", "/data/events_delta", typeof(DeltaRow));

        Assert.Equal("events", source.TableName);
        Assert.Equal("/data/events_delta", source.FilePath);
        Assert.Equal(FileFormats.DeltaLake, source.Format);
        Assert.Equal(typeof(DeltaRow), source.EntityType);
        Assert.Single(source.FilePaths);
    }

    [Fact]
    public void Constructor_MultiplePaths_SetsAllPaths()
    {
        var paths = new[] { "/lake/part1", "/lake/part2" };
        var source = new DeltaLakeFileSource("t", paths, typeof(DeltaRow));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("/lake/part1", source.FilePath);
    }

    // ------------------------------------------------------------------
    // Null / empty guards
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullTableName_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DeltaLakeFileSource(null!, "/p", typeof(DeltaRow)));

    [Fact]
    public void Constructor_NullFilePath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DeltaLakeFileSource("t", (string)null!, typeof(DeltaRow)));

    [Fact]
    public void Constructor_NullEntityType_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DeltaLakeFileSource("t", "/p", null!));

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new DeltaLakeFileSource("t", Array.Empty<string>(), typeof(DeltaRow)));

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    [Fact]
    public void Defaults_AreCorrect()
    {
        var source = new DeltaLakeFileSource("t", "/p", typeof(DeltaRow));

        Assert.False(source.IsPromotedToTable);
        Assert.False(source.IsPreloaded);
        Assert.Equal("DELTA", source.DuckDbFormatName);
    }

    // ------------------------------------------------------------------
    // GenerateReadFunction
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateReadFunction_ProducesDeltaScan()
    {
        var source = new DeltaLakeFileSource("t", "/p", typeof(DeltaRow));
        var fn = source.GenerateReadFunction("'/data/events'");

        Assert.Equal("delta_scan('/data/events')", fn);
    }

    // ------------------------------------------------------------------
    // GenerateCopyToOptions
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateCopyToOptions_ReturnsNull()
    {
        var source = new DeltaLakeFileSource("t", "/p", typeof(DeltaRow));
        Assert.Null(source.GenerateCopyToOptions());
    }

    // ------------------------------------------------------------------
    // IsPromotedToTable / IsPreloaded
    // ------------------------------------------------------------------

    [Fact]
    public void IsPromotedToTable_CanBeSet()
    {
        var source = new DeltaLakeFileSource("t", "/p", typeof(DeltaRow)) { IsPromotedToTable = true };
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public void IsPreloaded_CanBeSet()
    {
        var source = new DeltaLakeFileSource("t", "/p", typeof(DeltaRow)) { IsPreloaded = true };
        Assert.True(source.IsPreloaded);
    }
}
