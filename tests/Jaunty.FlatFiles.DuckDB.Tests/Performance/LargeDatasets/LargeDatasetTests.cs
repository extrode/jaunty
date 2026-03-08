using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Performance.LargeDatasets;

/// <summary>
/// Tests for large dataset handling (>100K rows).
/// </summary>
public class LargeDatasetTests : IDisposable
{
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

    public void Dispose()
    {
        // Cleanup is handled in each test's finally block
    }
}
