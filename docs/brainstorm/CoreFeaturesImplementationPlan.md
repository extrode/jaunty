# Core Features Implementation Plan

## Overview

This document provides implementation plans for four critical missing features:
1. **Collection Parameter Expansion** - `WHERE id IN @ids`
2. **Dynamic Object Support** - `Query<dynamic>()`
3. **Dictionary Support** - `Query<Dictionary<string, object>>()` and `Query<Dictionary<string, T>>()`
4. **Multi-Mapping** - `Query<T1, T2, TResult>()` as syntactic sugar over `QueryMultiple`

---

## Feature 1: Collection Parameter Expansion

### Problem

Currently, this fails:
```csharp
var ids = new[] { 1, 2, 3 };
connection.Query<Product>("SELECT * FROM products WHERE id IN @Ids", new { Ids = ids });
// Throws: Parameter type not supported
```

Users want:
```csharp
var ids = new[] { 1, 2, 3 };
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE id IN @Ids",
    new { Ids = ids });
// Works! Expands to: WHERE id IN (@Ids0, @Ids1, @Ids2)
```

### Implementation

**File to modify:** `src/Jaunty/Internals/Parameters/ParameterBinder.cs`

**Algorithm:**
1. Detect if parameter value is a collection (implements `IEnumerable` but not `string`)
2. If collection, expand the SQL and create multiple parameters
3. Use StringBuilder for SQL rewriting
4. Handle edge cases: empty collections, null collections

```csharp
public static class ParameterBinder
{
    public static void Bind(IDbCommand command, object parameters)
    {
        // Existing: Parse SQL parameter names
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var meta = ParameterCache.Get(parameters.GetType());

        // NEW: Check for collection parameters and expand SQL if needed
        var (expandedSql, expansions) = ExpandCollectionParameters(
            command.CommandText,
            sqlParamNames,
            meta,
            parameters);

        if (expandedSql != null)
        {
            command.CommandText = expandedSql;
            // Re-parse the expanded SQL for binding
            sqlParamNames = SqlParameterParser.ExtractParameterNames(expandedSql);
        }

        // Create lookup for expanded parameters
        var expandedLookup = CreateExpandedLookup(expansions, meta, parameters);

        // Existing binding logic, but use expandedLookup
        BindParameters(command, sqlParamNames, expandedLookup);
    }

    private static (string? expandedSql, List<Expansion>? expansions) ExpandCollectionParameters(
        string sql,
        string[] paramNames,
        ParameterMetadata[] meta,
        object parameters)
    {
        List<Expansion>? expansions = null;

        foreach (var m in meta)
        {
            var value = m.Getter(parameters);
            if (value is null) continue;

            // Check if it's a collection (not string, not byte[])
            if (IsCollection(value, out var collection))
            {
                expansions ??= new List<Expansion>();
                expansions.Add(new Expansion(m.Name, collection.Cast<object>().ToList()));
            }
        }

        if (expansions is null) return (null, null);

        // Rewrite SQL
        var sb = new StringBuilder(sql);
        foreach (var exp in expansions.OrderByDescending(e => e.Position))
        {
            // Replace @ParamName with (@ParamName0, @ParamName1, ...)
            var replacement = exp.Count == 0
                ? "(SELECT 1 WHERE 1 = 0)"  // Empty collection: never matches
                : "(" + string.Join(", ", Enumerable.Range(0, exp.Count)
                    .Select(i => $"@{exp.Name}{i}")) + ")";

            // Find and replace @ParamName with expansion
            sb.Replace($"@{exp.Name}", replacement);
        }

        return (sb.ToString(), expansions);
    }

    private static bool IsCollection(object value, out IEnumerable? collection)
    {
        // Exclude string and byte[] (they implement IEnumerable but aren't "collections")
        if (value is string || value is byte[])
        {
            collection = null;
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            collection = enumerable;
            return true;
        }

        collection = null;
        return false;
    }
}

internal readonly struct Expansion(string name, List<object> values)
{
    public readonly string Name = name;
    public readonly List<object> Values = values;
    public int Count => Values.Count;
}
```

### Edge Cases

| Case | Input | Behavior |
|------|-------|----------|
| Empty array | `new { Ids = Array.Empty<int>() }` | Expands to `(SELECT 1 WHERE 1 = 0)` - never matches |
| Null array | `new { Ids = (int[]?)null }` | Expands to single `@Ids` with `DBNull.Value` |
| Single item | `new { Ids = new[] { 1 } }` | Expands to `(@Ids0)` |
| Large array | `new { Ids = Enumerable.Range(1, 1000) }` | Expands all 1000 - user's responsibility for limits |

### Tests

```csharp
[Fact]
public void Query_WithCollectionParameter_ExpandsInClause()
{
    var ids = new[] { 1, 2, 3 };
    var products = _db.Connection.Query<Product>(
        "SELECT * FROM products WHERE product_id IN @Ids",
        new { Ids = ids });

    Assert.Equal(3, products.Count);
}

[Fact]
public void Query_WithEmptyCollection_ReturnsEmpty()
{
    var ids = Array.Empty<int>();
    var products = _db.Connection.Query<Product>(
        "SELECT * FROM products WHERE product_id IN @Ids",
        new { Ids = ids });

    Assert.Empty(products);
}
```

---

## Feature 2 & 3: Dynamic, Dictionary, Tuple, and Multi-Entity Support

### Design Philosophy: Extend Query<T>, Don't Proliferate Methods

Instead of creating separate method families (`QueryDictionary`, `QueryDynamic`, `QueryTuple`), we extend the existing `Query<T>` pattern to handle special types internally. This is more elegant and consistent.

**The existing Query API already supports:**
- `Query<T>`, `QueryFirst<T>`, `QueryFirstOrDefault<T>`
- `QuerySingle<T>`, `QuerySingleOrDefault<T>`
- `QueryPartial<T>`, `QueryPartialFirst<T>`, etc.
- `QueryStream<T>`, `QueryPartialStream<T>`
- All async variants

**We extend this to support:**
- `Query<Dictionary<string, object>>` - Dictionary mapping
- `Query<Dictionary<string, TValue>>` - Typed dictionary mapping
- `Query<dynamic>` - Dynamic/ExpandoObject mapping
- `Query<(T1, T2)>` or `Query<T1, T2>` - Tuple/Multi-entity mapping
- `Query<KeyValuePair<TKey, TValue>>` - Two-column key-value mapping

### Honest Assessment: Dynamic vs Dictionary

| Aspect | `dynamic` | `Dictionary<string, object>` |
|--------|-----------|------------------------------|
| **Compile-time safety** | None | Key access is string-based |
| **Runtime safety** | Throws on missing member | Returns null or throws on key not found |
| **Iteration** | Not enumerable | Fully enumerable (keys, values) |
| **Performance** | DLR overhead (~10x slower) | Fast dictionary lookup |
| **Serialization** | Works with most serializers | Direct JSON/XML serialization |
| **IDE Support** | No IntelliSense | No IntelliSense for keys |
| **Use case** | Quick scripts, exploratory | Data processing, APIs, exports |

**Use Dynamic for:** Ad-hoc queries, admin tools, exploratory debugging
**Use Dictionary for:** APIs, exports, bulk processing, runtime-determined column names

---

### Implementation Strategy: Type Detection in Query<T>

The `Query<T>` method detects special types and routes to appropriate handlers:

```csharp
public static List<T> Query<T>(this IDbConnection connection, string sql, ...)
{
    var type = typeof(T);

    // Dictionary<string, object> or Dictionary<string, TValue>
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
    {
        return (List<T>)(object)QueryAsDictionary(connection, sql, type, ...);
    }

    // dynamic (appears as object at compile time)
    if (type == typeof(object))
    {
        return (List<T>)(object)QueryAsDynamic(connection, sql, ...);
    }

    // KeyValuePair<TKey, TValue>
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
    {
        return (List<T>)(object)QueryAsKeyValuePair(connection, sql, type, ...);
    }

    // ValueTuple - (T1, T2), (T1, T2, T3), etc.
    if (type.IsValueType && type.Name.StartsWith("ValueTuple"))
    {
        return (List<T>)(object)QueryAsTuple(connection, sql, type, ...);
    }

    // Normal entity mapping (existing path)
    return QueryCore<T>(connection, sql, ...);
}
```

---

### API Surface: All Methods Mirror Existing Query API

For each special type, all existing Query variants are available:

```csharp
// === Dictionary<string, object> ===
Query<Dictionary<string, object>>(sql)
QueryFirst<Dictionary<string, object>>(sql)
QueryFirstOrDefault<Dictionary<string, object>>(sql)
QuerySingle<Dictionary<string, object>>(sql)
QuerySingleOrDefault<Dictionary<string, object>>(sql)
QueryPartial<Dictionary<string, object>>(sql)           // Same as Query for dictionaries
QueryStream<Dictionary<string, object>>(sql)
// + all Async variants

// === Dictionary<string, TValue> (typed values) ===
Query<Dictionary<string, decimal>>(sql)                 // All values as decimal
QueryFirst<Dictionary<string, int>>(sql)                // All values as int
// + all variants

// === dynamic ===
Query<dynamic>(sql)                                     // Returns List<dynamic>
QueryFirst<dynamic>(sql)                                // Returns dynamic
QueryFirstOrDefault<dynamic>(sql)                       // Returns dynamic?
QuerySingle<dynamic>(sql)
QuerySingleOrDefault<dynamic>(sql)
QueryStream<dynamic>(sql)                               // Returns IEnumerable<dynamic>
// + all Async variants

// === KeyValuePair<TKey, TValue> (2-column) ===
Query<KeyValuePair<int, string>>(sql)                   // First col → Key, second → Value
QueryFirst<KeyValuePair<int, decimal>>(sql)
QueryStream<KeyValuePair<string, object>>(sql)
// + all variants

// === ValueTuple (positional) ===
Query<(int Id, string Name)>(sql)                       // Ordinal mapping
Query<(int, string, decimal)>(sql)                      // Unnamed tuple
QueryFirst<(int Id, string Name, decimal Price)>(sql)
// + all variants

// === Multi-Entity (T1, T2) ===
Query<Order, Customer>(sql)                             // Returns List<(Order, Customer)>
Query<Order, Customer, Order>(sql, map)                 // With combiner function
QueryFirst<Order, Customer>(sql)                        // Returns (Order, Customer)
QueryStream<Order, Customer>(sql)                       // Returns IEnumerable<(Order, Customer)>
// + all Async variants
```

---

### Usage Examples

```csharp
// Dictionary - iterate columns, serialize easily
var rows = connection.Query<Dictionary<string, object>>(
    "SELECT * FROM products WHERE category_id = @CatId",
    new { CatId = 1 });

foreach (var row in rows)
{
    foreach (var (column, value) in row)
        Console.WriteLine($"{column}: {value}");
}

// Typed Dictionary - all values as specific type
var prices = connection.Query<Dictionary<string, decimal>>(
    "SELECT product_name, unit_price FROM products");

// Dynamic - quick ad-hoc access
var user = connection.QueryFirst<dynamic>(
    "SELECT * FROM users WHERE id = @Id",
    new { Id = 1 });
Console.WriteLine(user.Name);  // No DTO needed

// KeyValuePair - simple lookups
var lookup = connection.Query<KeyValuePair<int, string>>(
    "SELECT id, name FROM categories");
var dict = lookup.ToDictionary(kv => kv.Key, kv => kv.Value);

// Tuple - positional, no entity class needed
var products = connection.Query<(int Id, string Name, decimal Price)>(
    "SELECT product_id, product_name, unit_price FROM products");

foreach (var (id, name, price) in products)
    Console.WriteLine($"{id}: {name} - ${price}");

// Multi-entity - JOIN queries without splitOn nonsense
var orders = connection.Query<Order, Customer>(
    @"SELECT
        o.id AS OrderId, o.order_date AS OrderDate, o.total,
        c.id AS CustomerId, c.name AS CustomerName, c.email
      FROM orders o
      JOIN customers c ON o.customer_id = c.id");

foreach (var (order, customer) in orders)
    Console.WriteLine($"Order {order.OrderId} for {customer.CustomerName}");
```

---

## Feature 4: Multi-Entity Mapping (Property-Name Matching)

### Problem with Dapper's splitOn Approach

Dapper's `splitOn` parameter is fragile and error-prone:

```csharp
// Dapper's approach - FRAGILE!
var orders = connection.Query<Order, Customer, Order>(
    @"SELECT o.id, o.order_date, c.id, c.name
      FROM orders o JOIN customers c ON o.customer_id = c.id",
    (order, customer) => { ... },
    splitOn: "id");  // Which "id"? The second one. Magic!
```

**Problems with splitOn:**
1. Column order matters - rearrange columns and it breaks
2. Duplicate column names require guessing which occurrence
3. `SELECT *` is dangerous - schema changes break mapping
4. No compile-time safety
5. Error messages are cryptic

### Our Approach: Property-Name Matching

Instead of positional column splitting, we match columns to properties by name. The user uses SQL aliases to disambiguate:

```csharp
// Jaunty's approach - EXPLICIT and SAFE
var orders = connection.Query<Order, Customer>(
    @"SELECT
        o.id AS OrderId, o.order_date AS OrderDate, o.total AS Total,
        c.id AS CustomerId, c.name AS CustomerName, c.email AS CustomerEmail
      FROM orders o
      JOIN customers c ON o.customer_id = c.id");

// Each column explicitly maps to a property name
// No ambiguity, no magic, no splitOn!
```

### How Property-Name Matching Works

1. Read all columns from the result set
2. For each column, find which entity type has a matching property
3. Build a setter for each (column → entity property) pair
4. Map each row by invoking the appropriate setters

```
Result columns:     OrderId, OrderDate, Total, CustomerId, CustomerName, CustomerEmail
                       ↓         ↓        ↓        ↓            ↓             ↓
Order properties:   OrderId, OrderDate, Total   (3 matches)
Customer props:                              CustomerId, CustomerName, CustomerEmail (3 matches)
```

### Disambiguation Rules

When a column name matches properties in multiple entity types:

| Scenario | Resolution |
|----------|------------|
| Column matches one type only | Map to that type |
| Column matches multiple types | First type wins (T1 before T2) |
| Column matches no types | Ignored (partial mapping) |

**Best Practice:** Use explicit aliases to avoid ambiguity:
- `o.id AS OrderId` instead of `o.id`
- `c.id AS CustomerId` instead of `c.id`

### API Design

```csharp
// Two-entity - returns tuple of both entities
public static List<(T1, T2)> Query<T1, T2>(
    this IDbConnection connection,
    string sql,
    object? parameters = null,
    CommandOptions options = default)
    where T1 : new()
    where T2 : new()

// Two-entity with combiner function
public static List<TResult> Query<T1, T2, TResult>(
    this IDbConnection connection,
    string sql,
    Func<T1, T2, TResult> map,
    object? parameters = null,
    CommandOptions<TResult> options = default)
    where T1 : new()
    where T2 : new()

// Three-entity
public static List<(T1, T2, T3)> Query<T1, T2, T3>(...)
public static List<TResult> Query<T1, T2, T3, TResult>(... Func<T1, T2, T3, TResult> map ...)

// First/Single variants
public static (T1, T2) QueryFirst<T1, T2>(...)
public static (T1, T2)? QueryFirstOrDefault<T1, T2>(...)
public static (T1, T2) QuerySingle<T1, T2>(...)
public static (T1, T2)? QuerySingleOrDefault<T1, T2>(...)

// Streaming
public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(...)

// All async variants
public static Task<List<(T1, T2)>> QueryAsync<T1, T2>(...)
// etc.
```

### Implementation

**New file:** `src/Jaunty/Read/QueryMultiEntity.cs`

```csharp
public static partial class Jaunty
{
    /// <summary>
    /// Executes a query and maps columns to multiple entity types by property name.
    /// Use SQL aliases to disambiguate columns (e.g., "o.id AS OrderId").
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2)>();

            // Build column-to-property mappings for each type
            var mapping = BuildMultiEntityMapping<T1, T2>(reader);

            while (reader.Read())
            {
                var t1 = new T1();
                var t2 = new T2();

                // Apply setters for T1
                foreach (var setter in mapping.T1Setters)
                    setter.Apply(t1, reader);

                // Apply setters for T2
                foreach (var setter in mapping.T2Setters)
                    setter.Apply(t2, reader);

                results.Add((t1, t2));
            }

            return results;
        });
    }

    /// <summary>
    /// Executes a query and maps to multiple types, then combines with a function.
    /// </summary>
    public static List<TResult> Query<T1, T2, TResult>(
        this IDbConnection connection,
        string sql,
        Func<T1, T2, TResult> map,
        object? parameters = null,
        CommandOptions<TResult> options = default)
        where T1 : new()
        where T2 : new()
    {
        ArgumentNullException.ThrowIfNull(map);

        return ExecuteReader(connection, sql, parameters, options.ToNonGeneric(), reader =>
        {
            var results = new List<TResult>();
            var mapping = BuildMultiEntityMapping<T1, T2>(reader);

            while (reader.Read())
            {
                var t1 = new T1();
                var t2 = new T2();

                foreach (var setter in mapping.T1Setters)
                    setter.Apply(t1, reader);
                foreach (var setter in mapping.T2Setters)
                    setter.Apply(t2, reader);

                results.Add(map(t1, t2));
            }

            return results;
        });
    }

    private static MultiEntityMapping<T1, T2> BuildMultiEntityMapping<T1, T2>(IDataReader reader)
        where T1 : new()
        where T2 : new()
    {
        var t1Props = MetadataCache<T1>.Metadata.Properties
            .ToDictionary(p => p.ColumnName, StringComparer.OrdinalIgnoreCase);
        var t2Props = MetadataCache<T2>.Metadata.Properties
            .ToDictionary(p => p.ColumnName, StringComparer.OrdinalIgnoreCase);

        var t1Setters = new List<PropertySetter<T1>>();
        var t2Setters = new List<PropertySetter<T2>>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);

            // Try T1 first (priority order)
            if (t1Props.TryGetValue(columnName, out var t1Prop))
            {
                t1Setters.Add(new PropertySetter<T1>(i, t1Prop));
                continue;  // T1 wins, don't check T2
            }

            // Then try T2
            if (t2Props.TryGetValue(columnName, out var t2Prop))
            {
                t2Setters.Add(new PropertySetter<T2>(i, t2Prop));
            }

            // If neither matches, column is ignored (partial mapping behavior)
        }

        return new MultiEntityMapping<T1, T2>(t1Setters, t2Setters);
    }
}

internal readonly struct MultiEntityMapping<T1, T2>(
    List<PropertySetter<T1>> t1Setters,
    List<PropertySetter<T2>> t2Setters)
{
    public readonly List<PropertySetter<T1>> T1Setters = t1Setters;
    public readonly List<PropertySetter<T2>> T2Setters = t2Setters;
}
```

### Usage Examples

```csharp
// Simple two-entity query - returns tuples
var results = connection.Query<Order, Customer>(
    @"SELECT
        o.id AS OrderId, o.order_date AS OrderDate, o.total AS Total,
        c.id AS CustomerId, c.name AS CustomerName, c.email AS CustomerEmail
      FROM orders o
      JOIN customers c ON o.customer_id = c.id
      WHERE o.id = @Id",
    new { Id = 123 });

foreach (var (order, customer) in results)
{
    Console.WriteLine($"Order {order.OrderId} for {customer.CustomerName}");
}

// With combiner function - build object graph
var ordersWithCustomers = connection.Query<Order, Customer, Order>(
    @"SELECT
        o.id AS OrderId, o.order_date AS OrderDate, o.total AS Total,
        c.id AS CustomerId, c.name AS CustomerName, c.email AS CustomerEmail
      FROM orders o
      JOIN customers c ON o.customer_id = c.id",
    (order, customer) =>
    {
        order.Customer = customer;  // Build navigation property
        return order;
    });

// Three-entity join
var fullOrders = connection.Query<Order, Customer, Address>(
    @"SELECT
        o.id AS OrderId, o.order_date AS OrderDate,
        c.id AS CustomerId, c.name AS CustomerName,
        a.id AS AddressId, a.street AS Street, a.city AS City
      FROM orders o
      JOIN customers c ON o.customer_id = c.id
      JOIN addresses a ON c.address_id = a.id");

foreach (var (order, customer, address) in fullOrders)
{
    Console.WriteLine($"{order.OrderId}: {customer.CustomerName} @ {address.City}");
}

// First variant
var (firstOrder, firstCustomer) = connection.QueryFirst<Order, Customer>(
    @"SELECT ... FROM orders o JOIN customers c ... ORDER BY o.order_date DESC");

// Streaming for large result sets
foreach (var (order, customer) in connection.QueryStream<Order, Customer>(sql))
{
    ProcessOrder(order, customer);
}
```

### Comparison: Dapper vs Jaunty Multi-Entity

| Aspect | Dapper (splitOn) | Jaunty (Property-Name) |
|--------|------------------|------------------------|
| Column order | Matters! | Doesn't matter |
| Duplicate column names | Confusing | Use aliases to disambiguate |
| `SELECT *` | Risky | Safe (maps by name) |
| Schema changes | May break silently | Fails fast if property missing |
| Error messages | "Split point not found" | "Property 'X' has no matching column" |
| Explicitness | Magic boundary | Explicit aliases |

### Best Practices

1. **Always use explicit aliases** - `o.id AS OrderId` not `o.id`
2. **Avoid SELECT *** - List columns explicitly for clarity
3. **Name properties to match** - If Order has `OrderId`, alias as `OrderId`
4. **Use the combiner function** when building navigation properties
5. **Consider QueryStream** for large result sets

---

## Implementation Order

### Phase 1: Collection Parameter Expansion
1. Modify `ParameterBinder.cs` to detect collections (IEnumerable, not string/byte[])
2. Implement SQL rewriting with StringBuilder
3. Handle edge cases: empty, null, single-item collections
4. Add tests for all scenarios
5. Document the feature

### Phase 2: Special Type Detection in Query<T>
1. Modify `Query<T>` dispatch in `QueryCore.cs` to detect special types
2. Implement type checking for Dictionary, dynamic (object), KeyValuePair, ValueTuple
3. Route each to appropriate internal handler

### Phase 3: Dictionary Support
1. Create `QueryDictionary.cs` (internal) with `QueryAsDictionary<TValue>` methods
2. Support `Dictionary<string, object>` and `Dictionary<string, TValue>`
3. Works automatically via `Query<Dictionary<string, object>>(sql)`
4. All variants work (First, Single, Stream, Async)

### Phase 4: Dynamic Support
1. Create `QueryDynamic.cs` (internal) with `QueryAsDynamic` using `ExpandoObject`
2. Works automatically via `Query<dynamic>(sql)`
3. All variants work (First, Single, Stream, Async)

### Phase 5: KeyValuePair and Tuple Support
1. Implement `QueryAsKeyValuePair` for two-column key-value results
2. Implement `QueryAsTuple` for positional ValueTuple mapping
3. Both work via generic `Query<T>` detection

### Phase 6: Multi-Entity Mapping
1. Create `QueryMultiEntity.cs` with `Query<T1, T2>`, `Query<T1, T2, T3>`, etc.
2. Implement property-name matching algorithm (no splitOn!)
3. Support combiner functions: `Query<T1, T2, TResult>(sql, map)`
4. Add First/Single/Stream/Async variants
5. Comprehensive tests for disambiguation scenarios

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `Internals/Parameters/ParameterBinder.cs` | Modify | Collection expansion |
| `Internals/QueryCore.cs` | Modify | Special type detection/dispatch |
| `Read/QueryDictionary.cs` | Create | Internal Dictionary mapping |
| `Read/QueryDictionaryAsync.cs` | Create | Async Dictionary mapping |
| `Read/QueryDynamic.cs` | Create | Internal Dynamic mapping |
| `Read/QueryDynamicAsync.cs` | Create | Async Dynamic mapping |
| `Read/QueryKeyValuePair.cs` | Create | Two-column key-value mapping |
| `Read/QueryTuple.cs` | Create | Positional ValueTuple mapping |
| `Read/QueryMultiEntity.cs` | Create | Multi-entity property-name mapping |
| `Read/QueryMultiEntityAsync.cs` | Create | Async multi-entity mapping |

---

## Test Files to Create

| File | Purpose |
|------|---------|
| `Unit/Parameters/CollectionExpansionTests.cs` | Collection parameter tests |
| `Integration/Read/QueryDictionaryTests.cs` | Dictionary query tests |
| `Integration/Read/QueryDynamicTests.cs` | Dynamic query tests |
| `Integration/Read/QueryKeyValuePairTests.cs` | KeyValuePair query tests |
| `Integration/Read/QueryTupleTests.cs` | ValueTuple query tests |
| `Integration/Read/QueryMultiEntityTests.cs` | Multi-entity mapping tests |

---

## Summary

| Feature | Effort | Impact | Priority |
|---------|--------|--------|----------|
| Collection Expansion | Medium | Critical | 1 |
| Dictionary Support | Low | High | 2 |
| Dynamic Support | Low | High | 3 |
| KeyValuePair/Tuple | Low | Medium | 4 |
| Multi-Entity Mapping | Medium | High | 5 |

Collection expansion is the most impactful single feature. Dictionary and Dynamic are straightforward additions that extend the existing `Query<T>` pattern. Multi-entity mapping with property-name matching is cleaner than Dapper's splitOn and provides significant value for JOIN-heavy applications.

### Key Design Principles

1. **Extend `Query<T>`, don't proliferate methods** - Users write `Query<Dictionary<string, object>>`, not `QueryDictionary`
2. **Property-name matching for multi-entity** - No magical splitOn, explicit SQL aliases instead
3. **All variants work** - First, Single, Stream, Async automatically available for all special types
4. **Fail fast** - Clear error messages when mapping fails
