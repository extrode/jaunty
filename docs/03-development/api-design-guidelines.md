# Jaunty API Design Guidelines

This document establishes guidelines for designing and evolving the Jaunty API. These guidelines ensure consistency, usability, and maintainability.

## Table of Contents

- [Naming Conventions](#naming-conventions)
- [Method Signatures](#method-signatures)
- [Error Handling](#error-handling)
- [Async Design](#async-design)
- [Extension Methods](#extension-methods)
- [Generic Type Constraints](#generic-type-constraints)
- [LINQ Usage Policy](#linq-usage-policy)
- [Documentation Standards](#documentation-standards)

---

## Naming Conventions

### Method Names

Use clear, descriptive names that indicate what the method does:

```csharp
// Good: Clear action + entity
connection.Query<Product>(sql);
connection.Insert(product);
connection.Delete<Product>(id);

// Avoid: Unclear or abbreviated
connection.GetProducts(sql);
connection.Add(product);
connection.Remove<Product>(id);
```

### Async Method Suffix

Always suffix async methods with `Async`:

```csharp
// Sync
List<T> Query<T>(string sql);

// Async
Task<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default);
```

### Command Pattern

Use consistent parameter ordering across all methods:

```csharp
// Standard order: connection, sql, parameters, options, cancellationToken
public static List<T> Query<T>(
    this IDbConnection connection, 
    string sql, 
    object? parameters = null, 
    CommandOptions<T>? options = null)

public static Task<List<T>> QueryAsync<T>(
    this IDbConnection connection, 
    string sql, 
    object? parameters = null, 
    CommandOptions<T>? options = null,
    CancellationToken cancellationToken = default)
```

---

## Method Signatures

### Optional Parameters

Use optional parameters for backward compatibility and ease of use:

```csharp
// Good: Optional parameters with defaults
public static List<T> QueryPartial<T>(
    this IDbConnection connection, 
    string sql, 
    object? parameters = null, 
    CommandOptions<T>? options = null)
```

### Overload Organization

Organize overloads from simplest to most complex:

```csharp
// 1. Simplest: Just SQL
public static List<T> Query<T>(this IDbConnection connection, string sql)

// 2. With parameters
public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters)

// 3. With options
public static List<T> Query<T>(this IDbConnection connection, string sql, CommandOptions<T> options)

// 4. Full: Parameters + options
public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options)
```

### Return Types

Choose return types based on semantics:

| Operation | Return Type | Rationale |
|-----------|-------------|-----------|
| Query (multiple) | `List<T>` | Collection of results |
| QueryFirst | `T` | Single entity |
| QueryFirstOrDefault | `T?` | Single entity or null |
| QuerySingle | `T` | Exactly one entity |
| Insert | `long` | Identity value (supports bigint) |
| Update/Delete | `int` | Row count (never exceeds int.MaxValue) |

---

## Error Handling

### Exception Types

Use specific exception types:

```csharp
// Good: Specific exceptions
throw new ArgumentNullException(nameof(connection));
throw new ArgumentException("Invalid SQL", nameof(sql));
throw new InvalidOperationException("Connection is not open");

// Avoid: Generic exceptions
throw new Exception("Something went wrong");
```

### Error Messages

Include helpful context in error messages:

```csharp
// Good: Includes type name and available options
throw new ArgumentException(
    $"No property found on type '{type.Name}' matching SQL parameter '@{sqlName}'. " +
    $"Available properties: {string.Join(", ", propertyLookup.Keys)}");

// Avoid: Vague messages
throw new ArgumentException("Property not found");
```

### Parameter Validation

Validate parameters at method entry:

```csharp
#if NET8_0_OR_GREATER
ArgumentNullException.ThrowIfNull(connection);
ArgumentNullException.ThrowIfNull(entity);
#else
if (connection is null) throw new ArgumentNullException(nameof(connection));
if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
```

---

## Async Design

### CancellationToken

Always provide CancellationToken with default value:

```csharp
public static async Task<List<T>> QueryAsync<T>(
    this IDbConnection connection, 
    string sql, 
    CancellationToken cancellationToken = default)
```

### ConfigureAwait

Use `ConfigureAwait(false)` for library code:

```csharp
public static async Task<List<T>> QueryAsync<T>(...)
{
    var result = await SomeOperationAsync().ConfigureAwait(false);
    return result;
}
```

### Async All the Way

Don't mix sync and async:

```csharp
// Good: Fully async
public static async Task<int> InsertAsync<T>(...)
{
    return await InsertCoreAsync(...).ConfigureAwait(false);
}

// Avoid: Blocking on async
public static int Insert<T>(...)
{
    return InsertAsync(...).Result; // Deadlock risk!
}
```

---

## Extension Methods

### When to Use

Use extension methods for:
- Operations that work on existing types (IDbConnection)
- Fluent API patterns
- Adding functionality without inheritance

### Organization

Group extension methods by functionality:

```csharp
// Read operations
public static partial class Jaunty
{
    public static List<T> Query<T>(...) { }
    public static T QueryFirst<T>(...) { }
}

// Write operations
public static partial class Jaunty
{
    public static long Insert<T>(...) { }
    public static int Update<T>(...) { }
    public static int Delete<T>(...) { }
}
```

### this Parameter

Always use `this` for the primary parameter:

```csharp
// Good: Extension on IDbConnection
public static List<T> Query<T>(this IDbConnection connection, string sql)

// Avoid: Not an extension method
public static List<T> Query<T>(IDbConnection connection, string sql)
```

---

## Generic Type Constraints

### When to Use

Use constraints to enforce requirements:

```csharp
// Good: Clear constraints
public static List<T> Query<T>(...) where T : new()

public static long Insert<T>(...) where T : class, new()
```

### Constraint Organization

Order constraints: `class` first, then `new()`, then interface constraints:

```csharp
// Good: Ordered constraints
where T : class, IEntity, new()

// Avoid: Unordered
where T : new(), class, IEntity
```

### Query vs Write Constraints

**Query methods**: Only `new()` (supports value types like ValueTuple)

```csharp
public static List<T> Query<T>(...) where T : new()
```

**Write methods**: `class, new()` (requires reference type for entity tracking)

```csharp
public static long Insert<T>(...) where T : class, new()
```

---

## LINQ Usage Policy

### Performance Considerations

Jaunty is designed for high performance and low allocations. LINQ, while expressive, often introduces unnecessary heap allocations (enumerators, closures, delegate instances) that can degrade performance in hot paths.

### Prohibited Usage

**Do NOT use LINQ in high-frequency execution paths:**

- Inside `while(reader.Read())` loops (result mapping)
- Inside `foreach(var entity in entities)` loops (bulk operations)
- Inside parameter binding loops
- In metadata resolution that happens per-query

### Permitted Usage

**LINQ is acceptable in the following scenarios:**

- **One-time Initialization**: Building cached SQL statements, parsing type metadata for the first time.
- **Exception Messages**: Formatting error details (where allocation is expected and non-critical).
- **Public API Surface**: Returning `IEnumerable<T>` or `IAsyncEnumerable<T>` where users expect LINQ compatibility.

### Preferred Alternatives

Use structured loops on concrete collections to minimize overhead:

```csharp
// Good: Minimal allocation
for (int i = 0; i < list.Count; i++)
{
    var item = list[i];
    // process
}

// Avoid: Allocates enumerator and potential closures
var results = list.Where(x => x.IsActive).Select(x => x.Name);
```

---

## Documentation Standards

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Executes a SQL query and returns all results mapped to entities.
/// </summary>
/// <typeparam name="T">The entity type to map results to.</typeparam>
/// <param name="connection">The database connection.</param>
/// <param name="sql">The SQL query to execute.</param>
/// <returns>A list of entities.</returns>
/// <exception cref="ArgumentNullException">Thrown when connection is null.</exception>
public static List<T> Query<T>(this IDbConnection connection, string sql) where T : new()
```

### Required Elements

Include these elements in documentation:

- `<summary>`: What the method does
- `<typeparam>`: Type parameter descriptions
- `<param>`: Parameter descriptions
- `<returns>`: Return value description
- `<exception>`: Exceptions that may be thrown
- `<seealso>`: Related methods

### Examples

Include practical examples:

```csharp
/// <example>
/// <code>
/// var products = connection.Query&lt;Product&gt;(
///     "SELECT * FROM products WHERE category_id = @CategoryId",
///     new { CategoryId = 5 });
/// </code>
/// </example>
```

---

## Versioning

### Breaking Changes

Avoid breaking changes. When necessary:

1. Mark old API as `[Obsolete]` with migration guidance
2. Keep old API for at least one major version
3. Document breaking changes in release notes

### Adding Features

When adding new features:

1. Add new overloads (don't change existing signatures)
2. Use optional parameters for backward compatibility
3. Document new features clearly

---

## Testing API Changes

### API Surface Tests

Consider adding tests that verify API surface:

```csharp
[Fact]
public void QueryAsync_HasCancellationTokenParameter()
{
    var method = typeof(Jaunty).GetMethod(
        nameof(Jaunty.QueryAsync), 
        new[] { typeof(IDbConnection), typeof(string), typeof(CancellationToken) });
    
    Assert.NotNull(method);
}
```

---

## References

- [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [XML Documentation Comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
