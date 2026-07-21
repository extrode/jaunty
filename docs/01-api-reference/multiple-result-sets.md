# Multiple Result Sets

## Overview

Multiple result set methods allow executing a single command that returns multiple result sets and reading from each result set sequentially. This functionality is accessed through the `GridReader` class returned by `QueryMultiple` methods.

## QueryMultiple Methods

### QueryMultiple(string sql)

Executes a command that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static GridReader QueryMultiple(this IDbConnection connection, string sql)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL command containing multiple result sets (e.g., multiple SELECT statements separated by semicolons)

**Returns:**
- `GridReader`: A reader for accessing multiple result sets

**Example:**
```csharp
using var gridReader = connection.QueryMultiple("SELECT * FROM categories; SELECT * FROM products");
var categories = gridReader.Read<Category>().ToList();
var products = gridReader.Read<Product>().ToList();
```

### QueryMultiple(string sql, object parameters)

Executes a parameterized command that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static GridReader QueryMultiple(this IDbConnection connection, string sql, object parameters)
```

### QueryMultiple(string sql, CommandOptions options)

Executes a command with options that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static GridReader QueryMultiple(this IDbConnection connection, string sql, CommandOptions options)
```

### QueryMultiple(string sql, object parameters, CommandOptions options)

Executes a parameterized command with options that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static GridReader QueryMultiple(this IDbConnection connection, string sql, object parameters, CommandOptions options)
```

## Async QueryMultiple Methods

### QueryMultipleAsync(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a command that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
```

### QueryMultipleAsync(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized command that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
```

### QueryMultipleAsync(string sql, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously executes a command with options that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
```

### QueryMultipleAsync(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized command with options that returns multiple result sets and returns a `GridReader` for reading from each result set.

**Signature:**
```csharp
public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
```

**Example:**
```csharp
await using var grid = await connection.QueryMultipleAsync(
    "SELECT * FROM categories; SELECT * FROM products");
var categories = await grid.ReadAsync<Category>();
var products = await grid.ReadAsync<Product>();
```

## GridReader Methods

The `GridReader` class provides methods to read from each result set in sequence.

### Read&lt;T&gt;()

Reads the current result set as a list of entities using strict mapping mode.

**Signature:**
```csharp
public List<T> Read<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `List<T>`: A list of entities of type T from the current result set

### ReadPartial&lt;T&gt;()

Reads the current result set as a list of entities using partial mapping mode.

**Signature:**
```csharp
public List<T> ReadPartial<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `List<T>`: A list of entities of type T from the current result set

### ReadFirst&lt;T&gt;()

Reads the first entity from the current result set using strict mapping mode. Throws an exception if the result set is empty.

**Signature:**
```csharp
public T ReadFirst<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T`: The first entity of type T from the current result set

### ReadFirstOrDefault&lt;T&gt;()

Reads the first entity from the current result set or the default value if the result set is empty, using strict mapping mode.

**Signature:**
```csharp
public T? ReadFirstOrDefault<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T?`: The first entity of type T from the current result set, or null if the result set is empty

### ReadPartialFirst&lt;T&gt;()

Reads the first entity from the current result set using partial mapping mode. Throws an exception if the result set is empty.

**Signature:**
```csharp
public T ReadPartialFirst<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T`: The first entity of type T from the current result set

### ReadPartialFirstOrDefault&lt;T&gt;()

Reads the first entity from the current result set or the default value if the result set is empty, using partial mapping mode.

**Signature:**
```csharp
public T? ReadPartialFirstOrDefault<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T?`: The first entity of type T from the current result set, or null if the result set is empty

### ReadSingle&lt;T&gt;()

Reads the single entity from the current result set using strict mapping mode. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
public T ReadSingle<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T`: The single entity of type T from the current result set

### ReadSingleOrDefault&lt;T&gt;()

Reads the single entity from the current result set or the default value if the result set is empty, using strict mapping mode. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
public T? ReadSingleOrDefault<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T?`: The single entity of type T from the current result set, or null if the result set is empty

### ReadPartialSingle&lt;T&gt;()

Reads the single entity from the current result set using partial mapping mode. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
public T ReadPartialSingle<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T`: The single entity of type T from the current result set

### ReadPartialSingleOrDefault&lt;T&gt;()

Reads the single entity from the current result set or the default value if the result set is empty, using partial mapping mode. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
public T? ReadPartialSingleOrDefault<T>(CommandOptions<T> options = default) where T : new()
```

**Returns:**
- `T?`: The single entity of type T from the current result set, or null if the result set is empty

### ReadScalar&lt;T&gt;()

Reads a scalar value (first column of first row) from the current result set.

**Signature:**
```csharp
public T? ReadScalar<T>(CommandOptions options = default)
```

**Returns:**
- `T?`: The scalar value of type T from the current result set

**Example:**
```csharp
using var grid = connection.QueryMultiple("SELECT * FROM products; SELECT COUNT(*) FROM products");
var products = grid.Read<Product>().ToList();
var total = grid.ReadScalar<int>();
```

## Async GridReader Methods

### ReadAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the current result set as a list of entities using strict mapping mode.

**Signature:**
```csharp
public Task<List<T>> ReadAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadPartialAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the current result set as a list of entities using partial mapping mode.

**Signature:**
```csharp
public Task<List<T>> ReadPartialAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadFirstAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the first entity from the current result set using strict mapping mode.

**Signature:**
```csharp
public Task<T> ReadFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadFirstOrDefaultAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the first entity from the current result set or the default value if the result set is empty, using strict mapping mode.

**Signature:**
```csharp
public Task<T?> ReadFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadPartialFirstAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the first entity from the current result set using partial mapping mode.

**Signature:**
```csharp
public Task<T> ReadPartialFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadPartialFirstOrDefaultAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the first entity from the current result set or the default value if the result set is empty, using partial mapping mode.

**Signature:**
```csharp
public Task<T?> ReadPartialFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadSingleAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the single entity from the current result set using strict mapping mode.

**Signature:**
```csharp
public Task<T> ReadSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadSingleOrDefaultAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the single entity from the current result set or the default value if the result set is empty, using strict mapping mode.

**Signature:**
```csharp
public Task<T?> ReadSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadPartialSingleAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the single entity from the current result set using partial mapping mode.

**Signature:**
```csharp
public Task<T> ReadPartialSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadPartialSingleOrDefaultAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously reads the single entity from the current result set or the default value if the result set is empty, using partial mapping mode.

**Signature:**
```csharp
public Task<T?> ReadPartialSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ReadScalarAsync&lt;T&gt;(CommandOptions options = default, CancellationToken cancellationToken = default)

Asynchronously reads a scalar value (first column of first row) from the current result set.

**Signature:**
```csharp
public Task<T?> ReadScalarAsync<T>(CommandOptions options = default, CancellationToken cancellationToken = default)
```

## Streaming Methods in GridReader

### ReadStream&lt;T&gt;(CommandOptions&lt;T&gt; options = default)

Returns an enumerable stream of entities from the current result set using strict mapping mode.

**Signature:**
```csharp
public IEnumerable<T> ReadStream<T>(CommandOptions<T> options = default) where T : new()
```

### ReadPartialStream&lt;T&gt;(CommandOptions&lt;T&gt; options = default)

Returns an enumerable stream of entities from the current result set using partial mapping mode.

**Signature:**
```csharp
public IEnumerable<T> ReadPartialStream<T>(CommandOptions<T> options = default) where T : new()
```

### ReadStreamAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously returns an async enumerable stream of entities from the current result set using strict mapping mode.

**Signature:**
```csharp
public IAsyncEnumerable<T> ReadStreamAsync<T>(CommandOptions<T> options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
```

### ReadPartialStreamAsync&lt;T&gt;(CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously returns an async enumerable stream of entities from the current result set using partial mapping mode.

**Signature:**
```csharp
public IAsyncEnumerable<T> ReadPartialStreamAsync<T>(CommandOptions<T> options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
```

**Example:**
```csharp
using var grid = connection.QueryMultiple("SELECT * FROM categories; SELECT * FROM products");
var categories = grid.Read<Category>().ToList();
foreach (var p in grid.ReadStream<Product>())
{
    Console.WriteLine(p.ProductName);
}
```

## Important Notes

- **Sequential Access**: Result sets must be read in sequence; you cannot go back to a previous result set
- **Resource Management**: The `GridReader` implements `IDisposable` and should be disposed after use
- **Connection State**: The underlying connection remains open while the `GridReader` is active
- **Database Support**: Multiple result sets are supported by SQL Server, PostgreSQL, and MySQL but have limitations in SQLite
- **Mapping Mode**: Each read method has both strict and partial mapping variants
- **Async Behavior**: Async methods return completed tasks if the GridReader has already been consumed
- **Cancellation**: Async methods support cancellation tokens for cooperative cancellation

## Pool Safety

- Dispose both the `DbConnection` and the `GridReader`.
- If either is leaked in high-concurrency code, connection pools can exhaust and fail with errors like PostgreSQL `too many clients`.

**Recommended async pattern:**
```csharp
using var connection = fixture.GetDbConnection(dialect);
using var grid = await connection.QueryMultipleAsync(
    "SELECT ...; SELECT ...;");

var first = await grid.ReadAsync<MyRow1>();
var second = await grid.ReadAsync<MyRow2>();
```
