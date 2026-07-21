# Query Methods

## Overview

Query methods execute SQL commands and return lists of strongly-typed entities. These methods support both strict and partial mapping modes.

## Connection Ownership

- Jaunty opens a closed connection when needed and closes it after execution.
- If you pass an already-open connection, Jaunty leaves it open.
- The caller still owns the connection object lifetime and should dispose it.
- In high-concurrency apps, not disposing connections can exhaust the provider pool (for example PostgreSQL `too many clients`).

**Recommended pattern:**
```csharp
using var connection = new Npgsql.NpgsqlConnection(connectionString);
var rows = await connection.QueryAsync<MyRow>(
    "SELECT id, name FROM my_table WHERE id = @Id",
    new { Id = 1 });
```

## Methods

### Query&lt;T&gt;(string sql)

Executes a query and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> Query<T>(this IDbConnection connection, string sql) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute

**Returns:**
- `List<T>`: A list of entities of type T

**Example:**
```csharp
var products = connection.Query<Product>("SELECT * FROM products");
```

### Query&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query

**Returns:**
- `List<T>`: A list of entities of type T

**Example:**
```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId", 
    new { CategoryId = 1 });
```

### Query&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options)

Executes a query with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> Query<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `List<T>`: A list of entities of type T

**Example:**
```csharp
using var transaction = connection.BeginTransaction();
var products = connection.Query<Product>(
    "SELECT * FROM products", 
    CommandOptions<Product>.WithTransaction(transaction));
```

### Query&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options)

Executes a parameterized query with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `List<T>`: A list of entities of type T

**Example:**
```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId", 
    new { CategoryId = 1 },
    CommandOptions<Product>.WithTimeout(30));
```

## Async Variants

### QueryAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QueryAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QueryAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

**Example:**
```csharp
var products = await connection.QueryAsync<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    cancellationToken);
```

## Partial Mapping Variants

### QueryPartial&lt;T&gt;(string sql)

Executes a query and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql) where T : new()
```

### QueryPartial&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters) where T : new()
```

### QueryPartial&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options)

Executes a query with command options and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
```

### QueryPartial&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options)

Executes a parameterized query with command options and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
```

**Example:**
```csharp
// Only product_id and product_name are selected; partial mapping tolerates the missing columns
var products = connection.QueryPartial<Product>("SELECT product_id, product_name FROM products");
```

## Async Partial Mapping Variants

### QueryPartialAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

**Example:**
```csharp
var products = await connection.QueryPartialAsync<Product>(
    "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 },
    cancellationToken);
```
