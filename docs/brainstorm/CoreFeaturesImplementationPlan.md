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

## Feature 2 & 3: Dynamic and Dictionary Support

### Honest Assessment: Do We Need Both?

**Short answer: Yes, but for different use cases.**

| Aspect | `dynamic` | `Dictionary<string, object>` |
|--------|-----------|------------------------------|
| **Compile-time safety** | None | Key access is string-based (no IntelliSense) |
| **Runtime safety** | Throws on missing member | Returns null/default or throws on key not found |
| **Iteration** | Not enumerable | Fully enumerable (keys, values) |
| **Performance** | DLR overhead (~10x slower) | Dictionary lookup (fast) |
| **Serialization** | Works with most serializers | Direct JSON/XML serialization |
| **IDE Support** | No IntelliSense | No IntelliSense for keys |
| **Use case** | Quick scripts, exploratory | Data processing, APIs, exports |

### Use Cases Where Dynamic is Better

```csharp
// 1. Quick exploratory queries in debugging/REPL
var result = connection.QueryFirst<dynamic>("SELECT * FROM users WHERE id = 1");
Console.WriteLine(result.Name);  // No DTO needed!

// 2. Unknown columns at compile time (user-defined tables)
foreach (dynamic row in connection.Query<dynamic>(customSql))
{
    // Access columns by name dynamically
    ProcessField(row.FieldName);
}

// 3. Ad-hoc admin tools
dynamic user = connection.QueryFirst<dynamic>("SELECT * FROM sys.users WHERE name = @n", new { n = "admin" });
```

### Use Cases Where Dictionary is Better

```csharp
// 1. When you need to iterate over columns
var row = connection.QueryFirst<Dictionary<string, object>>("SELECT * FROM users WHERE id = 1");
foreach (var (column, value) in row)
{
    Console.WriteLine($"{column}: {value}");
}

// 2. Serialization to JSON/XML
var data = connection.Query<Dictionary<string, object>>("SELECT * FROM products");
return JsonSerializer.Serialize(data);  // Clean JSON output

// 3. When column names are runtime-determined
string columnName = GetColumnFromUser();
var value = row[columnName];  // Dynamic can't do this without DLR

// 4. Bulk data processing pipelines
var records = connection.Query<Dictionary<string, object>>("SELECT * FROM logs");
records.Where(r => (int)r["level"] > 3).ToList();

// 5. Generic strongly-typed values
var prices = connection.Query<Dictionary<string, decimal>>(
    "SELECT product_name, unit_price FROM products");
// All values are decimals, not boxed objects
```

### Recommendation

**Implement both**, but with these priorities:
1. **Dictionary<string, object>** first - more explicit, easier to implement, better performance
2. **Dictionary<string, T>** second - type-safe variant
3. **Dynamic** third - convenience layer, uses DLR

---

## Feature 2: Dictionary Support Implementation

### API Design

```csharp
// Non-generic: all values as object
List<Dictionary<string, object>> Query(string sql, object? parameters = null)

// Generic: all values cast/converted to T
List<Dictionary<string, TValue>> Query<TValue>(string sql, object? parameters = null)
```

### Implementation

**New file:** `src/Jaunty/Read/QueryDictionary.cs`

```csharp
public static partial class Jaunty
{
    /// <summary>
    /// Queries and returns results as dictionaries with object values.
    /// </summary>
    public static List<Dictionary<string, object>> QueryDictionary(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<Dictionary<string, object>>();
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];

            // Cache column names
            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (reader.Read())
            {
                var dict = new Dictionary<string, object>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    dict[names[i]] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(dict);
            }

            return results;
        });
    }

    /// <summary>
    /// Queries and returns results as dictionaries with typed values.
    /// All values are converted to TValue.
    /// </summary>
    public static List<Dictionary<string, TValue>> QueryDictionary<TValue>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<Dictionary<string, TValue>>();
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];

            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (reader.Read())
            {
                var dict = new Dictionary<string, TValue>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                    {
                        dict[names[i]] = default!;
                    }
                    else
                    {
                        var value = reader.GetValue(i);
                        dict[names[i]] = (TValue)Convert.ChangeType(value, typeof(TValue));
                    }
                }
                results.Add(dict);
            }

            return results;
        });
    }
}
```

### Additional Methods

```csharp
// First variants
public static Dictionary<string, object> QueryDictionaryFirst(...)
public static Dictionary<string, object>? QueryDictionaryFirstOrDefault(...)

// Async variants
public static Task<List<Dictionary<string, object>>> QueryDictionaryAsync(...)
public static Task<Dictionary<string, object>> QueryDictionaryFirstAsync(...)

// Streaming
public static IEnumerable<Dictionary<string, object>> QueryDictionaryStream(...)
public static IAsyncEnumerable<Dictionary<string, object>> QueryDictionaryStreamAsync(...)
```

---

## Feature 3: Dynamic Object Support

### Implementation

**New file:** `src/Jaunty/Read/QueryDynamic.cs`

Uses `System.Dynamic.ExpandoObject` which implements `IDictionary<string, object>`.

```csharp
public static partial class Jaunty
{
    /// <summary>
    /// Queries and returns results as dynamic objects.
    /// </summary>
    public static List<dynamic> QueryDynamic(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<dynamic>();
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];

            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (reader.Read())
            {
                var expando = new ExpandoObject();
                var dict = (IDictionary<string, object?>)expando;

                for (int i = 0; i < fieldCount; i++)
                {
                    dict[names[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                results.Add(expando);
            }

            return results;
        });
    }

    public static dynamic QueryDynamicFirst(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                throw new InvalidOperationException("Sequence contains no elements.");

            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            var fieldCount = reader.FieldCount;

            for (int i = 0; i < fieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            return expando;
        });
    }

    public static dynamic? QueryDynamicFirstOrDefault(...)
    public static dynamic QueryDynamicSingle(...)
    public static dynamic? QueryDynamicSingleOrDefault(...)

    // Async variants
    public static Task<List<dynamic>> QueryDynamicAsync(...)
    // etc.
}
```

### Alternative: Using Query<dynamic> Generic Constraint

We could also support `Query<dynamic>` by detecting the type parameter:

```csharp
// In existing Query<T> method, add special case
public static List<T> Query<T>(this IDbConnection connection, string sql, ...)
{
    // Special case for dynamic
    if (typeof(T) == typeof(object) && /* some way to detect dynamic intent */)
    {
        return (List<T>)(object)QueryDynamic(connection, sql, ...);
    }

    // Normal path
    ...
}
```

**Recommendation:** Use explicit `QueryDynamic()` methods rather than overloading `Query<T>` - clearer intent, no magic.

---

## Feature 4: Multi-Mapping (Joined Tables)

### Problem

Currently:
```csharp
// Verbose approach with QueryMultiple
using var multi = connection.QueryMultiple(@"
    SELECT * FROM orders WHERE id = @Id;
    SELECT * FROM customers WHERE id = @CustomerId;",
    new { Id = orderId, CustomerId = customerId });

var order = multi.ReadFirst<Order>();
var customer = multi.ReadFirst<Customer>();
order.Customer = customer;
```

Users want (Dapper-style):
```csharp
var orderWithCustomer = connection.Query<Order, Customer, Order>(
    @"SELECT o.*, c.*
      FROM orders o
      JOIN customers c ON o.customer_id = c.id
      WHERE o.id = @Id",
    (order, customer) => { order.Customer = customer; return order; },
    new { Id = orderId },
    splitOn: "customer_id");
```

### Design Decision: Syntactic Sugar over QueryMultiple

Rather than implementing true multi-mapping (which requires complex column splitting logic), we'll provide a simpler API that's syntactic sugar over what users already can do.

### API Design

```csharp
// Two-type join
public static List<TResult> Query<T1, T2, TResult>(
    this IDbConnection connection,
    string sql,
    Func<T1, T2, TResult> map,
    object? parameters = null,
    string splitOn = "Id",
    CommandOptions<TResult> options = default)

// Three-type join
public static List<TResult> Query<T1, T2, T3, TResult>(
    this IDbConnection connection,
    string sql,
    Func<T1, T2, T3, TResult> map,
    object? parameters = null,
    string splitOn = "Id",
    CommandOptions<TResult> options = default)
```

### Implementation Strategy

**Option A: True Column Splitting (Dapper-style)**
- Complex: Must parse column names, find splitOn boundaries, create multiple readers
- Fragile: Column ordering matters
- Performance: Single result set, but complex mapping

**Option B: Multiple Result Sets (Wrapper over QueryMultiple)**
- Simple: User writes multiple SELECTs, we just combine them
- Robust: Each entity is cleanly separated
- Limitation: Can't do single JOIN query

**Option C: Hybrid with Column Index Hints**
- User provides column count or indices for each type
- We slice the columns accordingly

### Recommended: Option A (True Multi-Mapping)

Dapper's approach is the standard. Users expect this to work:

```csharp
var results = connection.Query<Order, Customer, Order>(
    @"SELECT o.id, o.order_date, o.total, c.id, c.name, c.email
      FROM orders o
      JOIN customers c ON o.customer_id = c.id",
    (order, customer) => { order.Customer = customer; return order; },
    splitOn: "id");  // Second "id" column starts Customer
```

### Implementation

**New file:** `src/Jaunty/Read/QueryMultiMap.cs`

```csharp
public static partial class Jaunty
{
    /// <summary>
    /// Executes a query that returns a JOIN result and maps to multiple types.
    /// </summary>
    /// <param name="splitOn">
    /// The column name that starts each new type. Default is "Id".
    /// For multiple split points, use comma-separated: "Id,CustomerId"
    /// </param>
    public static List<TResult> Query<T1, T2, TResult>(
        this IDbConnection connection,
        string sql,
        Func<T1, T2, TResult> map,
        object? parameters = null,
        string splitOn = "Id",
        CommandOptions<TResult> options = default)
        where T1 : new()
        where T2 : new()
    {
        ArgumentNullException.ThrowIfNull(map);

        var splitColumns = splitOn.Split(',').Select(s => s.Trim()).ToArray();
        if (splitColumns.Length != 1)
            throw new ArgumentException("For two types, provide one split column.");

        return ExecuteReader(connection, sql, parameters, options.ToNonGeneric(), reader =>
        {
            var results = new List<TResult>();

            // Find the split point
            var splitIndex = FindSplitIndex(reader, splitColumns[0]);

            // Get setters for each type
            var setters1 = MetadataCache<T1>.GetSetters(reader, 0, splitIndex, MappingMode.Partial);
            var setters2 = MetadataCache<T2>.GetSetters(reader, splitIndex, reader.FieldCount - splitIndex, MappingMode.Partial);

            while (reader.Read())
            {
                var t1 = new T1();
                var t2 = new T2();

                // Map first type (columns 0 to splitIndex-1)
                ApplySetters(t1, reader, setters1, 0);

                // Map second type (columns splitIndex to end)
                ApplySetters(t2, reader, setters2, splitIndex);

                results.Add(map(t1, t2));
            }

            return results;
        });
    }

    private static int FindSplitIndex(IDataReader reader, string splitOn)
    {
        // Find the SECOND occurrence of a column matching splitOn
        // (First occurrence is assumed to be the first type)
        bool foundFirst = false;

        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(splitOn, StringComparison.OrdinalIgnoreCase))
            {
                if (foundFirst)
                    return i;  // Second occurrence
                foundFirst = true;
            }
        }

        // If splitOn not found twice, assume it's the start of second type
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(splitOn, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new InvalidOperationException(
            $"Column '{splitOn}' not found. Cannot determine split point.");
    }
}
```

### Three+ Type Variant

```csharp
public static List<TResult> Query<T1, T2, T3, TResult>(
    this IDbConnection connection,
    string sql,
    Func<T1, T2, T3, TResult> map,
    object? parameters = null,
    string splitOn = "Id",
    CommandOptions<TResult> options = default)
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    var splitColumns = splitOn.Split(',').Select(s => s.Trim()).ToArray();
    if (splitColumns.Length != 2)
        throw new ArgumentException("For three types, provide two split columns (comma-separated).");

    // Find both split points
    var (split1, split2) = FindTwoSplitIndices(reader, splitColumns);

    // Map each type to its column range
    // T1: 0 to split1-1
    // T2: split1 to split2-1
    // T3: split2 to end
    ...
}
```

### MetadataCache Modification

Need to modify `MetadataCache<T>.GetSetters()` to accept column offset and count:

```csharp
// Existing signature
public static PropertySetter<T>[] GetSetters(IDataReader reader, MappingMode mode)

// New signature with range support
public static PropertySetter<T>[] GetSetters(
    IDataReader reader,
    int startColumn,
    int columnCount,
    MappingMode mode)
```

### Usage Examples

```csharp
// Two-type mapping
var orders = connection.Query<Order, Customer, Order>(
    @"SELECT o.id, o.order_date, o.total,
             c.id, c.name, c.email
      FROM orders o
      JOIN customers c ON o.customer_id = c.id",
    (order, customer) =>
    {
        order.Customer = customer;
        return order;
    },
    splitOn: "id");  // Second "id" starts Customer

// Three-type mapping
var results = connection.Query<Order, Customer, Address, OrderDetail>(
    @"SELECT o.*, c.*, a.*
      FROM orders o
      JOIN customers c ON o.customer_id = c.id
      JOIN addresses a ON c.address_id = a.id",
    (order, customer, address) =>
    {
        customer.Address = address;
        order.Customer = customer;
        return new OrderDetail { Order = order };
    },
    splitOn: "id,id");  // Two split points
```

---

## Implementation Order

### Phase 1: Collection Parameter Expansion
1. Modify `ParameterBinder.cs` to detect collections
2. Implement SQL rewriting
3. Add tests for edge cases
4. Document the feature

### Phase 2: Dictionary Support
1. Create `QueryDictionary.cs` with basic methods
2. Add generic `QueryDictionary<TValue>` variant
3. Add First/Single/Stream variants
4. Add async variants
5. Add tests

### Phase 3: Dynamic Support
1. Create `QueryDynamic.cs` using `ExpandoObject`
2. Add all variants (First, Single, Stream, Async)
3. Add tests

### Phase 4: Multi-Mapping
1. Modify `MetadataCache` to support column ranges
2. Create `QueryMultiMap.cs` with two-type variant
3. Add three-type and four-type variants
4. Add async variants
5. Add comprehensive tests

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `Internals/Parameters/ParameterBinder.cs` | Modify | Collection expansion |
| `Read/QueryDictionary.cs` | Create | Dictionary mapping |
| `Read/QueryDictionaryAsync.cs` | Create | Async dictionary |
| `Read/QueryDynamic.cs` | Create | Dynamic mapping |
| `Read/QueryDynamicAsync.cs` | Create | Async dynamic |
| `Read/QueryMultiMap.cs` | Create | Multi-type mapping |
| `Read/QueryMultiMapAsync.cs` | Create | Async multi-mapping |
| `Internals/Entity/MetadataCache.cs` | Modify | Column range support |

---

## Test Files to Create

| File | Purpose |
|------|---------|
| `Unit/Parameters/CollectionExpansionTests.cs` | Collection parameter tests |
| `Integration/Read/QueryDictionaryTests.cs` | Dictionary query tests |
| `Integration/Read/QueryDynamicTests.cs` | Dynamic query tests |
| `Integration/Read/QueryMultiMapTests.cs` | Multi-mapping tests |

---

## Summary

| Feature | Effort | Impact | Priority |
|---------|--------|--------|----------|
| Collection Expansion | Medium | Critical | 1 |
| Dictionary Support | Low | High | 2 |
| Dynamic Support | Low | High | 3 |
| Multi-Mapping | High | High | 4 |

The collection expansion is the most impactful single feature. Dictionary and Dynamic are straightforward additions. Multi-mapping is complex but provides significant value for JOIN-heavy applications.
