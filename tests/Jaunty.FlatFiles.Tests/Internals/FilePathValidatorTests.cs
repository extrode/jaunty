using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.Tests.Internals;

/// <summary>
/// AUD-R26: every multi-path file source used to validate only <c>filePaths[0]</c> while consuming
/// the whole array, so a null at index 1 or later constructed successfully and surfaced much later
/// as a <see cref="NullReferenceException"/> during SQL generation. These tests pin the shared
/// validator's behaviour through each of the five affected constructors so they cannot drift apart
/// again.
/// </summary>
public class FilePathValidatorTests
{
    private sealed class Row
    {
        public int Id { get; set; }
    }

    public static TheoryData<string, Func<string[], object>> MultiPathSources() => new()
    {
        { "csv", paths => new CsvFileSource("t", paths, typeof(Row)) },
        { "tsv", paths => new TsvFileSource("t", paths, typeof(Row)) },
        { "json", paths => new JsonFileSource("t", paths, typeof(Row)) },
        { "parquet", paths => new ParquetFileSource("t", paths, typeof(Row)) },
        { "excel", paths => new ExcelFileSource("t", paths, typeof(Row)) },
    };

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void NullAtNonZeroIndex_Throws(string format, Func<string[], object> construct)
    {
        _ = format;

        var ex = Assert.Throws<ArgumentNullException>(
            () => construct(["a.dat", null!, "c.dat"]));

        Assert.Equal("filePaths", ex.ParamName);
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void NullAtIndexZero_StillThrows(string format, Func<string[], object> construct)
    {
        _ = format;

        var ex = Assert.Throws<ArgumentNullException>(() => construct([null!, "b.dat"]));

        Assert.Equal("filePaths", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void NullArray_Throws(string format, Func<string[], object> construct)
    {
        _ = format;

        var ex = Assert.Throws<ArgumentNullException>(() => construct(null!));

        Assert.Equal("filePaths", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void EmptyArray_Throws(string format, Func<string[], object> construct)
    {
        _ = format;

        var ex = Assert.Throws<ArgumentException>(() => construct([]));

        Assert.Equal("filePaths", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(MultiPathSources))]
    public void AllPathsPresent_Constructs(string format, Func<string[], object> construct)
    {
        _ = format;

        object source = construct(["a.dat", "b.dat", "c.dat"]);

        Assert.NotNull(source);
    }

    /// <summary>
    /// Empty and whitespace entries are deliberately accepted: rejecting them would be a new
    /// restriction rather than a fix, and a remote path scheme this library does not know about is
    /// not ours to second-guess.
    /// </summary>
    [Fact]
    public void WhitespacePath_IsNotRejected()
    {
        var source = new CsvFileSource("t", ["a.csv", "   "], typeof(Row));

        Assert.Equal(2, source.FilePaths!.Count);
    }
}
