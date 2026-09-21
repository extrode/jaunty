# Jaunty Examples

This document provides comprehensive examples of using the Jaunty micro-ORM for various scenarios.

## Basic Query Examples

### Query with Strict Mapping (All Properties Must Match)
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
}

// Strict mapping - all properties must have matching columns
var products = connection.Query<Product>(
    "SELECT id AS Id, name AS Name, price AS Price, category_id AS CategoryId FROM products");

// This will throw if any property doesn't have a matching column
// InvalidOperationException: "Strict mapping failed: Property 'Price' was mapped more than once..."
```

### Query with Partial Mapping (Only Matching Columns)
```csharp
public class ProductSummary
{
    public int Id { get; set; }
    public string Name { get; set; }
    // Note: Price property is not selected in query, but that's OK with QueryPartial
}

// Partial mapping - only map what exists
var summaries = connection.QueryPartial<ProductSummary>(
    "SELECT id AS Id, name AS Name FROM products");

// Extra columns in result are ignored
var summaries2 = connection.QueryPartial<ProductSummary>(
    "SELECT id AS Id, name AS Name, price, category_id FROM products");
```

## Parameter Binding Examples

### Named Parameters
```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId AND price > @MinPrice",
    new { CategoryId = 1, MinPrice = 10.00m });
```

### Positional Parameters
```csharp
// Single parameter
var product = connection.Query<Product>(
    "SELECT * FROM products WHERE id = @Id",
    42);

// Multiple positional parameters
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId AND price BETWEEN @MinPrice AND @MaxPrice",
    1, 10.00m, 100.00m);  // @CategoryId=1, @MinPrice=10.00, @MaxPrice=100.00

// Array parameters
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId AND price > @MinPrice",
    new object[] { 1, 10.00m });
```

### Parameter Validation
```csharp
// This will throw immediately with clear error message
try
{
    connection.Query<Product>(
        "SELECT * FROM products WHERE category_id = @CategoryId AND price > @MinPrice",
        1);  // Only 1 value provided for 2 parameters
}
catch (ArgumentException ex)
{
    // "Parameter count mismatch: SQL contains 2 unique parameter(s), but 1 value(s) provided."
}
```

## Async Examples

### Basic Async Query
```csharp
var products = await connection.QueryAsync<Product>(
    "SELECT id AS Id, name AS Name, price AS Price, category_id AS CategoryId FROM products",
    cancellationToken);

// With parameters
var products = await connection.QueryAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    cancellationToken);
```

### Async with Command Options
```csharp
var products = await connection.QueryAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    CommandOptions.WithTimeout(30),  // 30 second timeout
    cancellationToken);
```

## Scalar and Single Value Examples

### Scalar Values
```csharp
// Get count
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

// Get max price
var maxPrice = connection.QueryScalar<decimal>("SELECT MAX(price) FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 });

// Async scalar
var countAsync = await connection.QueryScalarAsync<long>("SELECT COUNT(*) FROM products");
```

### Single and First Results
```csharp
// Get first product
var firstProduct = connection.QueryFirst<Product>("SELECT * FROM products LIMIT 1");

// Get first or default (null if no results)
var product = connection.QueryFirstOrDefault<Product>("SELECT * FROM products WHERE id = @Id", new { Id = 999 });

// Get single product (throws if more than one result)
var product = connection.QuerySingle<Product>("SELECT * FROM products WHERE id = @Id", new { Id = 1 });

// Get single or default
var product = connection.QuerySingleOrDefault<Product>("SELECT * FROM products WHERE id = @Id", new { Id = 999 });
```

## Transaction and Timeout Examples

### Using Transactions
```csharp
using var transaction = connection.BeginTransaction();

try
{
    var products = connection.Query<Product>(
        "SELECT * FROM products WHERE category_id = @CategoryId",
        new { CategoryId = 1 },
        CommandOptions.WithTransaction(transaction));

    // Other operations in transaction...
    
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### With Timeouts
```csharp
// 30-second timeout
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    CommandOptions.WithTimeout(30));

// Async with timeout
var products = await connection.QueryAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    CommandOptions.WithTimeout(30),
    cancellationToken);
```

### Combined Transaction and Timeout
```csharp
using var transaction = connection.BeginTransaction();

var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    CommandOptions.With(transaction, timeoutSeconds: 30));

// Async version
var products = await connection.QueryAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    CommandOptions.With(transaction, timeoutSeconds: 30),
    cancellationToken);
```

## Multiple Result Sets (GridReader)

### Query Multiple Results
```csharp
using var gridReader = connection.QueryMultiple(
    "SELECT * FROM categories; SELECT * FROM products; SELECT COUNT(*) FROM orders");

var categories = gridReader.Read<Category>().ToList();
var products = gridReader.Read<Product>().ToList();
var orderCount = gridReader.ReadScalar<long>();
```

### Async Multiple Results
```csharp
using var gridReader = await connection.QueryMultipleAsync(
    "SELECT * FROM categories; SELECT * FROM products; SELECT COUNT(*) FROM orders",
    cancellationToken);

var categories = (await gridReader.ReadAsync<Category>()).ToList();
var products = (await gridReader.ReadAsync<Product>()).ToList();
var orderCount = await gridReader.ReadScalarAsync<long>();
```

## Attribute-Based Mapping

### Using Attributes for Custom Mapping
```csharp
[Table("product_table")]
public class Product
{
    [Column("product_id")]
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; }

    [Column("unit_price")]
    public decimal Price { get; set; }

    [Ignore]  // This property will be ignored during mapping
    public string ComputedValue { get; set; }
}

// Query using attribute-defined column names
var products = connection.QueryPartial<Product>(
    "SELECT product_id, product_name, unit_price FROM product_table");
```

## Configuration Examples

### Global Configuration for Naming Conventions
```csharp
// At application startup
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;  // ProductName -> product_name
JauntyConfig.TableNameResolver = NamingConvention.SnakeCasePluralTable;  // Product -> products

// Now property 'ProductName' maps to column 'product_name'
// And class 'Product' maps to table 'products'
```

### Custom Naming Conventions
```csharp
// Custom table naming
JauntyConfig.TableNameResolver = type => $"tbl_{type.Name.ToLower()}";

// Custom column naming
JauntyConfig.ColumnNameResolver = propertyName => $"col_{propertyName.ToLower()}";
```

## NULL Handling Examples

### Handling NULL Values
```csharp
public class Product
{
    public int Id { get; set; }           // Non-nullable: NULL from DB throws InvalidOperationException
    public string Name { get; set; }      // Reference type: NULL becomes null
    public int? CategoryId { get; set; }  // Nullable: NULL becomes null
    public DateTime? DiscontinuedDate { get; set; }  // Nullable: NULL becomes null
}

var products = connection.Query<Product>(
    "SELECT id, name, category_id, discontinued_date FROM products");

// Non-nullable value types will throw if database returns NULL
// Use nullable types for columns that might be NULL
```

## Streaming Examples (for Large Result Sets)

### Streaming Results
```csharp
// Sync streaming
var products = connection.QueryStream<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });

foreach (var product in products)
{
    // Process each product without loading all into memory
    Console.WriteLine(product.Name);
}

// Async streaming (IAsyncEnumerable)
await foreach (var product in connection.QueryStreamAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    cancellationToken))
{
    Console.WriteLine(product.Name);
}
```

## Error Handling Examples

### Common Error Scenarios
```csharp
try
{
    // This will throw if 'Price' column is missing (strict mapping)
    var products = connection.Query<Product>(
        "SELECT id, name, category_id FROM products");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Strict mapping failed"))
{
    // Handle mapping mismatch
    Console.WriteLine($"Mapping error: {ex.Message}");
}

try
{
    // This will throw if parameter count doesn't match
    var products = connection.Query<Product>(
        "SELECT * FROM products WHERE category_id = @CategoryId AND price > @MinPrice",
        1);  // Only provided one value for two parameters
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Parameter error: {ex.Message}");
}
```

## Command Options Examples

### Using Command Options for Advanced Scenarios
```csharp
// Custom mapper (for complex scenarios)
var customMapped = connection.Query<Product>(
    "SELECT * FROM products",
    CommandOptions<Product>.WithMapper(reader => new Product
    {
        Id = reader.GetInt32("id"),
        Name = reader.GetString("name"),
        Price = reader.GetDecimal("price")
    }));

// Combine transaction and timeout
using var transaction = connection.BeginTransaction();
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    CommandOptions.With(transaction, timeoutSeconds: 30));

// Just timeout
var products = connection.Query<Product>(
    "SELECT * FROM products",
    CommandOptions.WithTimeout(15));
```