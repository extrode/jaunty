# Special Types

## Overview

Four result shapes have no properties for Jaunty to map, so they are handled by a dedicated
resolver rather than by the source generator or the ordinary reflection mapper:

| `T` | Shape | Mapping |
|---|---|---|
| `Dictionary<string, object>` | one entry per column | by column name, case-**insensitive** |
| `Dictionary<string, TValue>` | one entry per column, values converted to `TValue` | by column name, case-**insensitive** |
| `dynamic` (`object`) | an `ExpandoObject` | by column name, case-**sensitive** |
| `KeyValuePair<TKey, TValue>` | first two columns | positional |
| `ValueTuple<...>` | first *n* columns | positional |

They work with any read method that takes a `T`: `Query<T>`, `QueryFirst<T>`,
`QuerySingleOrDefault<T>`, the streaming forms, and the async twins of all of them.

For untyped rows without any of this machinery, see
[`QueryPartialList`](query-partial-methods.md#querypartiallist---untyped-rows), which returns
`List<IDictionary<string, object?>>` with no registration and no reflection extension.

---

## Registration is required

Special-type mapping lives in `Jaunty.Extensions.Reflection`, not in the core package. Nothing is
wired up until you register it:

```csharp
using Jaunty.Extensions.Reflection;

JauntyReflectionExtensions.UseReflectionMapping();   // registers special types as well
```

`UseReflectionMapping()` calls `SpecialTypeMappers.Register()` for you, because
`DrDispatcher` has to resolve these types through the special-type hook before it ever reaches the
property-based reflection mapper - and these types have no mappable properties, so reaching that
mapper would fail. If you want the special-type hook and nothing else:

```csharp
SpecialTypeMappers.Register();
```

`Register()` is idempotent and non-clobbering: it assigns `JauntyConfig.SpecialTypeMapperResolver`
only when that property is still null, so a custom resolver you installed yourself survives a
later call.

Without registration, `Query<Dictionary<string, object>>` throws
*No mapper found for type 'Dictionary\`2'*.

### NativeAOT

`SpecialTypeMappers` is **not** AOT-safe. It builds dictionaries and tuples through
`Activator.CreateInstance` and `MakeGenericType`. In a trimmed or AOT-published application, use
strongly-typed entities, or a DTO plus strict mapping (see
[`query-partial-methods.md`](query-partial-methods.md#partial-mapping-does-not-use-the-source-generated-mapper)),
or exclude the assembly from trimming.

---

## Dictionary

```csharp
var rows = connection.Query<Dictionary<string, object>>(
    "SELECT ProductID, ProductName FROM Products");

foreach (var row in rows)
    Console.WriteLine($"{row["productid"]}: {row["productname"]}");
```

The dictionary is built with `StringComparer.OrdinalIgnoreCase`, so `row["productid"]` and
`row["ProductID"]` both resolve.

`Dictionary<string, TValue>` converts each value to `TValue`; a `NULL` becomes the default for
`TValue`. A key type other than `string` throws `NotSupportedException`.

## dynamic

```csharp
var rows = connection.Query<dynamic>("SELECT ProductID, ProductName FROM Products");

foreach (var row in rows)
    Console.WriteLine($"{row.ProductID}: {row.ProductName}");
```

Rows are `ExpandoObject`. Member access is case-sensitive, because C# is.

**This does not match `Dictionary`.** An `ExpandoObject`'s backing
`IDictionary<string, object?>` is ordinal case-sensitive and takes no comparer, so:

```csharp
var dict = connection.QueryFirst<Dictionary<string, object>>(sql);
dict["customerid"];                                         // resolves against CustomerID

var dyn  = connection.QueryFirst<dynamic>(sql);
((IDictionary<string, object?>)dyn)["customerid"];           // KeyNotFoundException
```

The asymmetry is not fixable in the mapper: aligning the two would mean not returning an
`ExpandoObject` from `Query<dynamic>`, which is the documented contract. Recorded as AUD-R35-226.
Use the exact column casing when indexing a `dynamic` row as a dictionary.

## KeyValuePair

```csharp
var pairs = connection.Query<KeyValuePair<int, string>>(
    "SELECT CategoryID, CategoryName FROM Categories");
```

Positional: column 0 is the key, column 1 is the value. Extra columns are ignored. Fewer than two
columns throws `InvalidOperationException` naming the actual field count. A `NULL` becomes the
default for that side.

## ValueTuple

```csharp
var rows = connection.Query<(int Id, string Name, decimal Price)>(
    "SELECT ProductID, ProductName, UnitPrice FROM Products");

foreach (var (id, name, price) in rows)
    Console.WriteLine($"{id} {name} {price:C}");
```

Positional, left to right. The query must return at least as many columns as the tuple has
elements; fewer throws `InvalidOperationException` listing the expected types.

**Eight or more elements throws `NotSupportedException`.** C# nests beyond seven elements into a
`Rest` field, and a positional mapper would bind that nested tuple as a single column - silently
truncating. Failing fast is deliberate. Use a DTO instead.

Tuple element *names* are compiler sugar and are not used for matching; only position matters.

---

## Duplicate column names

A join that returns two columns called `Id` would collapse into one dictionary entry. Jaunty
disambiguates instead, in all three untyped paths - `Dictionary`, `dynamic`, and
`QueryPartialList` - so both columns survive as distinct keys.

The rule is shared rather than copied between the sites (`DuplicateColumnNames.Disambiguate`),
because the two were found drifted apart once already: `QueryPartialList` and `Dictionary` were
aligned by AUD-R26, and `dynamic` was carried over later by AUD-R34-024, having silently kept only
the last of two `Id` columns until then.

Prefer aliasing in SQL (`SELECT p.Id AS ProductId, c.Id AS CategoryId`) over relying on the
disambiguated names.

---

## See Also

- [`query-methods.md`](query-methods.md) - the read methods these types plug into
- [`query-partial-methods.md`](query-partial-methods.md) - `QueryPartialList` and projection mapping
- [`configuration.md`](configuration.md) - `JauntyConfig.SpecialTypeMapperResolver`
- [`../04-extensions/README.md`](../04-extensions/README.md) - the reflection extension package
