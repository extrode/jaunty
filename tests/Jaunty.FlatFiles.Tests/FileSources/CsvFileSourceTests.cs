using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

public class CsvFileSourceTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var source = new CsvFileSource("sales", "data/sales.csv", typeof(SalesRecord));

        Assert.Equal("sales", source.TableName);
        Assert.Equal("data/sales.csv", source.FilePath);
        Assert.Equal(FileFormats.Csv, source.Format);
        Assert.Equal(typeof(SalesRecord), source.EntityType);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord));

        Assert.Null(source.HasHeader);
        Assert.Null(source.Delimiter);
        Assert.Null(source.QuoteChar);
        Assert.Null(source.NullString);
        Assert.Equal(0, source.SkipRows);
        Assert.False(source.IsPromotedToTable);
        Assert.False(source.IsPreloaded);
    }

    [Fact]
    public void Options_CanBeSet()
    {
        var source = new CsvFileSource("sales", "data/sales.csv", typeof(SalesRecord))
        {
            HasHeader = true,
            Delimiter = ';',
            QuoteChar = '\'',
            NullString = "NA",
            SkipRows = 2
        };

        Assert.True(source.HasHeader);
        Assert.Equal(';', source.Delimiter);
        Assert.Equal('\'', source.QuoteChar);
        Assert.Equal("NA", source.NullString);
        Assert.Equal(2, source.SkipRows);
    }

    [Fact]
    public void Constructor_NullTableName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvFileSource(null!, "f.csv", typeof(SalesRecord)));
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvFileSource("t", (string)null!, typeof(SalesRecord)));
    }

    [Fact]
    public void Constructor_NullEntityType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvFileSource("t", "f.csv", null!));
    }

    [Fact]
    public void Constructor_MultiplePaths_SetsAllPaths()
    {
        var paths = new[] { "data/a.csv", "data/b.csv" };
        var source = new CsvFileSource("t", paths, typeof(SalesRecord));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("data/a.csv", source.FilePath);
    }

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new CsvFileSource("t", Array.Empty<string>(), typeof(SalesRecord)));

    // ------------------------------------------------------------------
    // GenerateReadFunction
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateReadFunction_NoOptions_ProducesBasicReadCsv()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord));
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.StartsWith("read_csv(", fn);
        Assert.Contains("'f.csv'", fn);
        Assert.Contains("auto_detect = true", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithHeader_IncludesHeaderTrue()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { HasHeader = true };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("header = true", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithHeaderFalse_IncludesHeaderFalse()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { HasHeader = false };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("header = false", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithDelimiter_IncludesDelimParam()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { Delimiter = ';' };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("delim = ';'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithQuoteChar_IncludesQuoteParam()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { QuoteChar = '"' };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("quote = '\"'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithQuoteCharAsSingleQuote_Escapes()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { QuoteChar = '\'' };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("quote = ''''", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithDelimiterAsSingleQuote_Escapes()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { Delimiter = '\'' };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("delim = ''''", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithNullString_IncludesNullstrParam()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { NullString = "NA" };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("nullstr = 'NA'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithNullStringContainingSingleQuote_Escapes()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { NullString = "N'A" };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("nullstr = 'N''A'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithSkipRows_IncludesSkipParam()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { SkipRows = 3 };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.Contains("skip = 3", fn);
    }

    [Fact]
    public void GenerateReadFunction_SkipRowsZero_OmitsSkipParam()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { SkipRows = 0 };
        var fn = source.GenerateReadFunction("'f.csv'");

        Assert.DoesNotContain("skip =", fn);
    }

    // ------------------------------------------------------------------
    // GenerateCopyToOptions
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateCopyToOptions_ReturnsHeaderTrue()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord));
        Assert.Equal("HEADER true", source.GenerateCopyToOptions());
    }

    [Fact]
    public void GenerateCopyToOptions_WithDelimiter_IncludesDelimiter()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { Delimiter = ';' };
        Assert.Equal("HEADER true, DELIMITER ';'", source.GenerateCopyToOptions());
    }

    [Fact]
    public void GenerateCopyToOptions_WithQuoteChar_IncludesQuote()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { QuoteChar = '\'' };
        Assert.Equal("HEADER true, QUOTE ''''", source.GenerateCopyToOptions());
    }

    [Fact]
    public void GenerateCopyToOptions_WithNullString_IncludesNull()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { NullString = "NA" };
        Assert.Equal("HEADER true, NULL 'NA'", source.GenerateCopyToOptions());
    }

    [Fact]
    public void GenerateCopyToOptions_WithAllOptions_IncludesAllInOrder()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord))
        {
            Delimiter = ';',
            QuoteChar = '"',
            NullString = "NA"
        };

        Assert.Equal("HEADER true, DELIMITER ';', QUOTE '\"', NULL 'NA'", source.GenerateCopyToOptions());
    }

    [Fact]
    public void IsPromotedToTable_CanBeSet()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { IsPromotedToTable = true };
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public void IsPreloaded_CanBeSet()
    {
        var source = new CsvFileSource("t", "f.csv", typeof(SalesRecord)) { IsPreloaded = true };
        Assert.True(source.IsPreloaded);
    }

    // Minimal test entity
    private class SalesRecord { }
}