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
    public void Constructor_MultiplePaths_SetsAllPaths()
    {
        var paths = new[] { "data/a.tsv", "data/b.tsv" };
        var source = new TsvFileSource("t", paths, typeof(object));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("data/a.tsv", source.FilePath);
    }

    [Fact]
    public void Constructor_NullTableName_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TsvFileSource(null!, "f.tsv", typeof(object)));

    [Fact]
    public void Constructor_NullFilePath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TsvFileSource("t", (string)null!, typeof(object)));

    [Fact]
    public void Constructor_NullEntityType_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TsvFileSource("t", "f.tsv", null!));

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new TsvFileSource("t", Array.Empty<string>(), typeof(object)));

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
    public void GenerateReadFunction_WithHeaderFalse_IncludesHeaderFalse()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { HasHeader = false };
        var fn = source.GenerateReadFunction("'f.tsv'");

        Assert.Contains("header = false", fn);
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

    // AUD-R23 batch-1: GenerateCopyToOptions() used to hardcode "DELIMITER '\t', HEADER true"
    // regardless of NullString, unlike GenerateReadFunction (which does honor it) and unlike
    // CsvFileSource.GenerateCopyToOptions (which includes it). A NullString configured for
    // reading a TSV was silently dropped when exporting via COPY TO, round-tripping the NULL
    // sentinel as literal text instead of an actual NULL.
    [Fact]
    public void GenerateCopyToOptions_WithHeaderFalse_WritesHeaderFalse()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { HasHeader = false };
        Assert.Equal("DELIMITER '	', HEADER false", source.GenerateCopyToOptions());
    }

    [Fact]
    public void GenerateCopyToOptions_WithHeaderFalseAndNullString_WritesBoth()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { HasHeader = false, NullString = "NA" };
        Assert.Equal("DELIMITER '	', HEADER false, NULL 'NA'", source.GenerateCopyToOptions());
    }

    [Fact]
    public void GenerateCopyToOptions_WithNullString_IncludesNull()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { NullString = "NA" };
        Assert.Equal("DELIMITER '\t', HEADER true, NULL 'NA'", source.GenerateCopyToOptions());
    }

    [Fact]
    public void IsPromotedToTable_CanBeSet()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { IsPromotedToTable = true };
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public void IsPreloaded_CanBeSet()
    {
        var source = new TsvFileSource("t", "f.tsv", typeof(object)) { IsPreloaded = true };
        Assert.True(source.IsPreloaded);
    }
}
