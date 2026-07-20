using System.Reflection;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Core;

/// <summary>
/// Tests for the FlatFile.Open() static factory methods.
/// </summary>
public class FlatFileTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    // ==========================================
    // T011 — Single file Open
    // ==========================================

    [Fact]
    public void Open_CsvFile_ReturnsDatabase()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        using var db = FlatFile.Open(csvPath);

        Assert.NotNull(db);
        Assert.NotNull(db.Connection);
    }

    [Fact]
    public void Open_TsvFile_ReturnsDatabase()
    {
        var tsvPath = Path.Combine(DataDir, "tsv", "sales.tsv");
        using var db = FlatFile.Open(tsvPath);

        Assert.NotNull(db);
        Assert.NotNull(db.Connection);
    }

    [Fact]
    public void Open_UnsupportedExtension_Throws()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.xyz");
        File.WriteAllText(tempFile, "data");
        try
        {
            Assert.Throws<ArgumentException>(() => FlatFile.Open(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Open_LegacyXlsExtension_ThrowsWithHelpfulMessage()
    {
        // Legacy binary .xls is intentionally not registered: ExcelFileSource reads via DuckDB's
        // read_xlsx, which only supports the modern .xlsx format (AUD-R9).
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.xls");
        File.WriteAllText(tempFile, "not a real xls file");
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => FlatFile.Open(tempFile));
            Assert.Contains(".xls", ex.Message);
            Assert.Contains(".xlsx", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Open_NonexistentFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            FlatFile.Open("/nonexistent/path/sales.csv"));
    }

    [Fact]
    public void Open_CsvFile_CreatesViewWithFilenameStem()
    {
        // sales.csv → view name "sales"
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        using var db = FlatFile.Open(csvPath);

        // Verify the view exists by querying it with raw SQL
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"sales\"";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(10, count);
    }

    // ==========================================
    // T012 — Configured multi-source Open
    // ==========================================

    [Fact]
    public void Open_WithConfigure_RegistersMultipleSources()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var tsvPath = Path.Combine(DataDir, "tsv", "sales.tsv");

        using var db = FlatFile.Open(options =>
        {
            options.AddCsv<SalesRecord>(csvPath);
            // TSV also maps to "sales" table name from [Table("sales")] — use a different entity
            // or register with a custom table name. For this test, use raw source.
            var tsvSource = new TsvFileSource("tsv_sales", tsvPath, typeof(SalesRecord));
            options.Sources.Add(tsvSource);
        });

        Assert.NotNull(db);

        // Both views should exist
        using var cmd1 = db.Connection.CreateCommand();
        cmd1.CommandText = "SELECT COUNT(*) FROM \"sales\"";
        Assert.Equal(10, Convert.ToInt32(cmd1.ExecuteScalar()));

        using var cmd2 = db.Connection.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM \"tsv_sales\"";
        Assert.Equal(5, Convert.ToInt32(cmd2.ExecuteScalar()));
    }

    [Fact]
    public void Open_AddCsv_WithOptions_AppliesConfiguration()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");

        using var db = FlatFile.Open(options =>
        {
            options.AddCsv<SalesRecord>(csvPath, csv =>
            {
                csv.HasHeader = true;
                csv.Delimiter = ',';
            });
        });

        // Should still work correctly with explicit options
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"sales\"";
        Assert.Equal(10, Convert.ToInt32(cmd.ExecuteScalar()));
    }

    [Fact]
    public void Open_MultipleSources_EachQueryableViaFluentApi()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");

        using var db = FlatFile.Open(options =>
        {
            options.AddCsv<SalesRecord>(csvPath);
            var jsonSource = new JsonFileSource("customers", jsonPath, typeof(CustomerProfile));
            options.Sources.Add(jsonSource);
        });

        // Query CSV via fluent API
        var sales = db.Connection.From<SalesRecord>().Select();
        Assert.Equal(10, sales.Count);

        // Query JSON via fluent API
        var customers = db.Connection.From<CustomerProfile>().Select();
        Assert.Equal(3, customers.Count);
    }

    // ==========================================
    // M1 Exit Gate Test
    // ==========================================

    [Fact]
    public void ExitGate_OpenCsv_FluentApi_ReturnsCorrectResults()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");

        using var db = FlatFile.Open(options =>
        {
            options.AddCsv<SalesRecord>(csvPath);
        });

        var results = db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .OrderByDescending(s => s.Revenue)
            .Take(50)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Revenue > 10000m));
        // Results are ordered by revenue descending
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
    }

    [Fact]
    public async Task ExitGate_OpenCsv_FluentApi_Async()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");

        using var db = FlatFile.Open(options =>
        {
            options.AddCsv<SalesRecord>(csvPath);
        });

        var results = await db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .OrderByDescending(s => s.Revenue)
            .Take(50)
            .SelectAsync();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Revenue > 10000m));
    }

    // ==========================================
    // AUD-R11 batch-07: RegisterExtension coverage
    // ==========================================

    [Fact]
    public void RegisterExtension_NullExtension_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            FlatFile.RegisterExtension(null!, (_, path, type) => new CsvFileSource("t", path, type)));

    [Fact]
    public void RegisterExtension_NullFactory_Throws()
        => Assert.Throws<ArgumentNullException>(() => FlatFile.RegisterExtension(".myfmt", null!));

    [Fact]
    public void RegisterExtension_ExtensionWithoutLeadingDot_IsNormalized()
    {
        // FlatFile.RegisterExtension prefixes a missing leading dot before storing the key, so
        // both "myfmt2" and ".myfmt2" registrations must resolve for a ".myfmt2" file.
        FlatFile.RegisterExtension("myfmt2", (tableName, path, type) => new CsvFileSource(tableName, path, type));

        IFileSource source = FlatFile.CreateSourceFromExtension(".myfmt2", "t", "irrelevant.myfmt2", typeof(object));

        Assert.IsType<CsvFileSource>(source);
    }

    [Fact]
    public void RegisterExtension_CustomFactory_OpenUsesRegisteredFactory()
    {
        // Round-trips a custom extension end-to-end through FlatFile.Open: register ".myfmt" as a
        // CSV-backed format, then open a real file with that extension and confirm the registered
        // factory (not the "unsupported extension" error) is what handles it.
        FlatFile.RegisterExtension(".myfmt", (tableName, path, type) => new CsvFileSource(tableName, path, type));

        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var customPath = Path.Combine(Path.GetTempPath(), $"jaunty_flatfile_custom_{Guid.NewGuid():N}.myfmt");
        File.Copy(csvPath, customPath);
        try
        {
            using var db = FlatFile.Open(customPath);

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{Path.GetFileNameWithoutExtension(customPath)}\"";
            Assert.Equal(10, Convert.ToInt32(cmd.ExecuteScalar()));
        }
        finally
        {
            File.Delete(customPath);
        }
    }

    // ==========================================
    // AUD-R11 batch-07: Open() glob pattern coverage
    // ==========================================

    [Fact]
    public void Open_GlobPattern_MatchesMultipleFilesAndUnionsRows()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jaunty_flatfile_glob_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "part1.csv"), "id,name\n1,Alice\n2,Bob\n");
            File.WriteAllText(Path.Combine(tempDir, "part2.csv"), "id,name\n3,Carol\n");

            var globPath = Path.Combine(tempDir, "part*.csv");
            using var db = FlatFile.Open(globPath);

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"part\"";
            Assert.Equal(3, Convert.ToInt32(cmd.ExecuteScalar()));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Open_GlobPatternWithNoExtension_Throws()
    {
        // InferExtensionFromGlob strips glob characters before inferring the extension; a pattern
        // with no literal extension left over (e.g. "data/*") can't be mapped to a file format.
        var ex = Assert.Throws<ArgumentException>(() => FlatFile.Open("data/*"));
        Assert.Contains("Cannot infer file format", ex.Message);
    }

    // ==========================================
    // AUD-R11 batch-07: Open() remote URI scheme coverage
    // ==========================================

    [Fact]
    public void Open_DisallowedUriScheme_ThrowsWithAllowedSchemesListed()
    {
        var ex = Assert.Throws<ArgumentException>(() => FlatFile.Open("ftp://example.com/data.csv"));
        Assert.Contains("ftp://", ex.Message);
        Assert.Contains("s3://", ex.Message);
        Assert.Contains("https://", ex.Message);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    [InlineData("s3")]
    [InlineData("s3a")]
    [InlineData("s3n")]
    [InlineData("az")]
    [InlineData("abfss")]
    [InlineData("r2")]
    [InlineData("gs")]
    [InlineData("hf")]
    [InlineData("file")]
    public void AllowedSchemes_ContainsExpectedScheme(string scheme)
    {
        // Verified via the private _allowedSchemes set directly rather than a real FlatFile.Open()
        // call, which would require actual network access (httpfs/S3 credentials, DNS resolution)
        // for a remote scheme to get past DuckDB's own file-open step - not something a unit test
        // should depend on.
        var allowedSchemes = (System.Collections.Generic.HashSet<string>)typeof(FlatFile)
            .GetField("_allowedSchemes", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Contains(scheme, allowedSchemes);
    }

    // ==========================================
    // AUD-R11 batch-07: private URI/glob helper unit coverage
    // ==========================================

    private static object? InvokePrivateStatic(string methodName, object?[] args)
    {
        MethodInfo method = typeof(FlatFile).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return method.Invoke(null, args);
    }

    [Theory]
    [InlineData("s3://bucket/file.csv", true, "s3")]
    [InlineData("https://example.com/data.json", true, "https")]
    [InlineData("data/sales.csv", false, "")]
    [InlineData("C:\\data\\sales.csv", false, "")]
    public void IsRemoteUri_DetectsSchemeCorrectly(string path, bool expectedIsRemote, string expectedScheme)
    {
        var args = new object?[] { path, null };
        var result = (bool)InvokePrivateStatic("IsRemoteUri", args)!;

        Assert.Equal(expectedIsRemote, result);
        Assert.Equal(expectedScheme, (string)args[1]!);
    }

    [Theory]
    [InlineData("s3://bucket/path/file.csv", "file.csv")]
    [InlineData("https://example.com/data/customers.json?token=abc", "customers.json")]
    [InlineData("s3://bucket/onlyfile.parquet", "onlyfile.parquet")]
    public void GetFileNameFromUri_ExtractsFileName(string uri, string expected)
    {
        var result = (string)InvokePrivateStatic("GetFileNameFromUri", [uri])!;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("data/*.csv", true)]
    [InlineData("data/file?.csv", true)]
    [InlineData("data/sales.csv", false)]
    public void IsGlobPattern_DetectsWildcards(string path, bool expected)
    {
        var result = (bool)InvokePrivateStatic("IsGlobPattern", [path])!;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("data/*.csv", ".csv")]
    [InlineData("logs/**/*.json", ".json")]
    [InlineData("data/file?.parquet", ".parquet")]
    public void InferExtensionFromGlob_InfersExtension(string glob, string expected)
    {
        var result = (string)InvokePrivateStatic("InferExtensionFromGlob", [glob])!;
        Assert.Equal(expected, result);
    }
}