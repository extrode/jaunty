using Jaunty;
using Jaunty.Fluent;

using Microsoft.Data.Sqlite;

using NativeAOT.FluentQuery;

// NativeAOT-FluentQuery: Demonstrates Jaunty.Fluent under NativeAOT (fully AOT-compatible)
// No reflection used, no reference to Jaunty.Extensions.Reflection anywhere in this project.
// Spec 003 (fluent NativeAOT-safe metadata) closed the gap that made this impossible - fluent
// queries now resolve source-generated entity metadata the same reflection-free way Query<T>
// already did.

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

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

Console.WriteLine("=== Jaunty NativeAOT Fluent Query Sample ===");
Console.WriteLine();

// Fluent read: Where + Select
List<Product> active = connection.From<Product>()
    .Where(p => !p.Discontinued && p.UnitPrice > 10m)
    .Select();
Console.WriteLine($"Fluent Where+Select: {active.Count} active products over $10.00");
foreach (Product p in active)
    Console.WriteLine($"  [{p.ProductId}] {p.ProductName} - ${p.UnitPrice:F2}");

// Fluent CRUD: Insert/Update/Delete, all routing through the same source-gen-first
// metadata tier as the read path above.
var newProduct = new Product { ProductName = "Matcha", UnitPrice = 12.00m, Discontinued = false };
long generatedId = connection.Insert(newProduct);
Console.WriteLine($"\nInsert: generated id {generatedId}");

newProduct.ProductId = (int)generatedId;
newProduct.UnitPrice = 13.50m;
int updated = connection.Update(newProduct);
Console.WriteLine($"Update: {updated} row(s) affected");

int deleted = connection.Delete(newProduct);
Console.WriteLine($"Delete: {deleted} row(s) affected");

Console.WriteLine("\nDone.");
