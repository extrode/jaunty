using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

public class TsvFileSourceTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var source = new TsvFileSource("logs", "data/logs.tsv", typeof(object));

        Assert.Equal("logs", source.TableName);
        Assert.Equal("data/logs.tsv", source.FilePath);
        Assert.Equal(FileFormats.Tsv, source.Format);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object));

        Assert.Null(source.HasHeader);
        Assert.Null(source.NullString);
        Assert.Equal(0, source.SkipRows);
    }
}

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
    public void HivePartitioning_DefaultsFalse()
    {
        var source = new ParquetFileSource("t", "f.parquet", typeof(object));
        Assert.False(source.HivePartitioning);
    }
}

public class JsonFileSourceTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var source = new JsonFileSource("customers", "data/cust.json", typeof(object));

        Assert.Equal("customers", source.TableName);
        Assert.Equal("data/cust.json", source.FilePath);
        Assert.Equal(FileFormats.Json, source.Format);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object));

        Assert.Equal(JsonFileFormat.Auto, source.JsonFormat);
        Assert.Null(source.MaxDepth);
    }

    [Fact]
    public void JsonFormat_CanBeSet()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object))
        {
            JsonFormat = JsonFileFormat.NewlineDelimited,
            MaxDepth = 5
        };

        Assert.Equal(JsonFileFormat.NewlineDelimited, source.JsonFormat);
        Assert.Equal(5, source.MaxDepth);
    }
}
