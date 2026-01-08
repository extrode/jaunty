# Jaunty Coding Conventions

This document identifies inconsistencies found in the Jaunty codebase and establishes coding conventions to follow going forward.

---

## Inconsistencies Found

### 1. Extension Method Syntax

**Issue**: Mixed use of C# 13 extension syntax and traditional extension methods.

| File | Syntax Used |
|------|-------------|
| `Query.cs` | C# 13: `extension(IDbConnection connection) { ... }` |
| All other files | Traditional: `public static T Method(this IDbConnection connection, ...)` |

**Convention**: Choose one approach consistently. Traditional extension methods are more compatible with older tooling and IDEs.

---

### 2. Async Return Pattern

**Issue**: Inconsistent use of `async/await` vs direct `Task` return.

| File | Pattern |
|------|---------|
| `QueryAsync.cs` | Direct return: `return QueryCoreAsync<T>(...)` |
| `QueryPartialAsync.cs` | Direct return: `return QueryCoreAsync<T>(...)` |
| `QueryFirstAsync.cs` | Wrapped: `return await QueryFirstCoreAsync<T>(...).ConfigureAwait(false)` |
| `QuerySingleAsync.cs` | Wrapped: `return await QuerySingleCoreAsync<T>(...)` |
| `QueryFirstOrDefaultAsync.cs` | Wrapped: `return await ...` (no ConfigureAwait) |
| `QueryScalarAsync.cs` | Direct return: `return QueryScalarCoreAsync<T>(...)` |
| `QueryMultipleAsync.cs` | Wrapped: `return await ...` |

**Convention**:
- For simple pass-through methods, return the `Task` directly (avoids state machine overhead)
- When exceptions need different stack traces or when `using` statements need to await disposal, use `async/await`

---

### 3. CancellationToken Parameter Missing

**Issue**: Some async methods are missing `CancellationToken` parameter.

| File | Has CancellationToken |
|------|-----------------------|
| `QueryAsync.cs` | Yes |
| `QueryPartialAsync.cs` | Yes |
| `QueryFirstAsync.cs` | Yes |
| `QuerySingleAsync.cs` | Yes |
| `QueryFirstOrDefaultAsync.cs` | **NO** |
| `QuerySingleOrDefaultAsync.cs` | **NO** |
| `QueryScalarAsync.cs` | Yes |
| `QueryStreamAsync.cs` | Yes |
| `QueryMultipleAsync.cs` | Yes |

**Convention**: All async public methods MUST have `CancellationToken cancellationToken = default` as the last parameter.

---

### 4. Connection Type Inconsistency

**Issue**: Mixed use of `IDbConnection` (interface) and `DbConnection` (abstract class).

| File | Connection Type |
|------|-----------------|
| `Query.cs`, `QueryPartial.cs`, etc. | `IDbConnection` |
| `QueryStreamAsync.cs` | `DbConnection` |
| `QueryPartialStreamAsync.cs` | `DbConnection` |
| `QueryMultiple.cs` | `IDbConnection` |
| `QueryMultipleAsync.cs` | `DbConnection` |

**Convention**:
- Sync methods: Use `IDbConnection` for maximum compatibility
- Async methods requiring `DbDataReader`: Use `DbConnection` with internal fallback to sync for `IDbConnection`

---

### 5. Named vs Positional Parameter Style

**Issue**: Inconsistent use of named parameters in internal calls.

| File | Style |
|------|-------|
| `QueryFirstAsync.cs` | Named: `cancellationToken: cancellationToken` |
| `QuerySingleAsync.cs` | Positional: `cancellationToken` |

**Convention**: Use positional parameters for internal calls where parameter order is stable and clear. Use named parameters only when improving readability for optional parameters.

---

### 6. Using Statement Inconsistency

**Issue**: Inconsistent imports across files.

| File | Has `using Jaunty.PublicApi;` |
|------|------------------------------|
| `QueryAsync.cs` | Yes |
| `QueryPartialAsync.cs` | No |
| `QueryStreamAsync.cs` | Yes |
| `QueryPartialStreamAsync.cs` | No |
| `QueryScalarAsync.cs` | No |
| `ExecuteScalarAsync.cs` | Yes |

**Convention**: Include only necessary `using` statements. Remove unused imports. Prefer explicit imports over global usings for clarity.

---

### 7. Byte Order Mark (BOM) Inconsistency

**Issue**: Some files start with UTF-8 BOM, others don't.

**Files with BOM**: `QueryPartial.cs`, `QueryFirstOrDefault.cs`, `QuerySingleOrDefault.cs`, `QuerySingleOrDefaultAsync.cs`, `QueryFirstOrDefaultAsync.cs`, `ExecuteReader.cs`, `ExecuteReaderAsync.cs`, `GridReader.cs`, `KeyAttribute.cs`, `DatabaseGeneratedAttribute.cs`, `DatabaseGeneratedOptions.cs`, `IMapped.cs`, `IEntity.cs`

**Convention**: Save all files as UTF-8 without BOM for consistency.

---

### 8. Duplicate API Methods

**Issue**: `QueryScalar<T>` and `ExecuteScalar<T>` have identical implementations.

```csharp
// QueryScalar.cs
public static T QueryScalar<T>(this IDbConnection connection, string sql)
    => QueryScalarCore<T>(connection, sql, null, default);

// ExecuteScalar.cs
public static T ExecuteScalar<T>(this IDbConnection connection, string sql)
    => QueryScalarCore<T>(connection, sql, null, default);
```

**Convention**: Avoid duplicate APIs. If aliases are needed, clearly document the preferred method and mark alternatives with `[Obsolete]` or remove them.

---

### 9. JauntyConfig.Reset() Bug

**Issue**: The `Reset()` method doesn't reset `_schemaNameResolver`.

```csharp
public static void Reset()
{
    _tableNameResolver = null;
    _columnNameResolver = null;
    // Missing: _schemaNameResolver = null;
}
```

**Convention**: Ensure all resettable state is included in reset methods.

---

### 10. GridReader CancellationToken Inconsistency

**Issue**: Only some async methods on `GridReader` accept `CancellationToken`.

| Method | Has CancellationToken |
|--------|-----------------------|
| `ReadAsync<T>()` | No |
| `ReadPartialAsync<T>()` | No |
| `ReadFirstAsync<T>()` | No |
| `ReadFirstOrDefaultAsync<T>()` | No |
| `ReadSingleAsync<T>()` | No |
| `ReadSingleOrDefaultAsync<T>()` | No |
| `ReadScalarAsync<T>()` | Yes |
| `ReadStreamAsync<T>()` | Yes |

**Convention**: All async methods should accept `CancellationToken` for proper cancellation support.

---

### 11. Unused Parameter

**Issue**: `ReadSingleOrDefaultCore` has unused `throwOnEmpty` parameter.

```csharp
private T? ReadSingleOrDefaultCore<T>(..., bool throwOnEmpty) where T : new()
{
    // throwOnEmpty is never used in the method body
}
```

**Convention**: Remove unused parameters. If future use is planned, add a comment explaining why.

---

### 12. XML Documentation Inconsistency

**Issue**: Inconsistent XML documentation coverage.

| Type | Has XML Docs |
|------|--------------|
| `CommandOptions<T>` | Yes |
| `CommandOptions` | No |
| `TableAttribute` | Yes |
| `ColumnAttribute` | Yes |
| `KeyAttribute` | Yes |
| `IgnoreAttribute` | Yes |
| `JauntyConfig` properties | Yes |
| `GridReader` methods | No |
| Public Query methods | No |

**Convention**: All public APIs MUST have XML documentation.

---

### 13. Namespace Style Inconsistency

**Issue**: Mixed namespace declaration styles.

| File | Style |
|------|-------|
| `IMapped.cs` | Block: `namespace Jaunty.PublicApi.Interfaces { ... }` |
| `IEntity.cs` | Block: `namespace Jaunty.PublicApi.Interfaces { ... }` |
| All other files | File-scoped: `namespace Jaunty;` |

**Convention**: Use file-scoped namespaces (C# 10+) consistently.

---

### 14. Error Message Inconsistency

**Issue**: Error messages use different formats.

```csharp
// QueryCore.cs
throw new InvalidOperationException("Sequence contains no elements");

// GridReader.cs
throw new InvalidOperationException("Sequence contains no elements");

// MetadataCache.cs
throw new InvalidOperationException(
    $"Strict mapping failed: Property '{prop.Property.Name}' was mapped more than once...");
```

**Convention**: Use consistent error message format:
- Start with context prefix when helpful (e.g., "Strict mapping failed:")
- Include relevant variable names/values
- End without period

---

## Established Conventions

### File Organization

1. **One public type per file**
2. **File name matches primary type name**
3. **Extension methods**: Use `partial class Jaunty` across files named after the method
4. **UTF-8 encoding without BOM**
5. **File-scoped namespaces**

### Naming

| Element | Convention | Example |
|---------|------------|---------|
| Public classes | PascalCase | `GridReader`, `CommandOptions` |
| Internal classes | PascalCase + `internal sealed` | `internal sealed class EntityMetadata` |
| Methods | PascalCase, verb-based | `Query<T>()`, `CreateSetter()` |
| Properties | PascalCase, noun-based | `ColumnName`, `Mapper` |
| Parameters | camelCase | `sql`, `reader`, `columnName` |
| Private fields | _camelCase | `_cache`, `_consumed` |
| Constants | PascalCase | N/A |
| Generic type params | Single uppercase letter | `<T>`, `<TResult>` |

### Async Methods

1. **Suffix**: All async methods end with `Async`
2. **CancellationToken**: Required as last parameter with `= default`
3. **ConfigureAwait**: Use `.ConfigureAwait(false)` in library code
4. **Return pattern**:
   - Direct `return Task` for simple pass-through
   - `async/await` when needed for resource management

### Extension Methods

1. **Sync methods**: Extend `IDbConnection`
2. **Async methods requiring DbDataReader**: Extend `DbConnection` with internal fallback
3. **Consistent overloads**: Each method should have:
   - `(string sql)`
   - `(string sql, object parameters)`
   - `(string sql, CommandOptions<T> options)`
   - `(string sql, object parameters, CommandOptions<T> options)`

### Error Handling

1. **Argument validation**: Use `ArgumentNullException.ThrowIfNull` on .NET 8+, manual checks otherwise
2. **Message format**: `"Context: specific message with 'variables'"`
3. **No trailing periods** in exception messages

### XML Documentation

1. **Required for**: All public types, methods, properties, and parameters
2. **Format**: Use `<summary>`, `<param>`, `<returns>`, `<exception>` tags
3. **Style**: Start with verb (e.g., "Gets", "Sets", "Executes")

### Conditional Compilation

1. **Pattern**: `#if NET8_0_OR_GREATER ... #else ... #endif`
2. **Features to guard**:
   - `FrozenDictionary`
   - `ArgumentNullException.ThrowIfNull`
   - `IAsyncEnumerable`
   - Async close methods

### Using Statements

1. **Order**: System namespaces first, then project namespaces
2. **Include only what's needed**
3. **Separate groups with blank line**

```csharp
using System.Data;
using System.Data.Common;

using Jaunty.InternalApi;
using Jaunty.InternalApi.Enums;
```

---

## Action Items

- [x] Standardize extension method syntax (C# 13 extension syntax)
- [x] Add CancellationToken to `QueryFirstOrDefaultAsync` and `QuerySingleOrDefaultAsync`
- [x] Add CancellationToken to all GridReader async methods
- [x] Fix `JauntyConfig.Reset()` to include `_schemaNameResolver`
- [x] Remove unused `throwOnEmpty` parameter from `ReadSingleOrDefaultCore`
- [x] Consolidate or deprecate `ExecuteScalar` (marked with `[Obsolete]`)
- [ ] Add XML documentation to all public APIs
- [ ] Remove BOM from all files
- [x] Convert `IMapped.cs` and `IEntity.cs` to file-scoped namespaces
- [x] Standardize using statements across all files
