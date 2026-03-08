using System.Text;

using DuckDB.NET.Data;

namespace Jaunty.FlatFiles.Benchmarks;

/// <summary>
/// Generates test data files for benchmarks.
/// </summary>
internal static class TestDataGenerator
{
    private static readonly Random Random = new(42); // Fixed seed for reproducibility

    public static void GenerateAll()
    {
        Directory.CreateDirectory("Data");
        GenerateSmallCsv();
        GenerateParquet();
        GenerateJson();
    }

    private static void GenerateSmallCsv()
    {
        var path = "Data/small-sales.csv";
        if (File.Exists(path)) return;

        var sb = new StringBuilder();
        sb.AppendLine("id,product_name,revenue,quantity,date,region");

        for (int i = 1; i <= 100; i++)
        {
            sb.AppendLine($"{i},Product {i},{1000 + Random.Next(9000)},{1 + Random.Next(99)},{DateTime.UtcNow.AddDays(-Random.Next(365)):yyyy-MM-dd},{GetRandomRegion()}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static void GenerateParquet()
    {
        var path = "Data/inventory.parquet";
        if (File.Exists(path)) return;

        // Generate via DuckDB
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE inventory AS 
            SELECT 
                i as sku,
                'Item ' || i as name,
                floor(random() * 1000)::int as stockLevel,
                round(random() * 999 + 1, 2) as unitPrice,
                (random() > 0.2) as isActive
            FROM range(1, 101) t(i)
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = "COPY inventory TO 'Data/inventory.parquet' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();
    }

    private static void GenerateJson()
    {
        var path = "Data/customers.json";
        if (File.Exists(path)) return;

        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

        for (int i = 1; i <= 200; i++)
        {
            var customer = new
            {
                CustomerId = i,
                Name = $"Customer {i}",
                Email = $"customer{i}@example.com",
                Phone = Random.Next(2) == 1 ? $"+1-555-{Random.Next(1000, 9999)}" : null,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Next(730))
            };

            var json = System.Text.Json.JsonSerializer.Serialize(customer);
            writer.WriteLine(json);
        }
    }

    private static string GetRandomRegion()
    {
        var regions = new[] { "North", "South", "East", "West", "Central" };
        return regions[Random.Next(regions.Length)];
    }
}