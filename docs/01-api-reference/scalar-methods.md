# Scalar Methods

## Overview

Scalar methods execute SQL commands and return single scalar values (typically the first column of the first row). These methods are useful for aggregate functions like COUNT, SUM, AVG, etc.

## Methods

### QueryScalar&lt;T&gt;(string sql)

Executes a query and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static T QueryScalar<T>(this IDbConnection connection, string sql)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute

**Returns:**
- `T`: The scalar value of type T

**Example:**
```csharp
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");
```

### QueryScalar&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query

**Returns:**
- `T`: The scalar value of type T

**Example:**
```csharp
var price = connection.QueryScalar<decimal>(
    "SELECT MAX(price) FROM products WHERE category_id = @CategoryId", 
    new { CategoryId = 1 });
```

### QueryScalar&lt;T&gt;(string sql, CommandOptions options)

Executes a query with command options and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static T QueryScalar<T>(this IDbConnection connection, string sql, CommandOptions options)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout)

**Returns:**
- `T`: The scalar value of type T

**Example:**
```csharp
var count = connection.QueryScalar<long>(
    "SELECT COUNT(*) FROM products",
    CommandOptions.WithTimeout(30));
```

### QueryScalar&lt;T&gt;(string sql, object parameters, CommandOptions options)

Executes a parameterized query with command options and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query
- `options`: Command options (transaction, timeout)

**Returns:**
- `T`: The scalar value of type T

**Example:**
```csharp
using var transaction = connection.BeginTransaction();
var count = connection.QueryScalar<long>(
    "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId", 
    new { CategoryId = 1 },
    CommandOptions<long>.WithTransaction(transaction));
```

## Provider-specific behavior: COUNT(*) return type

`COUNT(*)` return type can differ by provider:

- SQL Server commonly returns `Int32` (`int`)
- PostgreSQL, MariaDB/MySQL, and SQLite commonly return `Int64` (`long`)

For cross-dialect code, prefer `QueryScalar<long>` / `ExecuteScalar<long>` for aggregate counts.

```csharp
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");
```

## Async Variants

### QueryScalarAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<T>`: A task that resolves to the scalar value of type T

**Example:**
```csharp
var count = await connection.QueryScalarAsync<long>("SELECT COUNT(*) FROM products");
```

### QueryScalarAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<T>`: A task that resolves to the scalar value of type T

### QueryScalarAsync&lt;T&gt;(string sql, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout)
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<T>`: A task that resolves to the scalar value of type T

### QueryScalarAsync&lt;T&gt;(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the first column of the first row as a scalar value.

**Signature:**
```csharp
public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query
- `options`: Command options (transaction, timeout)
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<T>`: A task that resolves to the scalar value of type T

**Example:**
```csharp
var count = await connection.QueryScalarAsync<long>(
    "SELECT COUNT(*) FROM products WHERE category_id > @MinCategory", 
    new { MinCategory = 5 },
    CommandOptions.WithTimeout(30),
    cancellationToken);
```
