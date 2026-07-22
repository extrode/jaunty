using System.Reflection;

namespace Jaunty.FlatFiles.DuckDB.Tests.Core;

/// <summary>
/// AUD-R20 batch-7: <c>DuckDb</c> never issued <c>INSTALL</c>/<c>LOAD</c> for the httpfs/azure
/// extensions before a remote read, relying entirely on DuckDB's own implicit extension
/// autoinstall/autoload, which silently breaks when outbound network access is blocked or
/// autoinstall/autoload has been disabled. Verified via the private
/// <c>GetDuckDbExtensionForScheme</c> mapping directly rather than a real remote read, which
/// would require actual network access (S3/Azure credentials, DNS resolution) - not something a
/// unit test should depend on (mirrors the same rationale as
/// <see cref="FlatFileTests.AllowedSchemes_ContainsExpectedScheme"/>).
/// </summary>
public class DuckDbExtensionLoadingTests
{
    private static string? GetDuckDbExtensionForScheme(string scheme)
    {
        MethodInfo method = typeof(DuckDb).GetMethod(
            "GetDuckDbExtensionForScheme", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string?)method.Invoke(null, [scheme]);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    [InlineData("s3")]
    [InlineData("s3a")]
    [InlineData("s3n")]
    [InlineData("r2")]
    [InlineData("gs")]
    [InlineData("hf")]
    public void GetDuckDbExtensionForScheme_HttpfsBackedScheme_ReturnsHttpfs(string scheme)
    {
        Assert.Equal("httpfs", GetDuckDbExtensionForScheme(scheme));
    }

    [Theory]
    [InlineData("az")]
    [InlineData("abfss")]
    public void GetDuckDbExtensionForScheme_AzureBackedScheme_ReturnsAzure(string scheme)
    {
        Assert.Equal("azure", GetDuckDbExtensionForScheme(scheme));
    }

    [Fact]
    public void GetDuckDbExtensionForScheme_FileScheme_ReturnsNull()
    {
        Assert.Null(GetDuckDbExtensionForScheme("file"));
    }

    [Fact]
    public void GetDuckDbExtensionForScheme_IsCaseInsensitive()
    {
        Assert.Equal("httpfs", GetDuckDbExtensionForScheme("S3"));
        Assert.Equal("azure", GetDuckDbExtensionForScheme("ABFSS"));
    }
}
