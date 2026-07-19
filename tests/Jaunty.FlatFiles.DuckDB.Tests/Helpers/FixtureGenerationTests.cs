namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers;

/// <summary>
/// Fixture generation tests - run once to create test data files.
/// </summary>
public class FixtureGenerationTests
{
    [Fact]
    public void Generate_ParquetFixture()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "data", "parquet", "inventory.parquet");

        if (!File.Exists(outputPath))
        {
            FixtureGenerator.GenerateParquetFixture(outputPath);
        }

        Assert.True(File.Exists(outputPath), "Parquet fixture should be generated");
    }

    [Fact]
    public void Generate_LargeCsvFixture()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "large-10k.csv");

        if (!File.Exists(outputPath))
        {
            FixtureGenerator.GenerateLargeCsv(outputPath, 10000);
        }

        Assert.True(File.Exists(outputPath), "Large CSV fixture should be generated");

        var lines = File.ReadAllLines(outputPath);
        Assert.Equal(10001, lines.Length); // Header + 10000 rows
    }
}