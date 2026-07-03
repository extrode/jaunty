# Get, GetAll, and Execute Operations

## Overview

This page documents key-based single-entity retrieval (`Get`), whole-table retrieval (`GetAll`), and raw non-query SQL execution (`Execute`, `ExecuteBatch`).

## Get Operations

### Get&lt;T&gt;(object id)

Retrieves an entity by its primary key value. Requires `T` to have a single `[Key]` property (or one inferred by convention).

**Signature:**
```csharp
public static T? Get<T>(this IDbConnection connection, object id) where T : new()
```

**Returns:** The entity if found; otherwise `null`. Throws `InvalidOperationException` if `T` has no single primary key.

**Example:**
```csharp
var product = connection.Get<Product>(42);
```

### Get&lt;T&gt;(object id, CommandOptions&lt;T&gt; options)

Same as above, with `CommandOptions<T>` for transaction, timeout, or a custom mapper.

```csharp
public static T? Get<T>(this IDbConnection connection, object id, CommandOptions<T> options) where T : new()
```

### Get&lt;T, TId&gt;(TId id)

Typed-key overload for entities implementing `IEntity<TId>`, avoiding the `object` boxing of the primary key value.

```csharp
public static T? Get<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
public static T? Get<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
```

### GetRequired&lt;T&gt;(object id) / GetRequired&lt;T, TId&gt;(TId id)

Same as `Get`, but throws `InvalidOperationException` instead of returning `null` when no matching entity exists. Available with and without `CommandOptions<T>`, and with and without the typed-key `TId` overload.

```csharp
public static T GetRequired<T>(this IDbConnection connection, object id) where T : new()
public static T GetRequired<T>(this IDbConnection connection, object id, CommandOptions<T> options) where T : new()
public static T GetRequired<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
public static T GetRequired<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
```

### GetAsync / GetRequiredAsync

Async equivalents of every `Get`/`GetRequired` overload above. Require a concrete `DbConnection` (or subclass) — an `ArgumentException` is thrown otherwise.

```csharp
public static ValueTask<T?> GetAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : new()
public static ValueTask<T?> GetAsync<T>(this IDbConnection connection, object id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
public static ValueTask<T?> GetAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
public static ValueTask<T?> GetAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
```

**Example:**
```csharp
var product = await connection.GetAsync<Product>(42);
```

## GetAll Operations

### GetAll&lt;T&gt;()

Retrieves every row from the table mapped to `T` as a materialized `List<T>`.

```csharp
public static List<T> GetAll<T>(this IDbConnection connection) where T : new()
public static List<T> GetAll<T>(this IDbConnection connection, CommandOptions<T> options) where T : new()
```

**Example:**
```csharp
var allProducts = connection.GetAll<Product>();
```

### GetAllAsync&lt;T&gt;()

Async equivalent; requires a concrete `DbConnection`.

```csharp
public static ValueTask<List<T>> GetAllAsync<T>(this IDbConnection connection, CancellationToken cancellationToken = default) where T : new()
public static ValueTask<List<T>> GetAllAsync<T>(this IDbConnection connection, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### GetAllStream&lt;T&gt;() / GetAllStreamAsync&lt;T&gt;()

Lazily streams rows one at a time via `IEnumerable<T>` (or `IAsyncEnumerable<T>` on target frameworks with async-enumerable support) instead of materializing a full list. Prefer this for large tables. See [Streaming Methods](streaming-methods.md) for the equivalent on parameterized queries.

```csharp
public static IEnumerable<T> GetAllStream<T>(this IDbConnection connection) where T : new()
public static IEnumerable<T> GetAllStream<T>(this IDbConnection connection, CommandOptions<T> options) where T : new()

public static IAsyncEnumerable<T> GetAllStreamAsync<T>(this IDbConnection connection, CancellationToken cancellationToken = default) where T : new()
public static IAsyncEnumerable<T> GetAllStreamAsync<T>(this IDbConnection connection, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

**Note:** On .NET Framework (`netstandard2.0`, no async-enumerable support), the async streaming overloads instead return `ValueTask<IEnumerable<T>>` with fully buffered results.

**Example:**
```csharp
foreach (var product in connection.GetAllStream<Product>())
{
    Process(product);
}
```

## Execute Operations

### Execute(string sql)

Executes a non-query SQL command (INSERT/UPDATE/DELETE/DDL) and returns the number of rows affected.

```csharp
public static int Execute(this IDbConnection connection, string sql)
public static int Execute(this IDbConnection connection, string sql, object parameters)
public static int Execute(this IDbConnection connection, string sql, CommandOptions options)
public static int Execute(this IDbConnection connection, string sql, object parameters, CommandOptions options)
```

**Example:**
```csharp
int rowsAffected = connection.Execute(
    "UPDATE products SET price = @Price WHERE category_id = @CategoryId",
    new { Price = 9.99m, CategoryId = 1 });
```

### ExecuteBatch(string sql, IEnumerable&lt;object&gt; parameterSets)

Executes the same SQL command once per item in `parameterSets`, on a single open connection/command, and returns the cumulative rows affected across all executions.

```csharp
public static int ExecuteBatch(this IDbConnection connection, string sql, IEnumerable<object> parameterSets)
public static int ExecuteBatch(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options)
```

**Example:**
```csharp
var updates = new[]
{
    new { Id = 1, Price = 9.99m },
    new { Id = 2, Price = 14.99m },
    new { Id = 3, Price = 4.99m },
};

int totalRowsAffected = connection.ExecuteBatch(
    "UPDATE products SET price = @Price WHERE id = @Id", updates);
```

**Notes:**
- Reuses a single `IDbCommand` across all parameter sets, re-binding parameters each iteration — more efficient than calling `Execute` in a loop.
- `null` entries in `parameterSets` are skipped.
- Wrap in a transaction (via `CommandOptions.WithTransaction(...)`) if the batch must be all-or-nothing.

### ExecuteBatchAsync

Async equivalent of `ExecuteBatch`. Requires a concrete `DbConnection` (or subclass) — throws `InvalidOperationException` otherwise.

```csharp
public static ValueTask<int> ExecuteBatchAsync(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CancellationToken cancellationToken = default)
public static ValueTask<int> ExecuteBatchAsync(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options, CancellationToken cancellationToken = default)
```

## Important Notes

- **Single vs. composite keys**: `Get<T>(object id)` requires exactly one `[Key]` property; entities with composite keys should use a normal `Query`/`QueryFirstOrDefault` with an explicit `WHERE` clause instead.
- **Async requires DbConnection**: All async `Get*`/`GetAll*`/`ExecuteBatchAsync` overloads require a concrete `DbConnection` subclass, consistent with the rest of Jaunty's async surface.
- **Streaming keeps the connection open**: `GetAllStream`/`GetAllStreamAsync` only fully close/read the connection once enumeration completes — dispose or fully enumerate promptly.
