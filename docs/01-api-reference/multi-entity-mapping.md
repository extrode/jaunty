# Multi-Entity Mapping

## Overview

Jaunty can map a single row from a joined query into two or more entity instances at once, without requiring a `SplitOn` column name like Dapper does. Instead, Jaunty uses **left-to-right ordinal claiming**: each type in the generic parameter list claims the result columns that match its own properties, in order, and later types can only claim columns not already claimed by an earlier type.

Multi-entity mapping is supported for arities `T1..T2` through `T1..T7` via `Query`, `QueryFirst`, `QueryFirstOrDefault`, `QuerySingle`, `QuerySingleOrDefault`, and `QueryStream`, mirroring the single-entity method set.

## How Ordinal Claiming Works

For `Query<T1, T2, ..., TN>`, Jaunty resolves setters for each type in order:

1. `T1` claims every result-set column ordinal that matches one of its mapped properties.
2. `T2` claims from the **remaining** (unclaimed) ordinals only — any column already claimed by `T1` is invisible to `T2`, even if `T2` also has a same-named property.
3. This repeats left-to-right through `T3, T4, ..., TN`.

This means column order in the `SELECT` list does not matter, but property name overlap across types does: whichever type appears first in the generic parameter list "wins" a shared column name.

**Example:**
```csharp
public class Author { public int Id { get; set; } public string Name { get; set; } = ""; }
public class Book   { public int Id { get; set; } public int AuthorId { get; set; } public string Name { get; set; } = ""; }

const string sql = @"
    SELECT a.id, a.name, b.id, b.author_id, b.name
    FROM authors a JOIN books b ON b.author_id = a.id";

var results = connection.Query<Author, Book>(sql);
// Author claims the first "id" and "name" ordinals it finds a match for;
// Book's "id" and "name" columns are claimed next since Author already took the first pair.
```

## Resolving Ambiguous Column Names

When two types share a property name (e.g. both `Author` and `Book` have `Name`) and the query aliases columns so they are distinguishable, use the existing `[Column("name")]` attribute (see [Attributes](attributes.md)) to pin each property to its exact column, removing any ambiguity from claiming order:

```csharp
public class Author
{
    public int Id { get; set; }
    [Column("author_name")]
    public string Name { get; set; } = "";
}

public class Book
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    [Column("book_name")]
    public string Name { get; set; } = "";
}
```

```sql
SELECT a.id, a.name AS author_name, b.id, b.author_id, b.name AS book_name
FROM authors a JOIN books b ON b.author_id = a.id
```

No separate "split on" mechanism is needed — `[Column]` is sufficient because ordinal claiming resolves by column name/ordinal match per type, not by position boundaries.

## Query Methods

### Query&lt;T1, T2&gt; (and T3..T7)

**Signature (arity 2, representative of all arities):**
```csharp
public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql)
    where T1 : new() where T2 : new()

public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object parameters)
    where T1 : new() where T2 : new()

public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options)
    where T1 : new() where T2 : new()

public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options)
    where T1 : new() where T2 : new()
```

Each arity from 3 through 7 (`Query<T1,T2,T3>` ... `Query<T1,T2,T3,T4,T5,T6,T7>`) exposes the same overload shape, returning a `List<(T1, T2, ..., TN)>` of value tuples.

**Related methods** (same overload shape, same arities 2-7):
- `QueryFirst<T1,...,TN>` — returns the first tuple, throws if the result set is empty.
- `QueryFirstOrDefault<T1,...,TN>` — returns the first tuple, or `default` if the result set is empty.
- `QuerySingle<T1,...,TN>` — returns the only tuple, throws if zero or more than one row.
- `QuerySingleOrDefault<T1,...,TN>` — returns the only tuple, or `default` if empty; throws if more than one row.
- `QueryStream<T1,...,TN>` — returns `IEnumerable<(T1,...,TN)>`, lazily streaming one row at a time (see [Streaming Methods](streaming-methods.md) for the single-entity equivalent).

## MultiEntityCommandOptions

`MultiEntityCommandOptions<T1,...,TN>` (arities 2-7) carries the same execution-level settings as `CommandOptions<T>` for multi-entity queries: `Transaction`, `CommandTimeout`, and `CommandType`. It implicitly converts to the appropriate `CommandOptions`/`CommandOptions<(T1,...,TN)>` overload.

```csharp
public readonly struct MultiEntityCommandOptions<T1, T2, T3>
    where T1 : new() where T2 : new() where T3 : new()
{
    public MultiEntityCommandOptions(
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType commandType = CommandType.Text);
}
```

> **No per-position custom mapper.** Earlier pre-release builds carried `mapper1..mapperN` parameters on this type intended to let an individual position supply its own `Func<IDataReader, TN>` and opt out of ordinal claiming (for keyless or projection-only trailing types). That capability was never actually wired up end-to-end — the delegate was accepted but silently discarded before it reached the mapping engine — and has been removed rather than shipped as a working 1.0 API. There is currently no way to supply a custom mapper for an individual type position in a multi-entity query; every type in the tuple must have properties that ordinal claiming can bind directly. This is tracked as a possible future enhancement, not a currently-supported feature.

## Mapping Modes

Internally, multi-entity mapping always resolves individual entity property setters in `MappingMode.Projection` (each type maps only the columns it can claim, rather than requiring every property to have a matching column). The public `Query<T1,...,TN>` surface always operates this way — there is no separate strict/partial toggle for multi-entity queries, since ordinal claiming already implies a projection over the full result set.

## Best Practices

### 1. Order generic type parameters to match column order

Since ordinal claiming is left-to-right, list your generic type parameters in the same left-to-right order as the columns appear in your `SELECT` list to avoid surprises when two types share property names.

### 2. Use `[Column]` for any shared property names

If two mapped types share a property name (e.g. both have `Name` or `CreatedAt`), alias the columns distinctly in SQL and use `[Column("...")]` on at least one of the properties so claiming is unambiguous.

### 3. Give every type at least one property ordinal claiming can bind

Multi-entity mapping has no mechanism today for keyless or computed trailing types — each type in the tuple needs at least one property that matches an unclaimed result-set column, or it maps back as an empty (default-constructed) instance.

## Important Notes

- **No SplitOn**: Jaunty does not implement Dapper's `splitOn` parameter. Ambiguous column names are resolved with `[Column]`.
- **Left-to-right claiming**: `T1` always claims before `T2`, `T2` before `T3`, and so on — order of generic type parameters is significant.
- **No per-position custom mapper**: there is no way to supply a custom mapping delegate for an individual type position; see the note under [MultiEntityCommandOptions](#multientitycommandoptions) above.
- **Arity range**: `T1` through `T7` are supported (2 to 7 total types per row).
- **Performance**: `Query<T1,...,TN>` uses the same `DbConnection`/`DbDataReader` fast path as single-entity queries when the connection is a concrete `DbConnection`, and `QueryStream<T1,...,TN>` streams lazily rather than materializing a list.
