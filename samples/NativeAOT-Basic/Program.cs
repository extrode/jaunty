using Jaunty;

using Microsoft.Data.Sqlite;

using NativeAOT.Basic;

// NativeAOT-Basic: Demonstrates Jaunty with source-generated mappers (fully AOT-compatible)
// No reflection used. The source generator creates ReadEntity at compile time for query mapping.

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

// Create schema and seed data
using var cmd = connection.CreateCommand();
cmd.CommandText = """
    CREATE TABLE products (
        product_id INTEGER PRIMARY KEY AUTOINCREMENT,
        product_name TEXT NOT NULL,
        unit_price REAL NOT NULL,
        discontinued INTEGER NOT NULL DEFAULT 0
    );
    INSERT INTO products (product_name, unit_price, discontinued) VALUES ('Chai', 18.00, 0);
    INSERT INTO products (product_name, unit_price, discontinued) VALUES ('Chang', 19.00, 0);
    INSERT INTO products (product_name, unit_price, discontinued) VALUES ('Aniseed Syrup', 10.00, 0);
    INSERT INTO products (product_name, unit_price, discontinued) VALUES ('Tofu', 23.25, 0);
    INSERT INTO products (product_name, unit_price, discontinued) VALUES ('Genen Shouyu', 15.50, 1);
    """;
cmd.ExecuteNonQuery();

Console.WriteLine("=== Jaunty NativeAOT Basic Sample ===");
Console.WriteLine();

// Query all products (source-generated mapper)
var products = connection.Query<Product>("SELECT product_id, product_name, unit_price, discontinued FROM products");
Console.WriteLine($"Query: {products.Count} products found");
foreach (var p in products)
    Console.WriteLine($"  [{p.ProductId}] {p.ProductName} - ${p.UnitPrice:F2} {(p.Discontinued ? "(discontinued)" : "")}");

// QueryFirst with parameters
var first = connection.QueryFirst<Product>(
    "SELECT product_id, product_name, unit_price, discontinued FROM products WHERE product_id = @Id",
    new { Id = 1 });
Console.WriteLine($"\nQueryFirst: {first.ProductName} (${first.UnitPrice:F2})");

// QueryScalar
var count = connection.QueryScalar<int>("SELECT COUNT(*) FROM products WHERE discontinued = 0");
Console.WriteLine($"\nQueryScalar: {count} active products");

Console.WriteLine("\nDone.");