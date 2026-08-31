using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.Tests.Core;

public class FlatFileOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new FlatFileOptions();

        Assert.Equal(":memory:", options.DatabasePath);
        Assert.True(options.AutoOpen);
        Assert.True(options.RegisterDialect);
        Assert.NotNull(options.Sources);
        Assert.Empty(options.Sources);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new FlatFileOptions
        {
            DatabasePath = "/tmp/test.duckdb",
            AutoOpen = false,
            RegisterDialect = false
        };

        Assert.Equal("/tmp/test.duckdb", options.DatabasePath);
        Assert.False(options.AutoOpen);
        Assert.False(options.RegisterDialect);
    }

    [Fact]
    public void Sources_CanBeAdded()
    {
        var options = new FlatFileOptions();
        var source = new CsvFileSource("sales", "sales.csv", typeof(object));

        options.Sources.Add(source);

        Assert.Single(options.Sources);
        Assert.Same(source, options.Sources[0]);
    }

    // ------------------------------------------------------------------
    // AddExcel / AddDeltaLake / AddIceberg fluent builders
    // ------------------------------------------------------------------

    private sealed class ExcelRow { }
    private sealed class DeltaRow { }
    private sealed class IcebergRow { }

    [Fact]
    public void AddExcel_AddsExcelFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddExcel<ExcelRow>("report.xlsx");

        Assert.Same(options, returned); // fluent chaining
        Assert.Single(options.Sources);
        Assert.IsType<ExcelFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddExcel_WithConfigure_AppliesConfiguration()
    {
        var options = new FlatFileOptions();
        options.AddExcel<ExcelRow>("report.xlsx", src => src.SheetName = "Data");

        var src = Assert.IsType<ExcelFileSource>(options.Sources[0]);
        Assert.Equal("Data", src.SheetName);
    }

    [Fact]
    public void AddDeltaLake_AddsDeltaLakeFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddDeltaLake<DeltaRow>("/lake/events");

        Assert.Same(options, returned);
        Assert.Single(options.Sources);
        Assert.IsType<DeltaLakeFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddIceberg_AddsIcebergFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddIceberg<IcebergRow>("/warehouse/orders");

        Assert.Same(options, returned);
        Assert.Single(options.Sources);
        Assert.IsType<IcebergFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddIceberg_WithConfigure_AppliesConfiguration()
    {
        var options = new FlatFileOptions();
        options.AddIceberg<IcebergRow>("/warehouse/orders", src => src.AllowMovedPaths = true);

        var src = Assert.IsType<IcebergFileSource>(options.Sources[0]);
        Assert.True(src.AllowMovedPaths);
    }

    [Fact]
    public void ChainedAdds_ProduceMultipleSources()
    {
        var options = new FlatFileOptions()
            .AddExcel<ExcelRow>("a.xlsx")
            .AddDeltaLake<DeltaRow>("/lake")
            .AddIceberg<IcebergRow>("/ice");

        Assert.Equal(3, options.Sources.Count);
    }

    [Fact]
    public void AddExcel_MultiFile_AddsExcelFileSourceWithAllPaths()
    {
        var options = new FlatFileOptions();
        var returned = options.AddExcel<ExcelRow>(["a.xlsx", "b.xlsx"]);

        Assert.Same(options, returned);
        var src = Assert.IsType<ExcelFileSource>(options.Sources[0]);
        Assert.Equal(["a.xlsx", "b.xlsx"], src.FilePaths);
    }

    // ------------------------------------------------------------------
    // AddCsv / AddTsv / AddParquet / AddJson fluent builders
    // ------------------------------------------------------------------

    private sealed class CsvRow { }
    private sealed class TsvRow { }
    private sealed class ParquetRow { }
    private sealed class JsonRow { }

    [Fact]
    public void AddCsv_AddsCsvFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddCsv<CsvRow>("data.csv");

        Assert.Same(options, returned); // fluent chaining
        Assert.Single(options.Sources);
        Assert.IsType<CsvFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddCsv_WithConfigure_AppliesConfiguration()
    {
        var options = new FlatFileOptions();
        options.AddCsv<CsvRow>("data.csv", src => src.Delimiter = ';');

        var src = Assert.IsType<CsvFileSource>(options.Sources[0]);
        Assert.Equal(';', src.Delimiter);
    }

    [Fact]
    public void AddCsv_MultiFile_AddsCsvFileSourceWithAllPaths()
    {
        var options = new FlatFileOptions();
        var returned = options.AddCsv<CsvRow>(["a.csv", "b.csv"]);

        Assert.Same(options, returned);
        var src = Assert.IsType<CsvFileSource>(options.Sources[0]);
        Assert.Equal(["a.csv", "b.csv"], src.FilePaths);
    }

    [Fact]
    public void AddTsv_AddsTsvFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddTsv<TsvRow>("data.tsv");

        Assert.Same(options, returned);
        Assert.Single(options.Sources);
        Assert.IsType<TsvFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddTsv_WithConfigure_AppliesConfiguration()
    {
        var options = new FlatFileOptions();
        options.AddTsv<TsvRow>("data.tsv", src => src.SkipRows = 2);

        var src = Assert.IsType<TsvFileSource>(options.Sources[0]);
        Assert.Equal(2, src.SkipRows);
    }

    [Fact]
    public void AddTsv_MultiFile_AddsTsvFileSourceWithAllPaths()
    {
        var options = new FlatFileOptions();
        var returned = options.AddTsv<TsvRow>(["a.tsv", "b.tsv"]);

        Assert.Same(options, returned);
        var src = Assert.IsType<TsvFileSource>(options.Sources[0]);
        Assert.Equal(["a.tsv", "b.tsv"], src.FilePaths);
    }

    [Fact]
    public void AddParquet_AddsParquetFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddParquet<ParquetRow>("data.parquet");

        Assert.Same(options, returned);
        Assert.Single(options.Sources);
        Assert.IsType<ParquetFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddParquet_WithConfigure_AppliesConfiguration()
    {
        var options = new FlatFileOptions();
        options.AddParquet<ParquetRow>("data.parquet", src => src.HivePartitioning = true);

        var src = Assert.IsType<ParquetFileSource>(options.Sources[0]);
        Assert.True(src.HivePartitioning);
    }

    [Fact]
    public void AddParquet_MultiFile_AddsParquetFileSourceWithAllPaths()
    {
        var options = new FlatFileOptions();
        var returned = options.AddParquet<ParquetRow>(["a.parquet", "b.parquet"]);

        Assert.Same(options, returned);
        var src = Assert.IsType<ParquetFileSource>(options.Sources[0]);
        Assert.Equal(["a.parquet", "b.parquet"], src.FilePaths);
    }

    [Fact]
    public void AddJson_AddsJsonFileSource()
    {
        var options = new FlatFileOptions();
        var returned = options.AddJson<JsonRow>("data.json");

        Assert.Same(options, returned);
        Assert.Single(options.Sources);
        Assert.IsType<JsonFileSource>(options.Sources[0]);
    }

    [Fact]
    public void AddJson_WithConfigure_AppliesConfiguration()
    {
        var options = new FlatFileOptions();
        options.AddJson<JsonRow>("data.json", src => src.JsonFormat = JsonFileFormat.Array);

        var src = Assert.IsType<JsonFileSource>(options.Sources[0]);
        Assert.Equal(JsonFileFormat.Array, src.JsonFormat);
    }

    [Fact]
    public void AddJson_MultiFile_AddsJsonFileSourceWithAllPaths()
    {
        var options = new FlatFileOptions();
        var returned = options.AddJson<JsonRow>(["a.json", "b.json"]);

        Assert.Same(options, returned);
        var src = Assert.IsType<JsonFileSource>(options.Sources[0]);
        Assert.Equal(["a.json", "b.json"], src.FilePaths);
    }

    // ------------------------------------------------------------------
    // ValidateSchema / PreloadIntoMemory
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateSchema_DefaultsFalse()
    {
        var options = new FlatFileOptions();
        Assert.False(options.ValidateSchema);
    }

    [Fact]
    public void ValidateSchema_CanBeSetTrue()
    {
        var options = new FlatFileOptions { ValidateSchema = true };
        Assert.True(options.ValidateSchema);
    }

    [Fact]
    public void PreloadIntoMemory_DefaultsFalse()
    {
        var options = new FlatFileOptions();
        Assert.False(options.PreloadIntoMemory);
    }

    [Fact]
    public void PreloadIntoMemory_CanBeSetTrue()
    {
        var options = new FlatFileOptions { PreloadIntoMemory = true };
        Assert.True(options.PreloadIntoMemory);
    }

    // ------------------------------------------------------------------
    // AddSource (custom IFileSource extension points)
    // ------------------------------------------------------------------

    private sealed class CustomSourceRow { }

    private sealed class FakeFileSource : IFileSource
    {
        public FakeFileSource(string tableName, string filePath, Type entityType)
        {
            TableName = tableName;
            FilePath = filePath;
            EntityType = entityType;
        }

        public string TableName { get; }
        public string FilePath { get; }
        public IReadOnlyList<string> FilePaths => new[] { FilePath };
        public string Format => "fake";
        public Type EntityType { get; }
        public bool IsPromotedToTable { get; set; }
        public bool IsPreloaded { get; set; }
        public string DuckDbFormatName => "FAKE";

        public string GenerateReadFunction(string pathExpression) => $"read_fake({pathExpression})";
        public string? GenerateCopyToOptions() => null;
    }

    [Fact]
    public void AddSource_IFileSource_AddsToSources()
    {
        var options = new FlatFileOptions();
        var source = new FakeFileSource("custom", "data.fake", typeof(CustomSourceRow));

        var returned = options.AddSource(source);

        Assert.Same(options, returned); // fluent chaining
        Assert.Single(options.Sources);
        Assert.Same(source, options.Sources[0]);
    }

    [Fact]
    public void AddSource_IFileSource_NullSource_ThrowsArgumentNullException()
    {
        var options = new FlatFileOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddSource((IFileSource)null!));
    }

    [Fact]
    public void AddSource_Generic_InvokesFactoryWithResolvedTableNameAndConfigure()
    {
        var options = new FlatFileOptions();
        string? capturedTableName = null;
        string? capturedFilePath = null;
        Type? capturedEntityType = null;
        var configured = false;

        var returned = options.AddSource<CustomSourceRow, FakeFileSource>(
            "data.fake",
            (tableName, filePath, entityType) =>
            {
                capturedTableName = tableName;
                capturedFilePath = filePath;
                capturedEntityType = entityType;
                return new FakeFileSource(tableName, filePath, entityType);
            },
            src => configured = true);

        Assert.Same(options, returned); // fluent chaining
        Assert.Single(options.Sources);
        Assert.Same(options.Sources[0], returned.Sources[0]);
        Assert.False(string.IsNullOrEmpty(capturedTableName));
        Assert.Equal("data.fake", capturedFilePath);
        Assert.Equal(typeof(CustomSourceRow), capturedEntityType);
        Assert.True(configured);

        var added = Assert.IsType<FakeFileSource>(options.Sources[0]);
        Assert.Equal(capturedTableName, added.TableName);
    }

    [Fact]
    public void AddSource_Generic_WithoutConfigure_StillAddsSource()
    {
        var options = new FlatFileOptions();

        options.AddSource<CustomSourceRow, FakeFileSource>(
            "data.fake",
            (tableName, filePath, entityType) => new FakeFileSource(tableName, filePath, entityType));

        Assert.Single(options.Sources);
        Assert.IsType<FakeFileSource>(options.Sources[0]);
    }
}