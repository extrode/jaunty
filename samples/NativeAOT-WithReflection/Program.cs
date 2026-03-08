using Jaunty;
using Jaunty.Extensions.Reflection;

using Microsoft.Data.Sqlite;

using NativeAOT.WithReflection;

// NativeAOT-WithReflection: Demonstrates Jaunty with the optional reflection extension.
// This enables special type mapping (Dictionary, KeyValuePair, etc.) at the cost of requiring
// TrimmerRootAssembly configuration to preserve reflection metadata.

// Explicit initialization for NativeAOT (avoids Assembly.Load which may fail in AOT)
JauntyReflectionExtensions.UseReflectionMapping();
SpecialTypeMappers.Register();

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

// Create schema
using var cmd = connection.CreateCommand();
cmd.CommandText = """
    CREATE TABLE categories (
        category_id INTEGER PRIMARY KEY AUTOINCREMENT,
        category_name TEXT NOT NULL,
        description TEXT
    );
    INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks, coffees, teas');
    INSERT INTO categories (category_name, description) VALUES ('Condiments', 'Sweet and savory sauces');
    INSERT INTO categories (category_name, description) VALUES ('Confections', 'Desserts, candies, and sweet breads');
    """;
cmd.ExecuteNonQuery();

Console.WriteLine("=== Jaunty NativeAOT With Reflection Sample ===");
Console.WriteLine();

// Standard typed query (works without reflection too)
var categories = connection.Query<Category>(
    "SELECT category_id, category_name, description FROM categories");
Console.WriteLine($"Typed query: {categories.Count} categories");
foreach (var c in categories)
    Console.WriteLine($"  [{c.CategoryId}] {c.CategoryName}: {c.Description}");

// Dictionary mapping (requires reflection extension)
Console.WriteLine("\nDictionary mapping:");
var dicts = connection.Query<Dictionary<string, object>>(
    "SELECT category_id, category_name, description FROM categories");
foreach (var dict in dicts)
    Console.WriteLine($"  {dict["category_name"]}: {dict["description"]}");

// Scalar query
var count = connection.QueryScalar<int>("SELECT COUNT(*) FROM categories");
Console.WriteLine($"\nScalar: {count} categories total");

Console.WriteLine("\nDone.");