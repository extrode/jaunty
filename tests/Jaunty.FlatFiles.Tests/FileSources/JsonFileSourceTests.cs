using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

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
    public void Constructor_MultiplePaths_SetsAllPaths()
    {
        var paths = new[] { "data/a.json", "data/b.json" };
        var source = new JsonFileSource("t", paths, typeof(object));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("data/a.json", source.FilePath);
    }

    [Fact]
    public void Constructor_NullTableName_Throws()
        => Assert.Throws<ArgumentNullException>(() => new JsonFileSource(null!, "f.json", typeof(object)));

    [Fact]
    public void Constructor_NullFilePath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new JsonFileSource("t", (string)null!, typeof(object)));

    [Fact]
    public void Constructor_NullEntityType_Throws()
        => Assert.Throws<ArgumentNullException>(() => new JsonFileSource("t", "f.json", null!));

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new JsonFileSource("t", Array.Empty<string>(), typeof(object)));

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

    [Fact]
    public void GenerateReadFunction_NoOptions_ProducesBasicReadJsonAuto()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object));
        var fn = source.GenerateReadFunction("'f.json'");

        Assert.StartsWith("read_json_auto(", fn);
        Assert.Contains("'f.json'", fn);
        Assert.DoesNotContain("format =", fn);
        Assert.DoesNotContain("maximum_depth", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithArrayFormat_IncludesFormatParam()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object)) { JsonFormat = JsonFileFormat.Array };
        var fn = source.GenerateReadFunction("'f.json'");

        Assert.Contains("format = 'array'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithNewlineDelimitedFormat_IncludesFormatParam()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object)) { JsonFormat = JsonFileFormat.NewlineDelimited };
        var fn = source.GenerateReadFunction("'f.json'");

        Assert.Contains("format = 'newline_delimited'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithMaxDepth_IncludesMaximumDepthParam()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object)) { MaxDepth = 10 };
        var fn = source.GenerateReadFunction("'f.json'");

        Assert.Contains("maximum_depth = 10", fn);
    }

    [Fact]
    public void GenerateCopyToOptions_ReturnsNull()
    {
        var source = new JsonFileSource("t", "f.json", typeof(object));
        Assert.Null(source.GenerateCopyToOptions());
    }
}
