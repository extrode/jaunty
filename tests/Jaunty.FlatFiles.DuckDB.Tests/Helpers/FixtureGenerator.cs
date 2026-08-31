using DuckDB.NET.Data;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers;

/// <summary>
/// Generates test fixture files on demand.
/// </summary>
public static class FixtureGenerator
{
    public static void GenerateParquetFixture(string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT setseed(0.42)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = """
            CREATE TABLE inventory AS
            SELECT
                i as sku,
                'Item ' || (i % 100) as name,
                floor(random() * 1000)::int as stockLevel,
                round(random() * 999 + 1, 2) as unitPrice,
                (random() > 0.2) as isActive
            FROM range(1, 101) t(i)
            """;
        cmd.ExecuteNonQuery();

        var escapedPath = outputPath.Replace("'", "''");
        cmd.CommandText = $"COPY inventory TO '{escapedPath}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();
    }

    public static void GenerateLargeCsv(string outputPath, int rowCount = 10000)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var random = new Random(42);
        using var writer = new StreamWriter(outputPath, append: false);
        writer.WriteLine("id,product_name,revenue,quantity,date,region");

        var regions = new[] { "North", "South", "East", "West", "Central" };
        for (int i = 1; i <= rowCount; i++)
        {
            var date = DateTime.UtcNow.AddDays(-random.Next(730));
            writer.WriteLine($"{i},Product {i},{1000 + random.Next(99000)},{1 + random.Next(999)},{date:yyyy-MM-dd},{regions[random.Next(regions.Length)]}");
        }
    }
}