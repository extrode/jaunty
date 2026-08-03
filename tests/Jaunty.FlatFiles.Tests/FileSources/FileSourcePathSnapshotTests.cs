using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.Tests.FileSources;

/// <summary>
/// AUD-R35-234 and AUD-R35-235: every source validated its path array and then stored it by
/// reference, so the caller kept a live handle past the guard, and the two custom-source extension
/// points on <see cref="FlatFileOptions"/> reported a null source two different unnamed ways.
/// </summary>
public class FileSourcePathSnapshotTests
{
    private sealed class Row
    {
        public int Id { get; set; }
    }

    public static TheoryData<string, Func<string[], IFileSource>> MultiPathSources() => new()
    {
        { "csv", paths => new CsvFileSource("t", paths, typeof(Row)) },
        { "tsv", paths => new TsvFileSource("t", paths, typeof(Row)) },
        { "json", paths => new JsonFileSource("t", paths, typeof(Row)) },
        { "parquet", paths => new ParquetFileSource("t", paths, typeof(Row)) },
        { "excel", paths => new ExcelFileSource("t", paths, typeof(Row)) },
    };

    public static TheoryData<string, Func<string[], IFileSource>> SinglePathSources() => new()
    {
        { "delta", paths => new DeltaLakeFileSource("t", paths, typeof(Row)) },
        { "iceberg", paths => new IcebergFileSource("t", paths, typeof(Row)) },
    };

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void MutatingTheCallersArray_DoesNotReachTheSource(string format, Func<string[], IFileSource> construct)
    {
        _ = format;
        string[] paths = ["a.dat", "b.dat", "c.dat"];

        IFileSource source = construct(paths);
        paths[1] = null!;
        paths[2] = "hijacked.dat";

        Assert.Equal(new[] { "a.dat", "b.dat", "c.dat" }, source.FilePaths);
    }

    [Theory]
    [MemberData(nameof(SinglePathSources))]
    public void MutatingTheCallersSinglePathArray_DoesNotReachTheSource(string format, Func<string[], IFileSource> construct)
    {
        _ = format;
        string[] paths = ["table/"];

        IFileSource source = construct(paths);
        paths[0] = "hijacked/";

        Assert.Equal(new[] { "table/" }, source.FilePaths);
    }

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void FilePaths_IsNotCastableBackToAWritableArray(string format, Func<string[], IFileSource> construct)
    {
        _ = format;

        IFileSource source = construct(["a.dat", "b.dat"]);

        Assert.Null(source.FilePaths as string[]);
    }

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void FilePath_StillReportsTheFirstPath(string format, Func<string[], IFileSource> construct)
    {
        _ = format;

        IFileSource source = construct(["a.dat", "b.dat"]);

        Assert.Equal("a.dat", source.FilePath);
        Assert.Equal(2, source.FilePaths.Count);
        Assert.Equal("b.dat", source.FilePaths[1]);
    }

    [Fact]
    public void AddSource_NullFactory_ThrowsArgumentNullException()
    {
        var options = new FlatFileOptions();

        var ex = Assert.Throws<ArgumentNullException>(
            () => options.AddSource<Row, CsvFileSource>("a.csv", null!));

        Assert.Equal("factory", ex.ParamName);
    }

    [Fact]
    public void AddSource_FactoryReturningNull_ThrowsInvalidOperationException()
    {
        var options = new FlatFileOptions();

        var ex = Assert.Throws<InvalidOperationException>(
            () => options.AddSource<Row, CsvFileSource>("a.csv", (_, _, _) => null!));

        Assert.Contains("returned null", ex.Message, StringComparison.Ordinal);
        Assert.Contains("CsvFileSource", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSource_NonGenericOverload_StillThrowsArgumentNullException()
    {
        var options = new FlatFileOptions();

        var ex = Assert.Throws<ArgumentNullException>(() => options.AddSource(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void AddSource_AWorkingFactory_StillRegistersTheSource()
    {
        var options = new FlatFileOptions();

        options.AddSource<Row, CsvFileSource>(
            "a.csv",
            (table, path, entity) => new CsvFileSource(table, path, entity),
            source => source.HasHeader = false);

        IFileSource registered = Assert.Single(options.Sources);
        Assert.Equal("a.csv", registered.FilePath);
        Assert.False(((CsvFileSource)registered).HasHeader);
    }
}
