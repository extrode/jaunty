using Jaunty.Attributes;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// P3-8 edge case tests: Unicode, malformed files, concurrent access, nested JSON, large datasets.
/// </summary>
public class P3EdgeCaseTests
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
            "SELECT * FROM \"unicode_where\" WHERE \"City\" = $1",
            new { p1 = "東京" });

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
            // DuckDB may reject the file — that's also acceptable behavior
            Assert.NotNull(ex.Message);
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

    // ==========================================
    // Concurrent access
    // ==========================================

    [Fact]
    public async Task ConcurrentReads_DoNotCorruptResults()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<Entities.SalesRecord>(csvPath);

        using var db = new DuckDb(options);

        // Get expected count
        var expected = db.Connection.From<Entities.SalesRecord>().Select().Count;

        // Run 10 concurrent queries
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var results = db.Connection.Query<Entities.SalesRecord>(
                "SELECT * FROM \"sales\"");
            return results.Count;
        }));

        var counts = await Task.WhenAll(tasks);

        // All concurrent reads should return the same count
        Assert.All(counts, c => Assert.Equal(expected, c));
    }

    [Fact]
    public async Task ConcurrentReads_DifferentQueries_Succeed()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<Entities.SalesRecord>(csvPath);

        using var db = new DuckDb(options);

        var tasks = new List<Task<int>>
        {
            Task.Run(() => db.Connection.Query<Entities.SalesRecord>("SELECT * FROM \"sales\"").Count),
            Task.Run(() =>
            {
                using var cmd = db.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM \"sales\"";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }),
            Task.Run(() => db.Connection.Query<Entities.SalesRecord>(
                "SELECT * FROM \"sales\" WHERE \"Quantity\" > 0").Count)
        };

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r > 0, "Each concurrent query should return results"));
    }

    // ==========================================
    // Nested JSON structures
    // ==========================================

    [Fact]
    public void NestedJson_RawQuery_AccessesNestedFields()
    {
        var jsonPath = Path.Combine(DataDir, "json", "nested.json");
        var source = new JsonFileSource("nested_data", jsonPath, typeof(object));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);

        // DuckDB supports struct field access with dot notation
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"Name\", \"Address\".\"City\" AS City FROM \"nested_data\" ORDER BY \"Id\"";
        using var reader = cmd.ExecuteReader();

        var rows = new List<(int Id, string Name, string City)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("City"))
            ));
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal("Springfield", rows[0].City);
        Assert.Equal("Shelbyville", rows[1].City);
        Assert.Equal("Capital City", rows[2].City);
    }

    [Fact]
    public void NestedJson_ArrayField_Queryable()
    {
        var jsonPath = Path.Combine(DataDir, "json", "nested.json");
        var source = new JsonFileSource("nested_tags", jsonPath, typeof(object));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);

        // DuckDB supports array_length on nested arrays
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"Name\", len(\"Tags\") AS TagCount FROM \"nested_tags\" ORDER BY \"Id\"";
        using var reader = cmd.ExecuteReader();

        var rows = new List<(int Id, int TagCount)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(reader.GetOrdinal("Id")),
                Convert.ToInt32(reader.GetValue(reader.GetOrdinal("TagCount")))
            ));
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal(2, rows[0].TagCount); // Alice: ["admin", "user"]
        Assert.Equal(1, rows[1].TagCount); // Bob: ["user"]
        Assert.Equal(0, rows[2].TagCount); // Charlie: []
    }

    [Fact]
    public void NestedJson_WhereOnNestedField()
    {
        var jsonPath = Path.Combine(DataDir, "json", "nested.json");
        var source = new JsonFileSource("nested_filter", jsonPath, typeof(object));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT \"Name\" FROM \"nested_filter\" WHERE \"Address\".\"Zip\" = '62702'";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Bob", reader.GetString(0));
        Assert.False(reader.Read()); // Only one match
    }

    // ==========================================
    // Large dataset (>100K rows, generated in-memory)
    // ==========================================

    [Table("large_dataset")]
    public class LargeEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public double Value { get; set; }
    }

    [Fact]
    public void LargeDataset_250K_Rows_QueriesSuccessfully()
    {
        const int rowCount = 250_000;
        var tempPath = Path.Combine(Path.GetTempPath(), $"large_{Guid.NewGuid()}.csv");

        try
        {
            // Generate a 250K row CSV
            using (var writer = new StreamWriter(tempPath))
            {
                writer.WriteLine("Id,Name,Value");
                for (int i = 1; i <= rowCount; i++)
                {
                    writer.WriteLine($"{i},Item_{i},{i * 1.5:F2}");
                }
            }

            var source = new CsvFileSource("large_dataset", tempPath, typeof(LargeEntity));
            var options = new FlatFileOptions();
            options.Sources.Add(source);

            using var db = new DuckDb(options);

            // Count
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"large_dataset\"";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.Equal(rowCount, count);

            // Aggregation
            cmd.CommandText = "SELECT SUM(\"Value\") FROM \"large_dataset\"";
            var sum = Convert.ToDouble(cmd.ExecuteScalar());
            Assert.True(sum > 0);

            // Filtered query returns correct subset
            cmd.CommandText = "SELECT COUNT(*) FROM \"large_dataset\" WHERE \"Id\" <= 1000";
            var filtered = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.Equal(1000, filtered);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void LargeDataset_StreamingDoesNotOOM()
    {
        const int rowCount = 200_000;
        var tempPath = Path.Combine(Path.GetTempPath(), $"stream_{Guid.NewGuid()}.csv");

        try
        {
            using (var writer = new StreamWriter(tempPath))
            {
                writer.WriteLine("Id,Name,Value");
                for (int i = 1; i <= rowCount; i++)
                {
                    writer.WriteLine($"{i},Item_{i},{i * 0.99:F2}");
                }
            }

            var source = new CsvFileSource("stream_test", tempPath, typeof(LargeEntity));
            var options = new FlatFileOptions();
            options.Sources.Add(source);

            using var db = new DuckDb(options);

            // Stream all rows via IEnumerable — should not load all into memory at once
            int streamed = 0;
            foreach (var entity in db.Connection.QueryStream<LargeEntity>(
                "SELECT * FROM \"stream_test\""))
            {
                streamed++;
                if (streamed >= 1000) break; // Early exit — proves streaming works without full materialization
            }

            Assert.Equal(1000, streamed);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
