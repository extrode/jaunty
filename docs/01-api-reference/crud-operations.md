# CRUD Operations

## Overview

CRUD (Create, Read, Update, Delete) operations provide methods for inserting, updating, and deleting entities in the database. These methods work with entities that follow the standard conventions and can be customized with attributes.

## Insert Operations

### Insert&lt;T&gt;(T entity)

Inserts an entity into the database and returns the generated identity value for identity columns, or 1 for non-identity inserts. For entities implementing `IEntity` or `IEntity<T>`, the Id property is automatically populated.

**Signature:**
```csharp
public static long Insert<T>(this IDbConnection connection, T entity) where T : class, new()
```

**Type Parameters:**
- `T`: The entity type (must be a class with a parameterless constructor)

**Parameters:**
- `connection`: The database connection
- `entity`: The entity to insert

**Returns:**
- `long`: Generated identity value, or 1 for non-identity inserts

**Example:**
```csharp
var product = new Product { ProductName = "New Product", CategoryId = 1, Price = 10.99m };
var productId = connection.Insert(product);
Console.WriteLine($"Inserted product with ID: {productId}");
```

### Insert&lt;T&gt;(T entity, CommandOptions options)

Inserts an entity into the database with command options and returns the generated identity value for identity columns, or 1 for non-identity inserts.

**Signature:**
```csharp
public static long Insert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `entity`: The entity to insert
- `options`: Command options (transaction, timeout)

**Returns:**
- `long`: Generated identity value, or 1 for non-identity inserts

**Example:**
```csharp
using var transaction = connection.BeginTransaction();
try
{
    var product = new Product { ProductName = "New Product", CategoryId = 1, Price = 10.99m };
    var productId = connection.Insert(product, CommandOptions.WithTransaction(transaction));
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

## Async Insert Operations

### InsertAsync&lt;T&gt;(T entity, CancellationToken cancellationToken = default)

Asynchronously inserts an entity into the database and returns the generated identity value for identity columns, or 1 for non-identity inserts.

**Signature:**
```csharp
public static Task<long> InsertAsync<T>(this DbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `entity`: The entity to insert
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<long>`: A task that resolves to the generated identity value, or 1 for non-identity inserts

**Example:**
```csharp
var product = new Product { ProductName = "New Product", CategoryId = 1, Price = 10.99m };
var productId = await connection.InsertAsync(product, cancellationToken);
```

### InsertAsync&lt;T&gt;(T entity, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously inserts an entity into the database with command options and returns the generated identity value for identity columns, or 1 for non-identity inserts.

**Signature:**
```csharp
public static Task<long> InsertAsync<T>(this DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `entity`: The entity to insert
- `options`: Command options (transaction, timeout)
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<long>`: A task that resolves to the generated identity value, or 1 for non-identity inserts

## Update Operations

### Update&lt;T&gt;(T entity)

Updates an entity in the database using the primary key(s) to identify the row to update. Returns the number of affected rows.

**Signature:**
```csharp
public static int Update<T>(this IDbConnection connection, T entity) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `entity`: The entity to update (must have primary key values set)

**Returns:**
- `int`: The number of rows affected by the update

**Example:**
```csharp
var product = new Product { ProductId = 1, ProductName = "Updated Product Name", Price = 15.99m };
var rowsAffected = connection.Update(product);
Console.WriteLine($"Updated {rowsAffected} rows");
```

### Update&lt;T&gt;(T entity, CommandOptions options)

Updates an entity in the database with command options using the primary key(s) to identify the row to update.

**Signature:**
```csharp
public static int Update<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `entity`: The entity to update
- `options`: Command options (transaction, timeout)

**Returns:**
- `int`: The number of rows affected by the update

## Async Update Operations

### UpdateAsync&lt;T&gt;(T entity, CancellationToken cancellationToken = default)

Asynchronously updates an entity in the database using the primary key(s) to identify the row to update.

**Signature:**
```csharp
public static Task<int> UpdateAsync<T>(this DbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `entity`: The entity to update
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<int>`: A task that resolves to the number of rows affected by the update

**Example:**
```csharp
product.Price = 15.99m;
var rowsAffected = await connection.UpdateAsync(product, cancellationToken);
```

### UpdateAsync&lt;T&gt;(T entity, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously updates an entity in the database with command options using the primary key(s) to identify the row to update.

**Signature:**
```csharp
public static Task<int> UpdateAsync<T>(this DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `entity`: The entity to update
- `options`: Command options (transaction, timeout)
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<int>`: A task that resolves to the number of rows affected by the update

## Delete Operations

### Delete&lt;T&gt;(T entity)

Deletes an entity from the database using the primary key(s) to identify the row to delete. Returns the number of affected rows.

**Signature:**
```csharp
public static int Delete<T>(this IDbConnection connection, T entity) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `entity`: The entity to delete (only primary key values need to be set)

**Returns:**
- `int`: The number of rows affected by the delete

**Example:**
```csharp
var product = new Product { ProductId = 1 }; // Only primary key is needed
var rowsAffected = connection.Delete(product);
Console.WriteLine($"Deleted {rowsAffected} rows");
```

### Delete&lt;T&gt;(T entity, CommandOptions options)

Deletes an entity from the database with command options using the primary key(s) to identify the row to delete.

**Signature:**
```csharp
public static int Delete<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `entity`: The entity to delete
- `options`: Command options (transaction, timeout)

**Returns:**
- `int`: The number of rows affected by the delete

### Delete&lt;T&gt;(object id)

Deletes an entity by its primary key value. Only works for entities with a single primary key. Returns the number of affected rows.

**Signature:**
```csharp
public static int Delete<T>(this IDbConnection connection, object id) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `id`: The primary key value

**Returns:**
- `int`: The number of rows affected by the delete

**Example:**
```csharp
var rowsAffected = connection.Delete<Product>(1); // Delete product with ID 1
Console.WriteLine($"Deleted {rowsAffected} rows");
```

### Delete&lt;T&gt;(object id, CommandOptions options)

Deletes an entity by its primary key value with command options. Only works for entities with a single primary key.

**Signature:**
```csharp
public static int Delete<T>(this IDbConnection connection, object id, CommandOptions options) where T : class, new()
```

**Parameters:**
- `connection`: The database connection
- `id`: The primary key value
- `options`: Command options (transaction, timeout)

**Returns:**
- `int`: The number of rows affected by the delete

## Async Delete Operations

### DeleteAsync&lt;T&gt;(T entity, CancellationToken cancellationToken = default)

Asynchronously deletes an entity from the database using the primary key(s) to identify the row to delete.

**Signature:**
```csharp
public static Task<int> DeleteAsync<T>(this DbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `entity`: The entity to delete
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<int>`: A task that resolves to the number of rows affected by the delete

**Example:**
```csharp
var rowsAffected = await connection.DeleteAsync(product, cancellationToken);
```

### DeleteAsync&lt;T&gt;(T entity, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously deletes an entity from the database with command options using the primary key(s) to identify the row to delete.

**Signature:**
```csharp
public static Task<int> DeleteAsync<T>(this DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `entity`: The entity to delete
- `options`: Command options (transaction, timeout)
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<int>`: A task that resolves to the number of rows affected by the delete

### DeleteAsync&lt;T&gt;(object id, CancellationToken cancellationToken = default)

Asynchronously deletes an entity by its primary key value. Only works for entities with a single primary key.

**Signature:**
```csharp
public static Task<int> DeleteAsync<T>(this DbConnection connection, object id, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `id`: The primary key value
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<int>`: A task that resolves to the number of rows affected by the delete

### DeleteAsync&lt;T&gt;(object id, CommandOptions options, CancellationToken cancellationToken = default)

Asynchronously deletes an entity by its primary key value with command options. Only works for entities with a single primary key.

**Signature:**
```csharp
public static Task<int> DeleteAsync<T>(this DbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
```

**Parameters:**
- `connection`: The database connection (must be a `DbConnection` for async operations)
- `id`: The primary key value
- `options`: Command options (transaction, timeout)
- `cancellationToken`: Cancellation token

**Returns:**
- `Task<int>`: A task that resolves to the number of rows affected by the delete

## Real-World Example: Transactional CRUD

```csharp
using var transaction = connection.BeginTransaction();
try
{
    var options = CommandOptions.WithTransaction(transaction);

    var newProduct = new Product { ProductName = "Widget Pro", CategoryId = 1, Price = 24.99m };
    var newId = connection.Insert(newProduct, options);

    newProduct.Price = 19.99m;
    connection.Update(newProduct, options);

    connection.Delete<Product>(discontinuedProductId, options);

    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

## Entity Mapping Conventions

### Primary Key Detection

Jaunty automatically detects primary keys using:
1. `[Key]` attribute on a property
2. Convention-based detection (properties named "Id", "{ClassName}Id", etc.)

### Identity Columns

For identity columns, the generated value is returned by Insert operations and can be automatically populated in the entity if it implements `IEntity` or `IEntity<T>`.

### Column Mapping

Properties are mapped to columns using:
1. `[Column]` attribute for custom column names
2. Property name (converted using configured naming convention)
3. Global configuration via `JauntyConfig.ColumnNameResolver`

## Important Notes

- **Entity Requirements**: All entity types must have a parameterless constructor (`where T : class, new()`)
- **Primary Keys**: Update and Delete operations require primary key information to identify the correct row
- **Return Values**: 
  - Insert returns the generated identity value (or 1 for non-identity columns)
  - Update and Delete return the number of affected rows
- **Async Requirements**: Async operations require a `DbConnection` rather than just `IDbConnection`
- **Transaction Support**: All operations support transactions via `CommandOptions`
- **Timeout Support**: All operations support command timeout via `CommandOptions`
- **Mapping Attributes**: Use `[Table]`, `[Column]`, `[Key]`, `[Ignore]`, `[DatabaseGenerated]` attributes to customize mapping