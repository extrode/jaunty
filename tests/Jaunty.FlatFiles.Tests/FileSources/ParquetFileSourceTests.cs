using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

public class ParquetFileSourceTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var source = new ParquetFileSource("inventory", "data/inv.parquet", typeof(object));

        Assert.Equal("inventory", source.TableName);
        Assert.Equal("data/inv.parquet", source.FilePath);
        Assert.Equal(FileFormats.Parquet, source.Format);
    }

    [Fact]
    public void Constructor_MultiplePaths_SetsAllPaths()
    {
        var paths = new[] { "data/a.parquet", "data/b.parquet" };
        var source = new ParquetFileSource("t", paths, typeof(object));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("data/a.parquet", source.FilePath);
    }

    [Fact]
    public void Constructor_NullTableName_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ParquetFileSource(null!, "f.parquet", typeof(object)));

    [Fact]
    public void Constructor_NullFilePath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ParquetFileSource("t", (string)null!, typeof(object)));

    [Fact]
    public void Constructor_NullEntityType_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ParquetFileSource("t", "f.parquet", null!));

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new ParquetFileSource("t", Array.Empty<string>(), typeof(object)));

    [Fact]
    public void HivePartitioning_DefaultsFalse()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object));
        Assert.False(source.HivePartitioning);
    }

    [Fact]
    public void HivePartitioning_CanBeSetTrue()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object)) { HivePartitioning = true };
        Assert.True(source.HivePartitioning);
    }

    [Fact]
    public void GenerateReadFunction_NoOptions_ProducesBasicReadParquet()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object));
        var fn = source.GenerateReadFunction("'f.parquet'");

        Assert.StartsWith("read_parquet(", fn);
        Assert.Contains("'f.parquet'", fn);
        Assert.DoesNotContain("hive_partitioning", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithHivePartitioning_IncludesHivePartitioningTrue()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object)) { HivePartitioning = true };
        var fn = source.GenerateReadFunction("'f.parquet'");

        Assert.Contains("hive_partitioning = true", fn);
    }

    [Fact]
    public void GenerateCopyToOptions_ReturnsNull()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object));
        Assert.Null(source.GenerateCopyToOptions());
    }

    [Fact]
    public void IsPromotedToTable_CanBeSet()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object)) { IsPromotedToTable = true };
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public void IsPreloaded_CanBeSet()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object)) { IsPreloaded = true };
        Assert.True(source.IsPreloaded);
    }
}
