# Jaunty Missing Features Examples

This document provides examples of missing features in Jaunty and how they could be implemented, showing both SQL and C# API usage.

## 1. Advanced Parameter Types (TVP - Table Valued Parameters)

### SQL for TVP Support
```sql
-- Create TVP type (SQL Server example)
CREATE TYPE dbo.IdListType AS TABLE
(
    Id INT
);

-- Stored procedure accepting TVP
CREATE PROCEDURE GetProductsByIds
    @Ids dbo.IdListType READONLY
AS
BEGIN
    SELECT p.* 
    FROM products p
    INNER JOIN @Ids ids ON p.id = ids.Id
END
```

### Current Jaunty Limitation
```csharp
// Jaunty currently doesn't support TVPs directly
// Would require custom implementation or workaround
```

### Proposed TVP Support
```csharp
// Hypothetical TVP support in Jaunty
var ids = new[] { 1, 2, 3, 4, 5 };
var products = connection.Query<Product>(
    "EXEC GetProductsByIds @Ids",
    new { Ids = new SqlParameter("@Ids", SqlDbType.Structured)
          {
              TypeName = "dbo.IdListType",
              Value = DataTable.FromEnumerable(ids.Select(id => new { Id = id }))
          }
    });
```

## 2. Dictionary Support

### SQL for Dictionary Support
```sql
-- Simple query returning mixed data
SELECT 
    id, 
    name, 
    price, 
    category_id,
    created_date
FROM products 
WHERE category_id = @CategoryId
```

### Current Jaunty Limitation
```csharp
// Jaunty doesn't support direct dictionary mapping
// Would need to create a class or use custom mapper
```

### Proposed Dictionary Support
```csharp
// Hypothetical dictionary support
var results = connection.Query<Dictionary<string, object>>(
    "SELECT id, name, price, category_id FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });

// Access results
foreach (var row in results)
{
    var id = row["id"];
    var name = row["name"];
    var price = row["price"];
}
```

## 3. Stored Procedure Support

### SQL for Stored Procedure
```sql
CREATE PROCEDURE GetProductsWithSales
    @CategoryId INT,
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SELECT p.*, SUM(s.quantity) as TotalSales
    FROM products p
    LEFT JOIN sales s ON p.id = s.product_id 
        AND s.sale_date BETWEEN @StartDate AND @EndDate
    WHERE p.category_id = @CategoryId
    GROUP BY p.id, p.name, p.price, p.category_id
END
```

### Current Jaunty Usage (Generic)
```csharp
// Currently treated as regular query
var products = connection.Query<Product>(
    "EXEC GetProductsWithSales @CategoryId, @StartDate, @EndDate",
    new { CategoryId = 1, StartDate = DateTime.Now.AddDays(-30), EndDate = DateTime.Now });
```

### Proposed Stored Procedure Support
```csharp
// Hypothetical stored procedure support with better syntax
var products = connection.ExecuteStoredProcedure<Product>(
    "GetProductsWithSales",
    new { CategoryId = 1, StartDate = DateTime.Now.AddDays(-30), EndDate = DateTime.Now });

// Or with explicit command type
var products = connection.Query<Product>(
    "[GetProductsWithSales]",
    new { CategoryId = 1, StartDate = DateTime.Now.AddDays(-30), EndDate = DateTime.Now },
    commandType: CommandType.StoredProcedure);
```

## 4. Dynamic Object Support

### SQL for Dynamic Support
```sql
-- Ad-hoc query with varying columns
SELECT 
    p.name,
    p.price,
    c.name as CategoryName,
    s.name as SupplierName
FROM products p
JOIN categories c ON p.category_id = c.id
JOIN suppliers s ON p.supplier_id = s.id
WHERE p.category_id = @CategoryId
```

### Current Jaunty Limitation
```csharp
// Jaunty doesn't support dynamic objects directly
// Would need to define a concrete class
```

### Proposed Dynamic Support
```csharp
// Hypothetical dynamic support
var results = connection.Query<dynamic>(
    "SELECT name, price, category_name FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });

foreach (dynamic product in results)
{
    Console.WriteLine($"{product.name} costs ${product.price}");
    Console.WriteLine($"Category: {product.category_name}");
}
```

## 5. Dictionary Support (Additional Examples)

### SQL for Complex Dictionary Queries
```sql
-- Query returning configuration data
SELECT 
    config_key,
    config_value,
    data_type
FROM app_config
WHERE module = @Module
```

### Proposed Dictionary Support with Typed Values
```csharp
// Dictionary with typed values
var config = connection.Query<Dictionary<string, object>>(
    "SELECT config_key, config_value, data_type FROM app_config WHERE module = @Module",
    new { Module = "Email" })
    .ToDictionary(row => (string)row["config_key"], row => row["config_value"]);

// Or as a flat dictionary
var flatConfig = connection.Query<KeyValuePair<string, object>>(
    "SELECT config_key, config_value FROM app_config WHERE module = @Module",
    new { Module = "Email" })
    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
```

## 6. How Dictionary Differs from Dynamic

### Dictionary vs Dynamic Comparison
```csharp
// DICTIONARY: Strongly-typed key-value pairs, enumerable
Dictionary<string, object> dictResults = connection.Query<Dictionary<string, object>>(
    "SELECT id, name, price FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 }).First();

// Access via indexer - compile-time safe for keys
var id = (int)dictResults["id"];        // Explicit casting required
var name = (string)dictResults["name"];

// DYNAMIC: Runtime binding, no compile-time checking
dynamic dynResults = connection.Query<dynamic>(
    "SELECT id, name, price FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 }).First();

// Access via property - no casting needed, runtime errors possible
var id = dynResults.id;                 // Runtime binding
var name = dynResults.name;             // No compile-time checking

// Key differences:
// - Dictionary: Compile-time safety for existence, runtime for types
// - Dynamic: No compile-time safety, but cleaner syntax
// - Dictionary: Enumerable, can be serialized easily
// - Dynamic: Cannot be easily serialized, harder to debug
```

## 7. Buffered/Unbuffered Control

### Current Streaming in Jaunty
```csharp
// Jaunty's current streaming approach
var products = connection.QueryStream<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });

// This streams results one by one, not buffering in memory
```

### Proposed Buffered/Unbuffered Control
```csharp
// Hypothetical buffered control - explicit buffering option
var allProducts = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    buffered: true);  // Explicitly buffer all results

// Unbuffered - read one at a time (similar to current streaming but more flexible)
var productEnumerator = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    buffered: false);  // Don't buffer, enumerate on demand

// The difference: Current streaming is all-or-nothing, proposed approach gives
// fine-grained control over buffering behavior per query
```

## 8. IN Clause Helper

### SQL for IN Clause
```sql
-- Without helper (current Jaunty approach)
SELECT * FROM products WHERE id IN (1, 2, 3, 4, 5)

-- With helper (proposed)
SELECT * FROM products WHERE id IN @Ids
```

### Current Jaunty Limitation
```csharp
// Current approach - manual string building (unsafe) or multiple parameters
var ids = new[] { 1, 2, 3, 4, 5 };
var inClause = string.Join(",", ids);
var sql = $"SELECT * FROM products WHERE id IN ({inClause})"; // Unsafe!
var products = connection.Query<Product>(sql);
```

### Proposed IN Clause Helper
```csharp
// Hypothetical IN clause helper
var ids = new[] { 1, 2, 3, 4, 5 };
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE id IN @Ids",
    new { Ids = ids });  // Jaunty would expand this to (?, ?, ?, ?, ?)

// Or with explicit helper method
var products = connection.QueryIn<Product, int>(
    "SELECT * FROM products WHERE id",
    ids);

// Or with extension method
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE id IN @Ids",
    new { Ids = ids.AsInClauseParameter() });
```

## 9. Multi-Mapping Support

### Current QueryMultiple vs Multi-Mapping
```csharp
// Jaunty's QueryMultiple - separate result sets
using var gridReader = connection.QueryMultiple(
    "SELECT * FROM categories; SELECT * FROM products");

var categories = gridReader.Read<Category>().ToList();
var products = gridReader.Read<Product>().ToList();
// These are separate, unrelated objects
```

### Proposed Multi-Mapping Support
```csharp
// Multi-mapping - join multiple result sets into one complex object
public class CategoryWithProducts
{
    public Category Category { get; set; }
    public List<Product> Products { get; set; }
}

// Hypothetical multi-mapping support
var categoriesWithProducts = connection.Query<Category, Product, CategoryWithProducts>(
    "SELECT c.*, p.* FROM categories c " +
    "LEFT JOIN products p ON c.id = p.category_id " +
    "WHERE c.id IN @CategoryIds",
    (category, product) => // Split function
    {
        // How to combine the two objects
        var result = new CategoryWithProducts { Category = category };
        if (product != null)
        {
            result.Products ??= new List<Product>();
            result.Products.Add(product);
        }
        return result;
    },
    splitOn: "id", // Column where Product starts
    new { CategoryIds = new[] { 1, 2, 3 } });

// This is DIFFERENT from QueryMultiple because:
// - QueryMultiple: Separate result sets, unrelated objects
// - Multi-Mapping: One query, related objects joined together
```

## 10. Result Reader Optimization / Direct IDataReader Mapping

### Current Jaunty Approach
```csharp
// Jaunty allows passing a custom mapper function
var products = connection.Query<Product>(
    "SELECT id, name, price FROM products",
    CommandOptions<Product>.WithMapper(reader => new Product
    {
        Id = reader.GetInt32("id"),
        Name = reader.GetString("name"),
        Price = reader.GetDecimal("price")
    }));
```

### Proposed Direct IDataReader Utilities
```csharp
// Hypothetical direct reader utilities for maximum performance
public static class DataReaderExtensions
{
    public static T ReadAs<T>(this IDataReader reader) where T : new()
    {
        // Optimized direct mapping without reflection
        if (typeof(T) == typeof(Product))
        {
            return (T)(object)new Product
            {
                Id = reader.GetInt32("id"),
                Name = reader.IsDBNull("name") ? null : reader.GetString("name"),
                Price = reader.GetDecimal("price")
            };
        }
        // ... other type-specific optimizations
        return default(T);
    }
}

// Usage
using var reader = connection.ExecuteReader("SELECT id, name, price FROM products");
while (reader.Read())
{
    var product = reader.ReadAs<Product>(); // Direct, optimized mapping
    // Process product
}

// Or as a query method
var products = connection.ReadDirect<Product>(
    "SELECT id, name, price FROM products");

// Difference from current Func<IDataReader, T> approach:
// - Current: Generic function passed to Jaunty's mapping pipeline
// - Proposed: Direct, type-specific optimized readers bypassing reflection
// - Current: Still goes through Jaunty's property mapping system
// - Proposed: Bypasses mapping entirely for maximum performance
```