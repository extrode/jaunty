# Stored Procedures

## Overview

Jaunty provides comprehensive support for executing stored procedures with various parameter types including input, output, and return parameters. The stored procedure methods work with both synchronous and asynchronous operations.

## Executing Stored Procedures

### ExecuteStoredProcedure&lt;T&gt;(string procedureName)

Executes a stored procedure and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName) where T : new()
```

**Type Parameters:**
- `T`: The entity type to map results to (must have a parameterless constructor)

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute

**Returns:**
- `List<T>`: A list of entities of type T mapped from the result set

**Example:**
```csharp
var products = connection.ExecuteStoredProcedure<Product>("GetActiveProducts");
```

### ExecuteStoredProcedure&lt;T&gt;(string procedureName, object? parameters)

Executes a stored procedure with parameters and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute
- `parameters`: Parameters to pass to the stored procedure

**Returns:**
- `List<T>`: A list of entities of type T mapped from the result set

**Example:**
```csharp
var products = connection.ExecuteStoredProcedure<Product>(
    "GetProductsByCategory", 
    new { CategoryId = 1 });
```

### ExecuteStoredProcedure&lt;T&gt;(string procedureName, object? parameters, CommandOptions&lt;T&gt; options)

Executes a stored procedure with parameters and command options, returning the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute
- `parameters`: Parameters to pass to the stored procedure
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `List<T>`: A list of entities of type T mapped from the result set

## Single Result Stored Procedure Methods

### ExecuteStoredProcedureFirst&lt;T&gt;(string procedureName)

Executes a stored procedure and returns the first result from the result set using strict mapping mode.

**Signature:**
```csharp
public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute

**Returns:**
- `T`: The first entity of type T from the result set

**Exceptions:**
- `InvalidOperationException`: If the result set is empty

### ExecuteStoredProcedureFirst&lt;T&gt;(string procedureName, object? parameters)

Executes a parameterized stored procedure and returns the first result from the result set using strict mapping mode.

**Signature:**
```csharp
public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
```

### ExecuteStoredProcedureFirst&lt;T&gt;(string procedureName, object? parameters, CommandOptions&lt;T&gt; options)

Executes a parameterized stored procedure with command options and returns the first result from the result set using strict mapping mode.

**Signature:**
```csharp
public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
```

## First or Default Stored Procedure Methods

### ExecuteStoredProcedureFirstOrDefault&lt;T&gt;(string procedureName)

Executes a stored procedure and returns the first result from the result set or the default value if the result set is empty, using strict mapping mode.

**Signature:**
```csharp
public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute

**Returns:**
- `T?`: The first entity of type T from the result set, or null if the result set is empty

### ExecuteStoredProcedureFirstOrDefault&lt;T&gt;(string procedureName, object? parameters)

Executes a parameterized stored procedure and returns the first result or default value using strict mapping mode.

**Signature:**
```csharp
public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
```

### ExecuteStoredProcedureFirstOrDefault&lt;T&gt;(string procedureName, object? parameters, CommandOptions&lt;T&gt; options)

Executes a parameterized stored procedure with command options and returns the first result or default value using strict mapping mode.

**Signature:**
```csharp
public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
```

## Scalar Stored Procedure Methods

### ExecuteStoredProcedureScalar&lt;T&gt;(string procedureName)

Executes a stored procedure and returns a scalar value.

**Signature:**
```csharp
public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName)
```

**Type Parameters:**
- `T`: The type of the scalar value to return

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute

**Returns:**
- `T`: The scalar value returned by the stored procedure

**Example:**
```csharp
var count = connection.ExecuteStoredProcedureScalar<int>("GetProductCount");
```

### ExecuteStoredProcedureScalar&lt;T&gt;(string procedureName, object? parameters)

Executes a parameterized stored procedure and returns a scalar value.

**Signature:**
```csharp
public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, object? parameters)
```

### ExecuteStoredProcedureScalar&lt;T&gt;(string procedureName, object? parameters, CommandOptions options)

Executes a parameterized stored procedure with command options and returns a scalar value.

**Signature:**
```csharp
public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options)
```

## Non-Query Stored Procedure Methods

### ExecuteStoredProcedureNonQuery(string procedureName)

Executes a stored procedure that does not return results (such as INSERT, UPDATE, DELETE operations) and returns the number of affected rows.

**Signature:**
```csharp
public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName)
```

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute

**Returns:**
- `int`: The number of rows affected by the stored procedure

**Example:**
```csharp
var rowsAffected = connection.ExecuteStoredProcedureNonQuery("UpdateProductPrices");
```

### ExecuteStoredProcedureNonQuery(string procedureName, object? parameters)

Executes a parameterized stored procedure that does not return results and returns the number of affected rows.

**Signature:**
```csharp
public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, object? parameters)
```

### ExecuteStoredProcedureNonQuery(string procedureName, object? parameters, CommandOptions options)

Executes a parameterized stored procedure with command options that does not return results and returns the number of affected rows.

**Signature:**
```csharp
public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options)
```

## Async Stored Procedure Methods

### ExecuteStoredProcedureAsync&lt;T&gt;(string procedureName, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureAsync&lt;T&gt;(string procedureName, object? parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized stored procedure and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureAsync&lt;T&gt;(string procedureName, object? parameters, CommandOptions&lt;T&gt; options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized stored procedure with command options and returns the results as a list of entities using strict mapping mode.

**Signature:**
```csharp
public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureFirstAsync&lt;T&gt;(string procedureName, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure and returns the first result from the result set using strict mapping mode.

**Signature:**
```csharp
public static Task<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureFirstOrDefaultAsync&lt;T&gt;(string procedureName, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure and returns the first result or default value using strict mapping mode.

**Signature:**
```csharp
public static Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureScalarAsync&lt;T&gt;(string procedureName, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure and returns a scalar value.

**Signature:**
```csharp
public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default)
```

### ExecuteStoredProcedureScalarAsync&lt;T&gt;(string procedureName, object? parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized stored procedure and returns a scalar value.

**Signature:**
```csharp
public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default)
```

### ExecuteStoredProcedureNonQueryAsync(string procedureName, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure that does not return results and returns the number of affected rows.

**Signature:**
```csharp
public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default)
```

### ExecuteStoredProcedureNonQueryAsync(string procedureName, object? parameters, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized stored procedure that does not return results and returns the number of affected rows.

**Signature:**
```csharp
public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default)
```

### ExecuteStoredProcedureNonQueryAsync(string procedureName, object? parameters, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously executes a parameterized stored procedure with command options that does not return results and returns the number of affected rows.

**Signature:**
```csharp
public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options, CancellationToken cancellationToken = default)
```

## Working with Output Parameters

Jaunty provides the `SpParameters` class for working with stored procedure parameters including input, output, and return parameters.

### Creating SpParameters

```csharp
var parameters = new SpParameters()
    .AddInput("CategoryId", 1)
    .AddOutput("TotalProducts", DbType.Int32)
    .AddInputOutput("LastUpdated", DateTime.Now, DbType.DateTime)
    .AddReturnValue("ReturnCode", DbType.Int32);
```

### ExecuteStoredProcedure&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions&lt;T&gt; options = default)

Executes a stored procedure with output parameters and returns the results as a list of entities. Output parameter values can be retrieved from the `SpParameters` object after execution.

**Signature:**
```csharp
public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
```

**Parameters:**
- `connection`: The database connection
- `procedureName`: The name of the stored procedure to execute
- `parameters`: The stored procedure parameters including input, output, and return parameters
- `options`: Command options (transaction, timeout, custom mapper)

**Returns:**
- `List<T>`: A list of entities of type T mapped from the result set

**Example:**
```csharp
var spParams = new SpParameters()
    .AddInput("CategoryId", 1)
    .AddOutput("ProductCount", DbType.Int32);

var products = connection.ExecuteStoredProcedure<Product>("GetProductsByCategory", spParams);

// Retrieve output parameter value after execution
var productCount = spParams.Get<int>("ProductCount");
```

### ExecuteStoredProcedureFirst&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions&lt;T&gt; options = default)

Executes a stored procedure with output parameters and returns the first result from the result set. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
```

### ExecuteStoredProcedureFirstOrDefault&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions&lt;T&gt; options = default)

Executes a stored procedure with output parameters and returns the first result or default value. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
```

### ExecuteStoredProcedureScalar&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions options = default)

Executes a stored procedure with output parameters and returns a scalar value. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default)
```

### ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default)

Executes a stored procedure with output parameters that does not return results. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default)
```

## Async Methods with Output Parameters

### ExecuteStoredProcedureAsync&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure with output parameters and returns the results as a list of entities. Output parameter values can be retrieved from the `SpParameters` object after execution.

**Signature:**
```csharp
public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureFirstAsync&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure with output parameters and returns the first result from the result set. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static Task<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureFirstOrDefaultAsync&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions&lt;T&gt; options = default, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure with output parameters and returns the first result or default value. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
```

### ExecuteStoredProcedureScalarAsync&lt;T&gt;(string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure with output parameters and returns a scalar value. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)
```

### ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)

Asynchronously executes a stored procedure with output parameters that does not return results. Output parameter values can be retrieved after execution.

**Signature:**
```csharp
public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)
```

## SpParameters Class

The `SpParameters` class provides methods for defining stored procedure parameters:

### AddInput(string name, object? value)

Adds an input parameter to the stored procedure call.

**Signature:**
```csharp
public SpParameters AddInput(string name, object? value)
```

### AddInput(string name, object? value, DbType dbType, int? size = null)

Adds an input parameter with explicit database type and optional size.

**Signature:**
```csharp
public SpParameters AddInput(string name, object? value, DbType dbType, int? size = null)
```

### AddOutput(string name, DbType dbType, int? size = null)

Adds an output parameter to the stored procedure call.

**Signature:**
```csharp
public SpParameters AddOutput(string name, DbType dbType, int? size = null)
```

### AddInputOutput(string name, object? value, DbType dbType, int? size = null)

Adds an input/output parameter to the stored procedure call.

**Signature:**
```csharp
public SpParameters AddInputOutput(string name, object? value, DbType dbType, int? size = null)
```

### AddReturnValue(string name = "RETURN_VALUE", DbType dbType = DbType.Int32)

Adds a return value parameter to the stored procedure call.

**Signature:**
```csharp
public SpParameters AddReturnValue(string name = "RETURN_VALUE", DbType dbType = DbType.Int32)
```

### Get&lt;T&gt;(string name)

Retrieves the value of an output or input/output parameter after the stored procedure has executed.

**Signature:**
```csharp
public T? Get<T>(string name)
```

**Parameters:**
- `name`: The parameter name

**Returns:**
- `T?`: The parameter value converted to the specified type

### GetReturnValue()

Retrieves the return value after the stored procedure has executed.

**Signature:**
```csharp
public int GetReturnValue()
```

**Returns:**
- `int`: The return value (typically an integer)

### HasValue(string name)

Checks if a parameter exists and has a non-null value.

**Signature:**
```csharp
public bool HasValue(string name)
```

**Parameters:**
- `name`: The parameter name

**Returns:**
- `bool`: True if the parameter exists and has a non-null, non-DBNull value

## Important Notes

- **Command Type**: Stored procedure methods automatically set the command type to `CommandType.StoredProcedure`
- **Parameter Format**: Parameter names should not include the `@` prefix when using `SpParameters`
- **Output Parameters**: Use `SpParameters` class to define and retrieve output parameter values
- **Async Requirements**: Async operations require a `DbConnection` rather than just `IDbConnection`
- **Transaction Support**: All stored procedure methods support transactions via `CommandOptions`
- **Timeout Support**: All stored procedure methods support command timeout via `CommandOptions`
- **Return Values**: Non-query methods return the number of affected rows; scalar methods return the scalar value; query methods return mapped entities
- **Mapping Mode**: All stored procedure query methods use strict mapping mode by default