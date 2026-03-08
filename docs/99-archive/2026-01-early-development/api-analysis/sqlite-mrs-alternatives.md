# Alternative Approaches to Multiple Result Sets in SQLite

## Overview

SQLite has fundamental limitations with multiple result sets (MRS) that affect all ORMs similarly. This document outlines alternative approaches that can be used with Jaunty when working with SQLite to achieve similar functionality to multiple result sets.

## Alternative Approaches

### 1. Sequential Queries
Instead of executing a single query with multiple result sets, execute separate queries sequentially:

```csharp
// Instead of: QueryMultiple("SELECT * FROM Categories; SELECT * FROM Products;")
var categories = connection.Query<Category>("SELECT * FROM Categories");
var products = connection.Query<Product>("SELECT * FROM Products WHERE category_id IN @Ids", new { Ids = categories.Select(c => c.CategoryId) });
```

### 2. Batch Processing with Individual Queries
Execute related queries in a batch but handle them individually:

```csharp
using var transaction = connection.BeginTransaction();

var categories = connection.Query<Category>("SELECT * FROM Categories", transaction: transaction);
var products = connection.Query<Product>("SELECT * FROM Products WHERE category_id IN @CategoryIds", 
    new { CategoryIds = categories.Select(c => c.CategoryId) }, 
    transaction: transaction);

transaction.Commit();
```

### 3. JOIN-Based Queries with Multi-Poco Mapping
Use JOIN queries to retrieve related data in a single result set and map to multiple objects:

```csharp
// This approach is similar to PetaPoco's multi-poco queries
var sql = @"
    SELECT c.category_id AS CategoryId, c.category_name AS CategoryName, 
           p.product_id AS ProductId, p.product_name AS ProductName
    FROM categories c
    LEFT JOIN products p ON c.category_id = p.category_id";

var results = connection.Query<CategoryProductJoin>(sql);

// Then group or process the results as needed
```

### 4. Stored Procedures (When Supported)
For databases that support them, use stored procedures that return single result sets with complex data:

```csharp
// Though SQLite has limited stored procedure support, you can use complex queries
var combinedResults = connection.Query<CombinedResult>(
    "SELECT 'category' as Type, category_id as Id, category_name as Name FROM categories " +
    "UNION ALL " +
    "SELECT 'product' as Type, product_id as Id, product_name as Name FROM products");
```

### 5. Custom Result Aggregation
Create a custom query that aggregates related data into a single result structure:

```csharp
var sql = @"
    SELECT 
        c.category_id,
        c.category_name,
        c.description,
        GROUP_CONCAT(p.product_id) as ProductIds,
        GROUP_CONCAT(p.product_name) as ProductNames
    FROM categories c
    LEFT JOIN products p ON c.category_id = p.category_id
    GROUP BY c.category_id, c.category_name, c.description";

var aggregated = connection.Query<CategoryWithProductIds>(sql);
```

### 6. Use QueryPartial for Flexible Mapping
When you need to retrieve subsets of data, use `QueryPartial` to map only the columns that exist:

```csharp
// Instead of expecting all columns, map only what's available
var partialCategories = connection.QueryPartial<CategorySummary>("SELECT category_id, category_name FROM categories");
```

### 7. Streaming for Large Result Sets
For large datasets, use streaming to process data efficiently:

```csharp
// Process large result sets without loading everything into memory
await foreach (var category in connection.QueryStream<Category>("SELECT * FROM categories"))
{
    // Process each category individually
    await foreach (var product in connection.QueryStream<Product>("SELECT * FROM products WHERE category_id = @Id", new { Id = category.CategoryId }))
    {
        // Process each product
    }
}
```

### 8. Transaction-Based Approach
Group related operations within transactions for consistency:

```csharp
using var transaction = connection.BeginTransaction();

try
{
    var categories = await connection.QueryAsync<Category>("SELECT * FROM categories", transaction: transaction);
    
    foreach (var category in categories)
    {
        var products = await connection.QueryAsync<Product>(
            "SELECT * FROM products WHERE category_id = @CategoryId", 
            new { CategoryId = category.CategoryId }, 
            transaction: transaction);
        
        // Associate products with category
        category.Products = products.ToList();
    }
    
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### 9. Caching and Prefetching
Retrieve related data upfront and cache it for later use:

```csharp
// Fetch all related data in advance
var allCategories = connection.Query<Category>("SELECT * FROM categories");
var allProducts = connection.Query<Product>("SELECT * FROM products");

// Then organize in memory
var productsByCategory = allProducts.GroupBy(p => p.CategoryId)
                                   .ToDictionary(g => g.Key, g => g.ToList());

foreach (var category in allCategories)
{
    if (productsByCategory.TryGetValue(category.CategoryId, out var products))
        category.Products = products;
}
```

### 10. Hybrid Approach with Different Database Providers
For applications that need MRS functionality, consider using different database providers for different operations:

```csharp
// Use SQLite for simple queries and operations
var simpleData = sqliteConnection.Query<SimpleEntity>("SELECT * FROM simple_table");

// Use SQL Server or PostgreSQL for complex MRS operations
var complexData = sqlServerConnection.QueryMultiple("SELECT * FROM table1; SELECT * FROM table2;");
```

## Jaunty-Specific Recommendations

### 1. Automatic Fallback Detection
Implement automatic detection of SQLite and switch to sequential query execution:

```csharp
// Pseudo-code for Jaunty's internal logic
if (IsSQLiteProvider(connection))
{
    // Execute queries sequentially instead of using MRS
    return ExecuteSequentialQueries(sqlStatements, connection);
}
else
{
    // Use standard MRS approach
    return QueryMultiple(sql, connection);
}
```

### 2. Clear Error Messages and Guidance
Provide specific error messages when MRS is attempted on SQLite:

```csharp
if (databaseProvider == DatabaseProvider.SQLite)
{
    throw new NotSupportedException(
        "Multiple result sets are not supported by SQLite. " +
        "Consider using separate queries: connection.Query<T>(sql1); connection.Query<U>(sql2); " +
        "Or use JOIN queries to retrieve related data in a single result set.");
}
```

### 3. SQLite-Compatible API Alternatives
Create alternative APIs specifically designed for SQLite's limitations:

```csharp
// Jaunty could provide SQLite-specific methods
public static async Task<(List<T1>, List<T2>)> QueryBatchAsync<T1, T2>(
    this IDbConnection connection, 
    string sql1, 
    string sql2, 
    object parameters1 = null, 
    object parameters2 = null)
{
    // Execute queries sequentially
    var result1 = await connection.QueryAsync<T1>(sql1, parameters1);
    var result2 = await connection.QueryAsync<T2>(sql2, parameters2);
    return (result1, result2);
}
```

## Best Practices for SQLite with Jaunty

1. **Design queries with SQLite's limitations in mind**: Structure your queries to work within SQLite's single-result-set model
2. **Use JOINs when possible**: Retrieve related data in a single query rather than multiple result sets
3. **Leverage transactions**: Group related sequential queries in transactions for consistency
4. **Consider caching**: For frequently accessed related data, fetch and cache in memory
5. **Use QueryPartial**: When you don't need all entity properties, use partial mapping to avoid strict mapping failures
6. **Implement fallback strategies**: Have alternative approaches ready when MRS functionality is needed

## Conclusion

These alternatives allow developers to achieve similar functionality to multiple result sets while working within SQLite's limitations. The key is to structure queries and data retrieval in ways that work with SQLite's single-result-set model rather than trying to force multiple result set behavior. The approach you choose depends on your specific use case, performance requirements, and data relationships.