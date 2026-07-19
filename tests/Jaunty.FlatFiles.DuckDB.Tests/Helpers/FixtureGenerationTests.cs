namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers;

/// <summary>
/// Generates the persistent fixture files (under data/, not a temp directory) that
/// ImportPipelineTests/MultiSourceTests/ParquetQueryTests/PreloadTests read by path.
/// </summary>
/// <remarks>
/// Always regenerates rather than skipping when the file already exists - a run that only
/// checks File.Exists after the first pass no longer exercises FixtureGenerator at all, and
/// can't self-heal if a prior run left a corrupted/stale file behind.
/// </remarks>
public class FixtureGenerationTests
{
    [Fact]
    public void Generate_ParquetFixture()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "data", "parquet", "inventory.parquet");

        FixtureGenerator.GenerateParquetFixture(outputPath);

        Assert.True(File.Exists(outputPath), "Parquet fixture should be generated");
    }

    [Fact]
    public void Generate_LargeCsvFixture()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "large-10k.csv");

        FixtureGenerator.GenerateLargeCsv(outputPath, 10000);

        Assert.True(File.Exists(outputPath), "Large CSV fixture should be generated");

        var lines = File.ReadAllLines(outputPath);
        Assert.Equal(10001, lines.Length); // Header + 10000 rows
    }
}