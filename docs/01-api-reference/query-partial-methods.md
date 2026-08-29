# Partial Query Methods

## Overview

The `QueryPartial*` family maps a result set in **projection mode**: only properties that have a
matching column are set, and everything else keeps its default value. The `Query*` family maps in
**strict mode**, where the entity shape and the result-set shape have to agree in both directions.

Every partial method is a shape-for-shape twin of a strict one:

| Strict | Partial | Returns |
|---|---|---|
| `Query<T>` | `QueryPartial<T>` | `List<T>` |
| `QueryFirst<T>` | `QueryPartialFirst<T>` | `T` (throws when empty) |
| `QueryFirstOrDefault<T>` | `QueryPartialFirstOrDefault<T>` | `T?` |
| `QuerySingle<T>` | `QueryPartialSingle<T>` | `T` (throws when empty or when more than one row) |
| `QuerySingleOrDefault<T>` | `QueryPartialSingleOrDefault<T>` | `T?` |
| `QueryStream<T>` | `QueryPartialStream<T>` | `IEnumerable<T>` / `IAsyncEnumerable<T>` |
| (none) | `QueryPartialUnbuffered<T>` | `IEnumerable<T>` / `IAsyncEnumerable<T>` |
| (none) | `QueryPartialList` | `List<IDictionary<string, object?>>` |

Streaming variants are documented in [`streaming-methods.md`](streaming-methods.md). Overload-by-overload
detail for `QueryPartial<T>` is in [`query-methods.md`](query-methods.md#partial-mapping-variants);
for the single-result family, in [`single-result-methods.md`](single-result-methods.md#partial-mapping-variants).

---

## What "strict" and "partial" mean

`MappingMode` (`src/Jaunty/Configuration/MappingMode.cs`) has two members, `Strict` and `Projection`.
Partial methods request `Projection`; everything else requests `Strict`.

Strict mapping fails in **both** directions, which is more than the name suggests:

| Condition | Strict | Partial |
|---|---|---|
| Result column has no matching property | `InvalidOperationException`: *Mapping failed: Column 'X' does not map to any property of type 'T'* | column ignored |
| Property has no matching column | `InvalidOperationException`: *Strict mapping failed: property 'X' has no matching column in result set for type 'T'* | property left at its default |
| Duplicate column name | first match wins, the rest are skipped | same |

Both checks live in `MetadataCache<T>.BuildSetters`. They run once per distinct result-set shape,
not once per row.

So `SELECT id, name FROM products` into a `Product` with four properties throws under `Query<T>` and
succeeds under `QueryPartial<T>`. That is the whole distinction.

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string? Description { get; set; }
}

// Throws: Price and Description have no matching column.
var bad = connection.Query<Product>("SELECT id, name FROM products");

// Succeeds: Price is 0m, Description is null.
var ok = connection.QueryPartial<Product>("SELECT id, name FROM products");
```

## Partial mapping does not use the source-generated mapper

This is the constraint to plan around, and it is not visible from the signatures.

`DrDispatcher.Resolve` picks a mapper in this order:

1. `options.Mapper`, when the caller supplied one.
2. The source-generated `IMapped<T>` mapper - **only when the mode is `Strict`**.
3. A special-type mapper, when `SpecialTypeMappers.Register()` has been called
   (see [`special-types.md`](special-types.md)).
4. The reflection extension, when `Jaunty.Extensions.Reflection` is loaded.
5. Otherwise it throws *No mapper found for type 'T'*.

Step 2 is skipped for projection mode by design: a generated mapper is built for the entity's full
shape and a partial result set may not carry every column it reads. The source generator emits no
projection mapper, so on a partial query the dispatcher always falls through.

**The practical consequence:** in a source-generation-only build - the NativeAOT and
zero-reflection configuration - `QueryPartial<T>` throws unless you either

- reference `Jaunty.Extensions.Reflection` and call `UseReflectionMapping()`, or
- pass your own mapper: `CommandOptions<T>.WithMapper(...)`.

If neither is acceptable, the alternative is to keep strict mapping and declare a DTO whose
properties are the columns the query actually selects. That is what
[`api-summary.md`](api-summary.md) means by "projection type": a `ProductSummary` with exactly
`Id` and `Name` maps under plain `Query<T>` with no reflection at all, and the compiler tells you
when the query and the type drift apart. Prefer it for anything on a hot path.

```csharp
// Zero-reflection projection: strict mapping over a DTO shaped like the SELECT.
public class ProductSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

var summaries = connection.Query<ProductSummary>("SELECT id, name FROM products");
```

---

## Overloads

Every `QueryPartial*<T>` method has four synchronous and four asynchronous arities. The parameter
list is uniform:

| # | Arity |
|---|---|
| 1 | `(string sql)` |
| 2 | `(string sql, object parameters)` |
| 3 | `(string sql, CommandOptions<T> options)` |
| 4 | `(string sql, object parameters, CommandOptions<T> options)` |

The async forms append `CancellationToken cancellationToken = default` and return `ValueTask<...>`.

```csharp
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql) where T : new();
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters) where T : new();
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new();
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new();

public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new();
// ... and the three remaining arities, each ending in a CancellationToken.
```

`QueryPartialFirst<T>`, `QueryPartialFirstOrDefault<T>`, `QueryPartialSingle<T>` and
`QueryPartialSingleOrDefault<T>` follow the identical pattern, differing only in return type and in
what they do with a row count that is not one.

| Method | 0 rows | 1 row | 2+ rows |
|---|---|---|---|
| `QueryPartialFirst<T>` | throws `InvalidOperationException` | the row | the first row |
| `QueryPartialFirstOrDefault<T>` | `default` | the row | the first row |
| `QueryPartialSingle<T>` | throws `InvalidOperationException` | the row | throws `InvalidOperationException` |
| `QueryPartialSingleOrDefault<T>` | `default` | the row | throws `InvalidOperationException` |

---

## QueryPartialList - untyped rows

`QueryPartialList` needs no entity type and no mapper at all. It returns one dictionary per row,
keyed by column name.

**Signatures:**
```csharp
public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql);
public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, object parameters);
public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, CommandOptions options);
public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, object parameters, CommandOptions options);
```

plus the four `QueryPartialListAsync` forms returning `ValueTask<List<IDictionary<string, object?>>>`.

Note the **non-generic** `CommandOptions` on these overloads: there is no `T`, so there is no
`Mapper` and no `ExpectedRowCount`. See [`command-options.md`](command-options.md).

```csharp
var rows = connection.QueryPartialList("SELECT id, name FROM products");
foreach (IDictionary<string, object?> row in rows)
    Console.WriteLine($"{row["id"]}: {row["name"]}");
```

Duplicate column names are disambiguated rather than collapsed - a join returning two `Id` columns
yields two distinct keys rather than keeping only the last. The same rule applies to
`Query<Dictionary<string, object>>` and `Query<dynamic>`; see
[`special-types.md`](special-types.md#duplicate-column-names).

---

## Notes

- Partial mapping changes which properties get set. It does not relax type conversion: a column
  whose value cannot be converted to the property type still fails.
- A `NULL` in a column mapped to a non-nullable value type throws in both modes.
- Connection ownership is the same as for every other read method: Jaunty opens a closed
  connection and closes it again, leaves an already-open one open, and never disposes a connection
  it did not open. See [`query-methods.md`](query-methods.md#connection-ownership).

## See Also

- [`query-methods.md`](query-methods.md) - the strict family and the `QueryPartial<T>` overloads
- [`single-result-methods.md`](single-result-methods.md) - First/Single semantics in full
- [`streaming-methods.md`](streaming-methods.md) - `QueryPartialStream` and `QueryPartialUnbuffered`
- [`special-types.md`](special-types.md) - `Dictionary`, `dynamic`, `KeyValuePair`, `ValueTuple`
- [`command-options.md`](command-options.md) - `CommandOptions<T>` and its non-generic twin
