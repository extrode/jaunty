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

// Grouped projection into a DTO. This is the path GroupedJoinedResultMapper reflects over, and
// until the projection type carried [DynamicallyAccessedMembers] nothing rooted it: published
// NativeAOT, the trimmer removed PriceBand's setters, GetProperties() came back empty and every
// row arrived fully defaulted - no exception, no diagnostic. The sample used to stop before this
// line, so the one unrooted reflection site on an AOT publish path was the one no AOT binary ran.
//
// The assertions below are the point of including it. Printing the rows would look identical
// whether the mapping worked or silently produced zeros.
List<PriceBand> bands = connection.From<Product>()
    .GroupBy(p => p.Discontinued)
    .Select(g => new PriceBand
    {
        Discontinued = g.Key,
        Count = g.Count(),
        Highest = g.Max(p => p.UnitPrice)
    });

Console.WriteLine("\nGrouped projection into a DTO:");
foreach (PriceBand band in bands)
    Console.WriteLine($"  discontinued={band.Discontinued}  count={band.Count}  highest=${band.Highest:F2}");

if (bands.Count != 2)
    throw new InvalidOperationException($"expected 2 groups, got {bands.Count}");

// Every group has rows and a non-zero price, so a defaulted instance is distinguishable from a
// correctly mapped one. Under the old unrooted code this is what failed after publish.
foreach (PriceBand band in bands)
{
    if (band.Count == 0 || band.Highest == 0m)
        throw new InvalidOperationException(
            $"grouped projection mapped to defaults (count={band.Count}, highest={band.Highest}) - " +
            "the projection type's members were trimmed away");
}

// The anonymous-type form takes the constructor path rather than the property path, and fails
// differently when trimmed: MissingMethodException instead of silent defaults. Both are covered.
var anonymous = connection.From<Product>()
    .GroupBy(p => p.Discontinued)
    .Select(g => new { Key = g.Key, Total = g.Count() });

if (anonymous.Count != 2 || anonymous.Any(a => a.Total == 0))
    throw new InvalidOperationException("anonymous-type grouped projection did not map");

Console.WriteLine($"Anonymous-type projection: {anonymous.Count} groups, {anonymous.Sum(a => a.Total)} rows total");

Console.WriteLine("\nDone.");
