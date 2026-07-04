using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.FileSources;

public class ExcelFileSourceTests
{
    private sealed class SalesRow { }

    // ------------------------------------------------------------------
    // Constructor — single file
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_SingleFile_SetsProperties()
    {
        var source = new ExcelFileSource("sales", "data/sales.xlsx", typeof(SalesRow));

        Assert.Equal("sales", source.TableName);
        Assert.Equal("data/sales.xlsx", source.FilePath);
        Assert.Equal(FileFormats.Excel, source.Format);
        Assert.Equal(typeof(SalesRow), source.EntityType);
        Assert.Single(source.FilePaths);
        Assert.Equal("data/sales.xlsx", source.FilePaths[0]);
    }

    [Fact]
    public void Constructor_MultipleFiles_SetsAllPaths()
    {
        var paths = new[] { "a.xlsx", "b.xlsx" };
        var source = new ExcelFileSource("t", paths, typeof(SalesRow));

        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("a.xlsx", source.FilePath); // first path is canonical
    }

    // ------------------------------------------------------------------
    // Null / empty guard checks
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullTableName_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ExcelFileSource(null!, "f.xlsx", typeof(SalesRow)));

    [Fact]
    public void Constructor_NullFilePath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ExcelFileSource("t", (string)null!, typeof(SalesRow)));

    [Fact]
    public void Constructor_NullEntityType_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ExcelFileSource("t", "f.xlsx", null!));

    [Fact]
    public void Constructor_EmptyFilePaths_Throws()
        => Assert.Throws<ArgumentException>(() => new ExcelFileSource("t", Array.Empty<string>(), typeof(SalesRow)));

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    [Fact]
    public void Defaults_AreCorrect()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow));

        Assert.Null(source.SheetName);
        Assert.Null(source.HasHeader);
        Assert.Null(source.Range);
        Assert.False(source.IsPromotedToTable);
        Assert.False(source.IsPreloaded);
    }

    // ------------------------------------------------------------------
    // Options can be set
    // ------------------------------------------------------------------

    [Fact]
    public void Options_CanBeSet()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow))
        {
            SheetName = "Sheet1",
            HasHeader = true,
            Range = "A1:D100"
        };

        Assert.Equal("Sheet1", source.SheetName);
        Assert.True(source.HasHeader);
        Assert.Equal("A1:D100", source.Range);
    }

    // ------------------------------------------------------------------
    // GenerateReadFunction
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateReadFunction_NoOptions_ProducesBasicReadXlsx()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow));
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.StartsWith("read_xlsx(", fn);
        Assert.Contains("'f.xlsx'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithSheetName_IncludesSheetParam()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { SheetName = "Data" };
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.Contains("sheet = 'Data'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithSheetNameContainingSingleQuote_Escapes()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { SheetName = "O'Donnell" };
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.Contains("O''Donnell", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithHeader_IncludesHeaderTrue()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { HasHeader = true };
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.Contains("header = true", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithHeaderFalse_IncludesHeaderFalse()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { HasHeader = false };
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.Contains("header = false", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithRange_IncludesRangeParam()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { Range = "B2:F50" };
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.Contains("range = 'B2:F50'", fn);
    }

    [Fact]
    public void GenerateReadFunction_WithRangeContainingSingleQuote_Escapes()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { Range = "A'1:D'10" };
        var fn = source.GenerateReadFunction("'f.xlsx'");

        Assert.Contains("A''1:D''10", fn);
    }

    // ------------------------------------------------------------------
    // GenerateCopyToOptions
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateCopyToOptions_ReturnsNull()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow));
        Assert.Null(source.GenerateCopyToOptions());
    }

    // ------------------------------------------------------------------
    // DuckDbFormatName / IsPromotedToTable / IsPreloaded
    // ------------------------------------------------------------------

    [Fact]
    public void DuckDbFormatName_IsXlsx()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow));
        Assert.Equal("XLSX", source.DuckDbFormatName);
    }

    [Fact]
    public void IsPromotedToTable_CanBeSet()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { IsPromotedToTable = true };
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public void IsPreloaded_CanBeSet()
    {
        var source = new ExcelFileSource("t", "f.xlsx", typeof(SalesRow)) { IsPreloaded = true };
        Assert.True(source.IsPreloaded);
    }
}
