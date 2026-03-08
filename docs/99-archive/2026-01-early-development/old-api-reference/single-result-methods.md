# Single Result Methods

## Overview

Single result methods execute SQL commands and return a single entity or scalar value. These methods provide different behaviors for handling empty result sets and multiple results.

## Methods

### QueryFirst&lt;T&gt;(string sql)

Executes a query and returns the first entity from the result set. Throws an exception if the result set is empty.

**Signature:**
```csharp
public static T QueryFirst<T>(this IDbConnection connection, string sql) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute

**Returns:**
- `T`: The first entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty

**Example:**
```csharp
var product = connection.QueryFirst<Product>("SELECT * FROM products WHERE id = 1");
```

### QueryFirst&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the first entity from the result set. Throws an exception if the result set is empty.

**Signature:**
```csharp
public static T QueryFirst<T>(this IDbConnection connection, string sql, object parameters) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query

**Returns:**
- `T`: The first entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty

### QueryFirst&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options)

Executes a query with command options and returns the first entity from the result set. Throws an exception if the result set is empty.

**Signature:**
```csharp
public static T QueryFirst<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `T`: The first entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty

### QueryFirstOrDefault&lt;T&gt;(string sql)

Executes a query and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute

**Returns:**
- `T?`: The first entity of type T from the result set, or null if the result set is empty

**Example:**
```csharp
var product = connection.QueryFirstOrDefault<Product>("SELECT * FROM products WHERE id = 999");
// Returns null if no product with id 999 exists
```

### QueryFirstOrDefault&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql, object parameters) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query

**Returns:**
- `T?`: The first entity of type T from the result set, or null if the result set is empty

### QueryFirstOrDefault&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options)

Executes a query with command options and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `T?`: The first entity of type T from the result set, or null if the result set is empty

### QuerySingle&lt;T&gt;(string sql)

Executes a query and returns the single entity from the result set. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
public static T QuerySingle<T>(this IDbConnection connection, string sql) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute

**Returns:**
- `T`: The single entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty or contains more than one element

**Example:**
```csharp
var product = connection.QuerySingle<Product>("SELECT * FROM products WHERE id = 1");
```

### QuerySingle&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the single entity from the result set. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
public static T QuerySingle<T>(this IDbConnection connection, string sql, object parameters) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query

**Returns:**
- `T`: The single entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty or contains more than one element

### QuerySingle&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options)

Executes a query with command options and returns the single entity from the result set. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
public static T QuerySingle<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `T`: The single entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty or contains more than one element

### QuerySingleOrDefault&lt;T&gt;(string sql)

Executes a query and returns the single entity from the result set or the default value if the result set is empty. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute

**Returns:**
- `T?`: The single entity of type T from the result set, or null if the result set is empty

**Exceptions:**
- `InvalidOperationException`: If the result set contains more than one element

**Example:**
```csharp
var product = connection.QuerySingleOrDefault<Product>("SELECT * FROM products WHERE id = 1");
// Returns the product if found, null if not found, or throws if multiple products match
```

### QuerySingleOrDefault&lt;T&gt;(string sql, object parameters)

Executes a parameterized query and returns the single entity from the result set or the default value if the result set is empty. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql, object parameters) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `parameters`: Parameters for the query

**Returns:**
- `T?`: The single entity of type T from the result set, or null if the result set is empty

**Exceptions:**
- `InvalidOperationException`: If the result set contains more than one element

### QuerySingleOrDefault&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options)

Executes a query with command options and returns the single entity from the result set or the default value if the result set is empty. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `sql`: The SQL query to execute
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `T?`: The single entity of type T from the result set, or null if the result set is empty

**Exceptions:**
- `InvalidOperationException`: If the result set contains more than one element

## Async Variants

### QueryFirstAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the first entity from the result set. Throws an exception if the result set is empty.

**Signature:**
```csharp
public static Task<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryFirstOrDefaultAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the single entity from the result set. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleOrDefaultAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the single entity from the result set or the default value if the result set is empty. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
public static Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

## Async Variants with Parameters

### QueryFirstAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the first entity from the result set.

**Signature:**
```csharp
public static Task<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QueryFirstOrDefaultAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the single entity from the result set.

**Signature:**
```csharp
public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleOrDefaultAsync&lt;T&gt;(string sql, object parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query and returns the single entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
```

## Async Variants with Command Options

### QueryFirstAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the first entity from the result set.

**Signature:**
```csharp
public static Task<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QueryFirstOrDefaultAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the single entity from the result set.

**Signature:**
```csharp
public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleOrDefaultAsync&lt;T&gt;(string sql, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a query with command options and returns the single entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

## Async Variants with Parameters and Command Options

### QueryFirstAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the first entity from the result set.

**Signature:**
```csharp
public static Task<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QueryFirstOrDefaultAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the first entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the single entity from the result set.

**Signature:**
```csharp
public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### QuerySingleOrDefaultAsync&lt;T&gt;(string sql, object parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized query with command options and returns the single entity from the result set or the default value if the result set is empty.

**Signature:**
```csharp
public static Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

## Partial Mapping Variants

### QueryPartialFirst&lt;T&gt;(string sql)

Executes a query and returns the first entity from the result set using partial mapping mode.

**Signature:**
```csharp
public static T QueryPartialFirst<T>(this IDbConnection connection, string sql) where T : new()
```

### QueryPartialFirstOrDefault&lt;T&gt;(string sql)

Executes a query and returns the first entity from the result set or the default value if the result set is empty using partial mapping mode.

**Signature:**
```csharp
public static T? QueryPartialFirstOrDefault<T>(this IDbConnection connection, string sql) where T : new()
```

### QueryPartialSingle&lt;T&gt;(string sql)

Executes a query and returns the single entity from the result set using partial mapping mode.

**Signature:**
```csharp
public static T QueryPartialSingle<T>(this IDbConnection connection, string sql) where T : new()
```

### QueryPartialSingleOrDefault&lt;T&gt;(string sql)

Executes a query and returns the single entity from the result set or the default value if the result set is empty using partial mapping mode.

**Signature:**
```csharp
public static T? QueryPartialSingleOrDefault<T>(this IDbConnection connection, string sql) where T : new()
```

## Async Partial Mapping Variants

### QueryPartialFirstAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the first entity from the result set using partial mapping mode.

**Signature:**
```csharp
public static Task<T> QueryPartialFirstAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialFirstOrDefaultAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the first entity from the result set or the default value if the result set is empty using partial mapping mode.

**Signature:**
```csharp
public static Task<T?> QueryPartialFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialSingleAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the single entity from the result set using partial mapping mode.

**Signature:**
```csharp
public static Task<T> QueryPartialSingleAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```

### QueryPartialSingleOrDefaultAsync&lt;T&gt;(string sql, CancellationToken cancellationToken = default)

Asynchronously executes a query and returns the single entity from the result set or the default value if the result set is empty using partial mapping mode.

**Signature:**
```csharp
public static Task<T?> QueryPartialSingleOrDefaultAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
```