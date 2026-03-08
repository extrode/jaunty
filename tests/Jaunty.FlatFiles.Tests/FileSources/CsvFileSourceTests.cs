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

    // Minimal test entity
    private class SalesRecord { }
}
