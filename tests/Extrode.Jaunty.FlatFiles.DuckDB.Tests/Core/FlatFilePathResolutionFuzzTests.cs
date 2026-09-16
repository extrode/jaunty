namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Core;

public class FlatFilePathResolutionFuzzTests
{
    private static readonly string[] AdversarialPaths =
    [
        "", " ", "\t", "\n",
        "://", "://x", "x://", "://x://y",
        "ftp://example.com/data.csv",
        "javascript://alert(1)",
        "file", "file:", "file:/", "file://",
        "a" + new string('/', 5000) + "b.csv",
        new string('a', 10_000) + ".csv",
        "\0", "data\0.csv", "data.csv\0",
        "café/données.csv", "テーブル.csv", "🎉.csv",
        "*.csv", "**/*.csv", "data/*/*.csv", "?.csv", "***???.csv",
        "C:\\data\\file.csv", "\\\\server\\share\\file.csv",
        "../../../etc/passwd", "..\\..\\..\\windows\\system32",
        "s3://", "s3://bucket", "s3://bucket/",
        "https://user:pass@example.com/data.csv?query=1&other=2",
        "data.csv?arg=1", "data.csv#fragment",
        "   data.csv   ",
        new string('/', 1000),
    ];

    [Fact]
    public void IsRemoteUri_NeverThrows_ForAnyInput()
    {
        foreach (string path in AdversarialPaths)
            FlatFile.IsRemoteUri(path, out _);
    }

    [Fact]
    public void ResolvePath_NeverThrowsAnUnexpectedExceptionType()
    {
        foreach (string path in AdversarialPaths)
        {
            try
            {
                FlatFile.ResolvePath(path, "filePath");
            }
            catch (ArgumentException)
            {
            }
            catch (FileNotFoundException)
            {
            }
            catch (Exception ex)
            {
                Assert.Fail($"ResolvePath(\"{Printable(path)}\") threw unexpected {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [Fact]
    public void ResolvePath_RejectedUriSchemes_NameTheArgument()
    {
        foreach (string scheme in new[] { "ftp", "javascript", "ldap", "file123", "S3X" })
        {
            var ex = Assert.Throws<ArgumentException>(() => FlatFile.ResolvePath($"{scheme}://host/path.csv", "filePath"));
            Assert.Equal("filePath", ex.ParamName);
        }
    }

    [Theory]
    [InlineData("s3://bucket/path/file.csv")]
    [InlineData("https://example.com/data.csv")]
    [InlineData("s3://bucket/café données.csv")]
    [InlineData("s3://bucket/")]
    [InlineData("s3://bucket/no-extension")]
    [InlineData("s3://bucket/1starts-with-digit.csv")]
    [InlineData("s3://bucket/'; DROP TABLE t; --.csv")]
    public void ResolvePath_AllowedRemoteSchemes_ProduceASafeTableName(string path)
    {
        (string resolvedPath, _, string tableName) = FlatFile.ResolvePath(path, "filePath");

        Assert.Equal(path, resolvedPath);
        Assert.NotNull(tableName);
        Assert.NotEmpty(tableName);
        Assert.True(char.IsLetter(tableName[0]) || tableName[0] == '_',
            $"tableName \"{tableName}\" from \"{path}\" does not start with a letter or underscore.");
        Assert.DoesNotContain(tableName, c => !char.IsLetterOrDigit(c) && c != '_');
    }

    private static string Printable(string s) =>
        s.Replace("\0", "\\0").Replace("\n", "\\n").Replace("\r", "\\r");
}
