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

    [Fact]
    public void GenerateReadFunction_NoOptions_ProducesTabDelimitedReadCsv()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object));
        var fn = source.GenerateReadFunction("'f.tsv'");

        Assert.StartsWith("read_csv(", fn);
        Assert.Contains("delim = '\t'", fn);
        Assert.Contains("auto_detect = true", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithHeader_IncludesHeaderTrue()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { HasHeader = true };
        var fn = source.GenerateReadFunction("'f.tsv'");

        Assert.Contains("header = true", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithNullString_IncludesNullstrParam()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { NullString = "NA" };
        var fn = source.GenerateReadFunction("'f.tsv'");

        Assert.Contains("nullstr = 'NA'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithNullStringContainingSingleQuote_Escapes()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { NullString = "N'A" };
        var fn = source.GenerateReadFunction("'f.tsv'");

        Assert.Contains("nullstr = 'N''A'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithSkipRows_IncludesSkipParam()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { SkipRows = 4 };
        var fn = source.GenerateReadFunction("'f.tsv'");

        Assert.Contains("skip = 4", fn);
    }

    [Fact]
    public void GenerateCopyToOptions_ReturnsTabDelimiterAndHeader()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object));
        Assert.Equal("DELIMITER '\t', HEADER true", source.GenerateCopyToOptions());
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