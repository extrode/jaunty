# Unbuffering Query Implementations for Jaunty

## Overview
This document provides possible implementations of unbuffering queries for Jaunty, showing the differences between the current streaming API and potential unbuffered query approaches.

## Current Streaming API in Jaunty

### Current Implementation
```csharp
// Current Jaunty streaming
var products = connection.QueryStream<Product>("SELECT * FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 });

foreach (var product in products)
{
    // Process each product individually
    ProcessProduct(product);
    // Only current product is in memory
}
```

### SQL Examples for Current Streaming
```sql
-- Simple streaming query
SELECT id, name, price, category_id FROM products WHERE category_id = @CategoryId

-- Complex streaming query
SELECT p.id, p.name, p.price, c.name as CategoryName 
FROM products p 
JOIN categories c ON p.category_id = c.id 
WHERE p.created_date > @StartDate
```

## Possible Unbuffered Query Implementations

### 1. Direct IDataReader Implementation

#### Implementation
```csharp
public static class UnbufferedQueryExtensions
{
    public static IEnumerable<T> QueryUnbuffered<T>(this IDbConnection connection, string sql, object parameters = null) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            var map = DrDispatcher.Resolve(reader, default(CommandOptions<T>), MappingMode.Strict);

            while (reader.Read())
            {
                yield return map(reader);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
```

#### Usage Examples
```csharp
// Unbuffered query - processes one record at a time
var products = connection.QueryUnbuffered<Product>("SELECT * FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 });

foreach (var product in products)
{
    // Process each product individually
    ProcessProduct(product);
    // Only current product is in memory
}

// SQL for this usage
// SELECT * FROM products WHERE category_id = @CategoryId
```

### 2. Callback-Based Unbuffered Implementation

#### Implementation
```csharp
public static class CallbackUnbufferedQueryExtensions
{
    public static void QueryUnbuffered<T>(this IDbConnection connection, string sql, Action<T> processor, object parameters = null) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            var map = DrDispatcher.Resolve(reader, default(CommandOptions<T>), MappingMode.Strict);

            while (reader.Read())
            {
                var entity = map(reader);
                processor(entity);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
```

#### Usage Examples
```csharp
// Callback-based unbuffered query
connection.QueryUnbuffered<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId", 
    product => ProcessProduct(product), 
    new { CategoryId = 1 });

// SQL for this usage
// SELECT * FROM products WHERE category_id = @CategoryId
```

### 3. Partial Unbuffered Implementation

#### Implementation
```csharp
public static class PartialUnbufferedQueryExtensions
{
    public static IEnumerable<T> QueryPartialUnbuffered<T>(this IDbConnection connection, string sql, object parameters = null) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            var map = DrDispatcher.Resolve(reader, default(CommandOptions<T>), MappingMode.Projection); // Projection mode for partial mapping

            while (reader.Read())
            {
                yield return map(reader);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
```

#### Usage Examples
```csharp
// Partial unbuffered query - only maps columns that exist
public class ProductSummary
{
    public int Id { get; set; }
    public string Name { get; set; }
    // Price property is not selected in query, but that's OK with partial mapping
}

var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
    "SELECT id AS Id, name AS Name FROM products WHERE category_id = @CategoryId", 
    new { CategoryId = 1 });

foreach (var summary in summaries)
{
    // Process each summary
    ProcessProductSummary(summary);
}

// SQL for this usage
// SELECT id AS Id, name AS Name FROM products WHERE category_id = @CategoryId
```

### 4. Async Unbuffered Implementation

#### Implementation
```csharp
public static class AsyncUnbufferedQueryExtensions
{
    public static async IAsyncEnumerable<T> QueryUnbufferedAsync<T>(
        this DbConnection connection, 
        string sql, 
        object parameters = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
                ParameterBinder.Bind(command, parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var map = DrDispatcher.Resolve(reader, default(CommandOptions<T>), MappingMode.Strict);

            while (await reader.ReadAsync(cancellationToken))
            {
                yield return map(reader);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                await connection.CloseAsync();
        }
    }
}
```

#### Usage Examples
```csharp
// Async unbuffered query
await foreach (var product in connection.QueryUnbufferedAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId", 
    new { CategoryId = 1 }, 
    cancellationToken))
{
    await ProcessProductAsync(product);
}

// SQL for this usage
// SELECT * FROM products WHERE category_id = @CategoryId
```

## Differences Between Current Streaming and Proposed Unbuffered Approaches

### 1. Current Streaming vs Direct Unbuffered

| Aspect | Current Streaming (`QueryStream<T>`) | Proposed Direct Unbuffered |
|--------|-------------------------------------|----------------------------|
| **API Style** | Returns `IEnumerable<T>` | Returns `IEnumerable<T>` |
| **Connection Management** | Similar connection handling | Similar connection handling |
| **Memory Usage** | Constant (one object at a time) | Constant (one object at a time) |
| **Flexibility** | Standard streaming approach | More direct control over mapping |
| **Performance** | Good for large datasets | Potentially better for simple scenarios |

### 2. Current Streaming vs Callback-Based Unbuffered

| Aspect | Current Streaming | Callback-Based Unbuffered |
|--------|-------------------|---------------------------|
| **API Style** | Returns enumerable for foreach | Takes callback function |
| **Memory Usage** | Constant | Constant |
| **Processing Model** | Pull-based (consumer controls) | Push-based (library controls) |
| **Use Case** | General streaming | ETL operations, bulk processing |
| **Flexibility** | High (can store results) | Lower (process immediately) |

### 3. Current Streaming vs Partial Unbuffered

| Aspect | Current Streaming | Partial Unbuffered |
|--------|-------------------|--------------------|
| **Mapping Mode** | Strict (all properties must match) | Projection (partial mapping) |
| **Error Handling** | Throws on unmapped properties | Ignores unmapped properties |
| **Use Case** | Complete entities | DTOs, projections, summaries |
| **Performance** | Good | Potentially better (less mapping) |

## SQL Examples for Different Scenarios

### Large Dataset Processing (Current Streaming)
```sql
-- Processing large orders table
SELECT 
    o.order_id,
    o.customer_id,
    o.order_date,
    o.total_amount,
    c.customer_name,
    c.email
FROM orders o
JOIN customers c ON o.customer_id = c.customer_id
WHERE o.order_date >= @StartDate
ORDER BY o.order_date
```

### ETL Operations (Callback-Based Unbuffered)
```sql
-- Data migration scenario
SELECT 
    legacy_id,
    customer_name,
    email_address,
    phone_number,
    created_timestamp
FROM legacy_customers
WHERE migrated = 0
```

### Reporting/Analytics (Partial Unbuffered)
```sql
-- Summary report
SELECT 
    p.product_name,
    COUNT(o.order_id) as order_count,
    SUM(oi.quantity) as total_quantity,
    AVG(oi.unit_price) as avg_price
FROM products p
LEFT JOIN order_items oi ON p.product_id = oi.product_id
LEFT JOIN orders o ON oi.order_id = o.order_id
WHERE o.order_date >= @StartDate
GROUP BY p.product_id, p.product_name
HAVING COUNT(o.order_id) > 0
```

### Real-time Processing (Async Unbuffered)
```sql
-- Real-time data processing
SELECT 
    event_id,
    event_type,
    event_data,
    timestamp
FROM event_log
WHERE processed = 0
AND timestamp >= @LastProcessedTime
ORDER BY timestamp
```

## Benefits of Unbuffered Approaches

1. **Memory Efficiency**: Constant memory usage regardless of result set size
2. **Immediate Processing**: Start processing as soon as first record is available
3. **Scalability**: Handle datasets larger than available memory
4. **ETL Operations**: Perfect for data migration, reporting, and analytics
5. **Resource Management**: Better control over database connections and memory

## When to Use Each Approach

### Use Current Streaming When:
- Need to iterate results multiple times
- Want familiar foreach syntax
- Processing moderately large datasets
- Need to return results as enumerable

### Use Unbuffered Approaches When:
- Processing very large datasets
- Performing ETL operations
- Need maximum memory efficiency
- Processing data in real-time
- Building data pipelines