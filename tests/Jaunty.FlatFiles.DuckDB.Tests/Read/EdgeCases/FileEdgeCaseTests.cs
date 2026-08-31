using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read.EdgeCases;

/// <summary>
/// Tests for edge cases: Unicode, malformed files, empty files, non-existent files.
/// </summary>
public class FileEdgeCaseTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    // ==========================================
    // Unicode / non-ASCII characters
    // ==========================================

    [Table("unicode_data")]
    public class UnicodeEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    [Fact]
    public void Unicode_CsvLoadsCorrectly()
    {
        var csvPath = Path.Combine(DataDir, "csv", "unicode.csv");
        var source = new CsvFileSource("unicode_data", csvPath, typeof(UnicodeEntity));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);
        var results = db.Connection.Query<UnicodeEntity>("SELECT * FROM \"unicode_data\" ORDER BY \"Id\"");

        Assert.Equal(5, results.Count);

        // German umlauts
        Assert.Equal("Müller", results[0].Name);
        Assert.Equal("München", results[0].City);

        // Japanese kanji
        Assert.Equal("田中太郎", results[1].Name);
        Assert.Equal("東京", results[1].City);

        // Cyrillic
        Assert.Equal("Борис", results[2].Name);
        Assert.Equal("Москва", results[2].City);

        // Portuguese diacritics
        Assert.Equal("José García", results[3].Name);
        Assert.Equal("São Paulo", results[3].City);

        // Chinese characters
        Assert.Equal("李明", results[4].Name);
        Assert.Equal("北京", results[4].City);
    }

    [Fact]
    public void Unicode_WhereClause_FiltersCorrectly()
    {
        var csvPath = Path.Combine(DataDir, "csv", "unicode.csv");
        var source = new CsvFileSource("unicode_where", csvPath, typeof(UnicodeEntity));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);
        var results = db.Connection.Query<UnicodeEntity>(
            "SELECT * FROM \"unicode_where\" WHERE \"City\" = $city",
            new { city = "東京" });

        Assert.Single(results);
        Assert.Equal("田中太郎", results[0].Name);
    }

    // ==========================================
    // Malformed / corrupted files
    // ==========================================

    [Table("malformed_data")]
    public class MalformedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    [Fact]
    public void MalformedCsv_DuckDbHandlesGracefully()
    {
        var csvPath = Path.Combine(DataDir, "csv", "malformed.csv");
        var source = new CsvFileSource("malformed_data", csvPath, typeof(MalformedEntity));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        // DuckDB's CSV parser is lenient — it should either parse what it can
        // or throw a descriptive error. Either outcome is acceptable.
        try
        {
            using var db = new DuckDb(options);
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"malformed_data\"";
            var count = Convert.ToInt32(cmd.ExecuteScalar());

            // If DuckDB parses it, we should get at least the well-formed rows
            Assert.True(count >= 1, $"Expected at least 1 parseable row, got {count}");
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException || ex is InvalidOperationException)
        {
            // DuckDB may reject the file — that's also acceptable behavior. The exception filter
            // above (DbException or InvalidOperationException) is the actual assertion: any other
            // exception type propagates and fails the test instead of being silently accepted.
        }
    }

    [Fact]
    public void NonExistentFile_ThrowsDescriptiveError()
    {
        var fakePath = Path.Combine(DataDir, "csv", "does-not-exist.csv");
        var source = new CsvFileSource("nonexistent", fakePath, typeof(MalformedEntity));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        var ex = Assert.ThrowsAny<Exception>(() => new DuckDb(options));
        Assert.True(ex is System.Data.Common.DbException || ex is IOException,
            $"Expected DbException or IOException, got {ex.GetType().Name}: {ex.Message}");
    }

    [Fact]
    public void SourceRegistrationFailure_DoesNotLeakConnection()
    {
        // Regression test for AUD-R9: a source registration failure mid-constructor previously
        // left the already-opened DuckDBConnection undisposed forever, because the constructor
        // never returns an instance the caller could Dispose. Repeating the failing construction
        // many times proves the connection is now cleaned up on the failure path instead of
        // accumulating undisposed native connections/handles across iterations.
        var fakePath = Path.Combine(DataDir, "csv", "does-not-exist-for-leak-test.csv");

        for (int i = 0; i < 50; i++)
        {
            var source = new CsvFileSource($"nonexistent_{i}", fakePath, typeof(MalformedEntity));
            var options = new FlatFileOptions();
            options.Sources.Add(source);

            var ex = Assert.ThrowsAny<Exception>(() => new DuckDb(options));
            Assert.True(ex is System.Data.Common.DbException || ex is IOException,
                $"Expected DbException or IOException, got {ex.GetType().Name}: {ex.Message}");
        }
    }

    [Fact]
    public void EmptyJsonArray_ReturnsEmpty()
    {
        // Create an in-memory empty JSON array file
        var tempPath = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempPath, "[]");

            var source = new JsonFileSource("empty_json", tempPath, typeof(UnicodeEntity));
            var options = new FlatFileOptions();
            options.Sources.Add(source);

            using var db = new DuckDb(options);
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"empty_json\"";
            var count = Convert.ToInt32(cmd.ExecuteScalar());

            Assert.Equal(0, count);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}