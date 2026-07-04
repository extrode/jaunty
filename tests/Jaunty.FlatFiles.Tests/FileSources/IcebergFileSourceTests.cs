using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

public class IcebergFileSourceTests
{
    private sealed class IcebergRow { }

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_SinglePath_SetsProperties()
    {
        var source = new IcebergFileSource("warehouse", "/iceberg/warehouse", typeof(IcebergRow));

        Assert.Equal("warehouse", source.TableName);
        Assert.Equal("/iceberg/warehouse", source.FilePath);
        Assert.Equal(FileFormats.Iceberg, source.Format);
        Assert.Equal(typeof(IcebergRow), source.EntityType);
        Assert.Single(source.FilePaths);
    }

    [Fact]
    public void Constructor_MultiplePaths_SetsAllPaths()
    {
        var paths = new[] { "/ice/a", "/ice/b" };
        var source = new IcebergFileSource("t", paths, typeof(IcebergRow));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("/ice/a", source.FilePath);
    }

    // ------------------------------------------------------------------
    // Null / empty guards
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullTableName_Throws()
        => Assert.Throws<ArgumentNullException>(() => new IcebergFileSource(null!, "/p", typeof(IcebergRow)));

    [Fact]
    public void Constructor_NullFilePath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new IcebergFileSource("t", (string)null!, typeof(IcebergRow)));

    [Fact]
    public void Constructor_NullEntityType_Throws()
        => Assert.Throws<ArgumentNullException>(() => new IcebergFileSource("t", "/p", null!));

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new IcebergFileSource("t", Array.Empty<string>(), typeof(IcebergRow)));

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    [Fact]
    public void Defaults_AreCorrect()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow));

        Assert.False(source.IsPromotedToTable);
        Assert.False(source.IsPreloaded);
        Assert.False(source.AllowMovedPaths);
        Assert.Equal("ICEBERG", source.DuckDbFormatName);
    }

    // ------------------------------------------------------------------
    // AllowMovedPaths option
    // ------------------------------------------------------------------

    [Fact]
    public void AllowMovedPaths_DefaultsFalse()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow));
        Assert.False(source.AllowMovedPaths);
    }

    [Fact]
    public void AllowMovedPaths_CanBeSetTrue()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow)) { AllowMovedPaths = true };
        Assert.True(source.AllowMovedPaths);
    }

    // ------------------------------------------------------------------
    // GenerateReadFunction
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateReadFunction_NoOptions_ProducesIcebergScan()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow));
        var fn = source.GenerateReadFunction("'/iceberg/table'");

        Assert.StartsWith("iceberg_scan(", fn);
        Assert.Contains("'/iceberg/table'", fn);
    }

    [Fact]
    public void GenerateReadFunction_AllowMovedPathsTrue_IncludesOption()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow)) { AllowMovedPaths = true };
        var fn = source.GenerateReadFunction("'/p'");

        Assert.Contains("allow_moved_paths = true", fn);
    }

    [Fact]
    public void GenerateReadFunction_AllowMovedPathsFalse_DoesNotIncludeOption()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow)) { AllowMovedPaths = false };
        var fn = source.GenerateReadFunction("'/p'");

        // Default false — option should not appear in the SQL
        Assert.DoesNotContain("allow_moved_paths", fn);
    }

    // ------------------------------------------------------------------
    // GenerateCopyToOptions
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateCopyToOptions_ReturnsNull()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow));
        Assert.Null(source.GenerateCopyToOptions());
    }

    // ------------------------------------------------------------------
    // IsPromotedToTable / IsPreloaded
    // ------------------------------------------------------------------

    [Fact]
    public void IsPromotedToTable_CanBeSet()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow)) { IsPromotedToTable = true };
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public void IsPreloaded_CanBeSet()
    {
        var source = new IcebergFileSource("t", "/p", typeof(IcebergRow)) { IsPreloaded = true };
        Assert.True(source.IsPreloaded);
    }
}
