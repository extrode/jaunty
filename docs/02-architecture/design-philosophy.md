# Design Philosophy

Jaunty's design is guided by clear principles that prioritize performance, correctness, and developer experience.

## Core Philosophy

### 1. Strict Mapping by Default

**Problem**: Silent partial mapping causes bugs that surface far from their origin.

**Solution**: `Query<T>()` requires all entity properties to have matching columns.

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// This throws - missing 'Price' column
var products = connection.Query<Product>(
    "SELECT id, name FROM products");
// InvalidOperationException: "Strict mapping failed: property 'Price' has no matching column"
```

**Benefit**: Catches mismatches at development time, not in production.

**Escape hatch**: Use `QueryPartial<T>()` when you intentionally want partial data.

### 2. Zero Reflection at Runtime

**Problem**: Reflection is slow and allocates heavily.

**Solution**: Compile expression trees once, cache forever.

```csharp
internal static class MetadataCache<T> where T : new()
{
    static MetadataCache()
    {
        // Build metadata once
        // Compile setters once
        // Cache forever
    }
    
    public static readonly Action<T, IDataRecord, int>[] Setters;
}
```

**Benefit**: Query execution has zero reflection overhead.

### 3. Minimal Allocations

**Problem**: Allocations trigger GC, hurting performance.

**Solution**:
- Pre-size collections
- Use `Span<T>` for zero-allocation slicing
- Avoid LINQ in hot paths
- Cache everything possible

```csharp
// Pre-size list
var results = new List<T>(expectedRows);

// Use Span for parsing
ReadOnlySpan<char> sqlSpan = sql.AsSpan();
```

**Benefit**: Reduced GC pressure, better throughput.

### 4. Connection State Respect

**Problem**: ORMs that leave connections open or close unexpectedly cause issues.

**Solution**: Respect the initial connection state.

```csharp
var wasClosed = connection.State == ConnectionState.Closed;
try
{
    if (wasClosed) connection.Open();
    // Execute command
}
finally
{
    if (wasClosed && connection.State != ConnectionState.Closed)
        connection.Close();
}
```

**Benefit**: Predictable connection behavior.

### 5. Clear Error Messages

**Problem**: Cryptic error messages waste debugging time.

**Solution**: Include contextual information in all exceptions.

```
Bad: "Object reference not set to an instance of an object."

Good: "Strict mapping failed: property 'Price' on type 'Product' has no matching column. 
       SQL columns: [id, name]. Missing: [Price]."
```

**Benefit**: Faster debugging.

## What Jaunty Is NOT

| Feature | Jaunty's Stance |
|---------|-----------------|
| Query Builder | No - Write SQL explicitly |
| LINQ Translation | No - SQL is explicit |
| Change Tracking | No - You manage state |
| Lazy Loading | No - Explicit queries only |
| Migrations | No - Use separate tool |
| Unit of Work | No - Use transactions |

## What Jaunty IS

| Feature | Jaunty's Approach |
|---------|-------------------|
| SQL Execution | Your SQL, executed fast |
| Object Mapping | Strict or partial, your choice |
| Parameter Binding | Named or positional |
| Async Support | Full async/await |
| Multi-Mapping | Tuple-based `Query<T1, T2>` |
| Multiple Result Sets | `QueryMultiple` / `GridReader` |
| Bulk Operations | `BulkInsert`, `BulkUpdate`, `BulkDelete` |

## Trade-offs

### What We Sacrifice

1. **Convenience for Control**: No automatic query generation - you write SQL
2. **Magic for Predictability**: No hidden behavior - everything is explicit
3. **Features for Performance**: No change tracking, no lazy loading

### What We Gain

1. **Performance**: Compiled delegates, cached metadata
2. **Predictability**: No surprises - SQL executes as written
3. **Debuggability**: Clear error messages, no magic
4. **Flexibility**: Any valid SQL works

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Strict methods | `Query*` | `Query<T>`, `QueryFirst<T>` |
| Partial methods | `QueryPartial*` | `QueryPartial<T>`, `QueryPartialFirst<T>` |
| Async methods | `*Async` suffix | `QueryAsync<T>`, `QueryFirstAsync<T>` |
| Streaming methods | `*Stream` | `QueryStream<T>`, `QueryPartialStream<T>` |
| Scalar methods | `*Scalar` | `QueryScalar<T>`, `ExecuteScalar<T>` |

## Configuration Philosophy

**Rule**: Configure at startup, cache forever.

```csharp
// At application startup
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;

// First query triggers caching
var product = connection.Query<Product>(sql);
// MetadataCache<Product> is now initialized

// Changing config after first use has NO EFFECT on cached types
JauntyConfig.ColumnNameResolver = null;  // Ignored for Product
```

**Why**: Static generic caching is faster than per-call resolution.

## See Also

- [`performance.md`](performance.md) - Performance optimizations
- [`../../01-api-reference/configuration.md`](../../01-api-reference/configuration.md) - Configuration API
