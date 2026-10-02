# Query Methods

## Overview

Query methods execute SQL commands and return lists of strongly-typed entities. These methods support both strict and partial mapping modes.

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
public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QueryAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QueryAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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

## Async Partial Mapping Variants

### QueryPartialAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the results as a list of entities using partial mapping mode.

**Signature:**
```csharp
public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```