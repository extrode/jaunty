# IQueryable<T> From<T>() Usage Examples and Benefits

## Overview
The `IQueryable<T> From<T>()` functionality would provide a type-safe, fluent interface for building queries without writing raw SQL. This would complement Jaunty's existing raw SQL approach by offering an alternative for developers who prefer LINQ-style queries.

## Current Jaunty Approach (Raw SQL)
```csharp
// Current approach - requires raw SQL
var products = connection.Query<Product>(
    "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE category_id = @CategoryId AND unit_price > @MinPrice",
    new { CategoryId = 1, MinPrice = 10.00m });

var expensiveProducts = connection.Query<Product>(
    "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE unit_price > @Price ORDER BY unit_price DESC LIMIT 10",
    new { Price = 100.00m });

var productCount = connection.QueryScalar<long>(
    "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });
```

## Proposed IQueryable<T> From<T>() Approach

### Basic Usage
```csharp
// Type-safe query building
var products = connection.From<Product>()
                        .Where(p => p.CategoryId == 1 && p.Price > 10.00m)
                        .ToList();

// Equivalent to:
// SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice, etc.
// FROM products 
// WHERE category_id = @p1 AND unit_price > @p2
```

### Advanced Filtering
```csharp
// Complex filtering with multiple conditions
var products = connection.From<Product>()
                        .Where(p => p.CategoryId == 1)
                        .Where(p => p.Price > 10.00m && p.Price < 100.00m)
                        .Where(p => p.Discontinued == false)
                        .ToList();

// Or chained in single Where
var products = connection.From<Product>()
                        .Where(p => p.CategoryId == 1 && 
                                   p.Price > 10.00m && 
                                   p.Price < 100.00m && 
                                   !p.Discontinued)
                        .ToList();
```

### Ordering and Pagination
```csharp
// Ordering
var products = connection.From<Product>()
                        .Where(p => p.CategoryId == 1)
                        .OrderBy(p => p.Price)
                        .ToList();

var productsDesc = connection.From<Product>()
                             .Where(p => p.CategoryId == 1)
                             .OrderByDescending(p => p.Price)
                             .ToList();

// Pagination
var productsPage = connection.From<Product>()
                            .Where(p => p.CategoryId == 1)
                            .OrderBy(p => p.Name)
                            .Skip(10)
                            .Take(20)
                            .ToList();
```

### Projection
```csharp
// Select specific properties (would map to matching properties in result)
var productSummaries = connection.From<Product>()
                                .Where(p => p.CategoryId == 1)
                                .Select(p => new ProductSummary { 
                                    ProductId = p.ProductId, 
                                    ProductName = p.ProductName 
                                })
                                .ToList();

// Or using anonymous types (would require dynamic mapping)
var productNames = connection.From<Product>()
                            .Where(p => p.CategoryId == 1)
                            .Select(p => new { p.ProductId, p.ProductName })
                            .ToList();
```

### Aggregation
```csharp
// Count
var count = connection.From<Product>()
                     .Where(p => p.CategoryId == 1)
                     .Count();

// Sum
var totalValue = connection.From<Product>()
                          .Where(p => p.CategoryId == 1)
                          .Sum(p => p.Price);

// Average
var averagePrice = connection.From<Product>()
                            .Where(p => p.CategoryId == 1)
                            .Average(p => p.Price);

// Max/Min
var maxPrice = connection.From<Product>()
                        .Where(p => p.CategoryId == 1)
                        .Max(p => p.Price);
var minPrice = connection.From<Product>()
                        .Where(p => p.CategoryId == 1)
                        .Min(p => p.Price);
```

### Complex Queries with Joins (if supported)
```csharp
// Inner join example (hypothetical)
var productCategories = connection.From<Product>()
                                 .Join<Category>(p => p.CategoryId, c => c.CategoryId)
                                 .Where(pc => pc.Item1.Price > 50.00m)
                                 .Select(pc => new ProductCategoryDto {
                                     ProductName = pc.Item1.ProductName,
                                     CategoryName = pc.Item2.CategoryName
                                 })
                                 .ToList();

// Or with navigation properties (if supported)
var productsWithCategories = connection.From<Product>()
                                      .Where(p => p.Category.Name.Contains("Beverages"))
                                      .ToList();
```

### Grouping
```csharp
// Group by example (hypothetical)
var productsByCategory = connection.From<Product>()
                                  .GroupBy(p => p.CategoryId)
                                  .Select(g => new CategorySummary {
                                      CategoryId = g.Key,
                                      ProductCount = g.Count(),
                                      AveragePrice = g.Average(p => p.Price)
                                  })
                                  .ToList();
```

### Async Support
```csharp
// Async versions
var products = await connection.From<Product>()
                              .Where(p => p.CategoryId == 1)
                              .ToListAsync();

var firstProduct = await connection.From<Product>()
                                  .Where(p => p.Price > 100.00m)
                                  .FirstOrDefaultAsync();

var count = await connection.From<Product>()
                           .Where(p => p.CategoryId == 1)
                           .CountAsync();
```

### Composition and Reusability
```csharp
// Query composition - build reusable query fragments
IQueryable<Product> GetActiveProducts(IDbConnection conn)
{
    return conn.From<Product>()
              .Where(p => !p.Discontinued);
}

IQueryable<Product> GetCategoryProducts(IDbConnection conn, int categoryId)
{
    return GetActiveProducts(conn)
              .Where(p => p.CategoryId == categoryId);
}

// Usage
var beverages = GetCategoryProducts(connection, 1)
                         .Where(p => p.Price > 10.00m)
                         .OrderBy(p => p.Name)
                         .ToList();
```

### Null Handling
```csharp
// Proper null handling
var products = connection.From<Product>()
                        .Where(p => p.SupplierId.HasValue && p.SupplierId.Value > 0)
                        .ToList();

var productsWithNullCheck = connection.From<Product>()
                                     .Where(p => p.Description != null && p.Description.Length > 10)
                                     .ToList();
```

### Collection Filtering
```csharp
// IN clause support
var categoryIds = new[] { 1, 2, 3 };
var products = connection.From<Product>()
                        .Where(p => categoryIds.Contains(p.CategoryId))
                        .ToList();

// Or with a method
var products = connection.From<Product>()
                        .WhereIn(p => p.CategoryId, categoryIds)
                        .ToList();
```

## Benefits to Users

### 1. Type Safety
- Compile-time checking of property names
- No typos in column names that would only be caught at runtime
- IntelliSense support throughout query building

### 2. Productivity
- Faster development for simple to medium complexity queries
- Less SQL knowledge required for basic operations
- Familiar LINQ syntax for .NET developers

### 3. Maintainability
- Refactoring support - rename properties and queries update automatically
- Less SQL string maintenance
- Easier to understand query intent

### 4. Reduced SQL Injection Risk
- Automatic parameterization
- No string concatenation for building queries

### 5. Database Agnostic Code
- Same LINQ expressions work across different database providers
- Less database-specific SQL to maintain

## When to Use Each Approach

### Use Raw SQL When:
- Complex queries with advanced SQL features
- Performance-critical queries requiring specific SQL optimizations
- Queries that are difficult to express in LINQ
- Full control over the generated SQL is needed
- Working with stored procedures or complex views

### Use IQueryable<T> From<T>() When:
- Simple to medium complexity queries
- Rapid prototyping and development
- Type safety is important
- Refactoring support is needed
- Working with business logic that benefits from LINQ expressions

## Implementation Considerations

The `IQueryable<T>` implementation would need to:
1. Translate LINQ expressions to appropriate SQL
2. Handle parameter binding automatically
3. Map results back to objects using existing Jaunty mapping logic
4. Maintain Jaunty's strict mapping behavior
5. Support both sync and async execution
6. Provide good error messages when translation fails

This approach would offer users a choice between Jaunty's core philosophy of "respecting your SQL" and a more convenient LINQ-based approach, without compromising the existing functionality.