# Jaunty.FlatFiles — Code Analysis: Inconsistencies & Performance Issues

> Analysis date: March 2026
> Scope: All Jaunty.FlatFiles and Jaunty.FlatFiles.DuckDB source files

---

## Executive Summary

**Critical Issues:** 0
**High Priority Issues:** 3
**Medium Priority Issues:** 5
**Low Priority Issues:** 4

The Jaunty.FlatFiles implementation is **functionally correct** but has several inconsistencies with Jaunty core patterns and performance optimization opportunities.

---

## 1. API Design Inconsistencies

### HIGH: Missing Query<T>() Method in IFlatFileDatabase

**Issue:** The interface defines CRUD, write-back, and import operations but **lacks the core `Query<T>()` method** that users need to actually read data from flat files.

**Current State:**
```csharp
public interface IFlatFileDatabase
{
    // CRUD, Write-Back, Import methods exist...
    // But NO Query<T>() method!
}
```

**Impact:** Users cannot query flat files through the interface. They must cast to `DuckDbFlatFileDatabase` or use a different pattern.

**Expected (per spec.md User Stories):**
```csharp
var results = await db.Query<SalesRecord>()
    .Where(x => x.Revenue > 10000)
    .ToListAsync();
```

**Fix Required:** Add `IQueryable<T> Query<T>()` to `IFlatFileDatabase` and implement in `DuckDbFlatFileDatabase`.

---

### MEDIUM: Inconsistent Dispose Pattern

**Issue:** `IFlatFileDatabase` conditionally implements `IAsyncDisposable` only for non-netstandard2.0 targets:

```csharp
public interface IFlatFileDatabase : IDisposable
#if !NETSTANDARD2_0
    , IAsyncDisposable
#endif
```

**But** `DuckDbFlatFileDatabase` unconditionally implements both:
```csharp
public sealed class DuckDbFlatFileDatabase : IFlatFileDatabase
{
    public void Dispose() { ... }
    public async ValueTask DisposeAsync() { ... }
}
```

**Impact:** 
- netstandard2.0 consumers see `IAsyncDisposable` on the class but not the interface
- Violates Liskov Substitution Principle for netstandard2.0

**Fix:** Either:
1. Remove conditional compilation from interface (preferable - netstandard2.0 has IAsyncDisposable via package)
2. Add conditional compilation to class implementation

---

### MEDIUM: Missing `QueryAsync` Raw SQL Method

**Issue:** The original spec (data-model.md) shows:
```csharp
Task<List<T>> QueryAsync<T>(string rawSql, params object[] parameters);
```

This is **not implemented** in `IFlatFileDatabase`.

**Impact:** Users cannot execute raw SQL queries through the interface.

**Fix:** Add method to interface and implement in `DuckDbFlatFileDatabase`.

---

### LOW: Inconsistent Method Overloading Style

**Issue:** Jaunty core uses **method overloading** instead of optional parameters (per the coding standards). FlatFiles mostly follows this but has inconsistencies:

```csharp
// Good - overloads
ValueTask SaveAsync<T>(string outputPath, CancellationToken);
ValueTask SaveAsync<T>(WriteBackMode mode, CancellationToken);

// Inconsistent - optional parameter
ValueTask<long> ImportIntoAsync<T>(
    DbConnection targetConnection,
    Action<ImportOptions>? configure = null,  // ← Optional parameter
    CancellationToken cancellationToken = default);
```

**Fix:** Add overloads:
```csharp
ValueTask<long> ImportIntoAsync<T>(DbConnection targetConnection, CancellationToken);
ValueTask<long> ImportIntoAsync<T>(DbConnection targetConnection, Action<ImportOptions> configure, CancellationToken);
```

---

## 2. Performance Issues

### HIGH: Reflection in Hot Paths (Constitution Article III Violation)

**Issue:** `FlatFileExpressionHelper.GetColumnMappings()` and `GetDateTimeColumnNames()` use **uncached reflection**:

```csharp
public static List<(string ColumnName, PropertyInfo Property)> GetColumnMappings(Type entityType)
{
    var result = new List<(string, PropertyInfo)>();
    foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))  // ← Reflection!
    {
        if (!prop.CanRead || !prop.CanWrite) continue;
        result.Add((GetColumnName(prop), prop));  // ← GetColumnName calls GetCustomAttribute
    }
    return result;
}
```

**Called:** Every `InsertAsync`, `UpdateAsync`, `DeleteAsync`, and import operation.

**Constitution Article III Violation:**
> No runtime reflection in the abstractions layer

**Impact:** 
- ~500-1000ns overhead per call for reflection
- Multiplied by entity count in batch operations

**Fix:** Cache column mappings per type:
```csharp
private static readonly ConcurrentDictionary<Type, List<(string, PropertyInfo)>> _columnCache = new();

public static List<(string ColumnName, PropertyInfo Property)> GetColumnMappings(Type entityType)
{
    return _columnCache.GetOrAdd(entityType, type =>
    {
        var result = new List<(string, PropertyInfo)>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            result.Add((GetColumnName(prop), prop));
        }
        return result;
    });
}
```

---

### HIGH: Expression Compilation in Hot Path

**Issue:** `FlatFileExpressionHelper.EvaluateExpression()` **compiles expressions at runtime**:

```csharp
private static object? EvaluateExpression(Expression expression)
{
    // ...
    // Compile and invoke for complex expressions (closures, field access, etc.)
    var lambda = Expression.Lambda(expression);
    var compiled = lambda.Compile();  // ← Expensive!
    return compiled.DynamicInvoke();
}
```

**Called:** Every predicate evaluation in WHERE clauses.

**Impact:** 
- Expression compilation: ~5-10μs
- Should be <100ns for cached delegates

**Fix:** Cache compiled delegates:
```csharp
private static readonly ConcurrentDictionary<Expression, Func<object?>> _expressionCache = new();

private static object? EvaluateExpression(Expression expression)
{
    if (expression is ConstantExpression constant)
        return constant.Value;
    
    if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        return EvaluateExpression(unary.Operand);
    
    return _expressionCache.GetOrAdd(expression, expr =>
    {
        var lambda = Expression.Lambda<Func<object?>>(expr);
        return lambda.Compile();
    })();
}
```

---

### MEDIUM: Unnecessary LINQ Allocations in Import Pipeline

**Issue:** `ImportExecutor.ExecuteAsync()` creates LINQ allocations on every import:

```csharp
var columnNames = mappings.Select(m => m.ColumnName).ToList();  // ← Allocation
var parameterNames = Enumerable.Range(0, mappings.Count).Select(i => $"@p{i}").ToList();  // ← Allocation
```

**Called:** Every import operation.

**Impact:** 
- ~100-200 bytes allocated per import setup
- String interpolation in loop creates multiple string objects

**Fix:** Use pre-sized collections:
```csharp
var columnNames = new List<string>(mappings.Count);
foreach (var mapping in mappings)
    columnNames.Add(mapping.ColumnName);

var parameterNames = new List<string>(mappings.Count);
for (int i = 0; i < mappings.Count; i++)
    parameterNames.Add($"@p{i}");
```

---

### MEDIUM: entities.ToList() in Batch Insert

**Issue:** `InsertAsync(IEnumerable<T>)` eagerly materializes the entire sequence:

```csharp
var entityList = entities as IList<T> ?? entities.ToList();  // ← Full enumeration
```

**Impact:**
- Defeats purpose of `IEnumerable<T>` for streaming
- OOM risk for large sequences
- Unnecessary allocation if already a list

**Fix:** Check for common collection types first:
```csharp
var entityList = entities switch
{
    IList<T> list => list,
    ICollection<T> collection => collection.ToList(), // At least we know the count
    _ => entities.ToList()
};
```

Better: Support true streaming with a configurable max batch size.

---

### LOW: StringBuilder Not Pre-sized

**Issue:** Multiple `StringBuilder` usages don't specify initial capacity:

```csharp
var columns = new StringBuilder();  // Default: 16 chars
var values = new StringBuilder();
```

**Impact:** Multiple buffer reallocations for entities with many columns.

**Fix:** Estimate capacity:
```csharp
var columns = new StringBuilder(mappings.Count * 20);  // ~20 chars per column name
var values = new StringBuilder(mappings.Count * 10);   // ~10 chars per parameter
```

---

### LOW: DuckDBParameter Object Allocations

**Issue:** CRUD operations create new `DuckDBParameter` objects for every operation:

```csharp
parameters.Add(new DuckDBParameter { Value = prop.GetValue(entity) ?? DBNull.Value });
```

**Impact:** 
- GC pressure in high-throughput scenarios
- ~48 bytes per parameter

**Fix:** Reuse parameters where possible (requires DuckDB.NET API support for parameter reset).

---

## 3. Error Handling Issues

### MEDIUM: Silent Exception Swallow in ValidateTargetSchema

**Issue:** `ImportExecutor.ValidateTargetSchema()` catches all exceptions and silently continues:

```csharp
catch (Exception ex) when (ex is not InvalidOperationException)
{
    // Table doesn't exist — if CreateTableIfMissing was true, it should have been created already.
    // If not, the INSERT will fail with a clear error anyway.
}
```

**Impact:** 
- Hides potential bugs (connection issues, permission errors, etc.)
- Makes debugging harder

**Fix:** Log or re-wrap exception:
```csharp
catch (Exception ex) when (ex is not InvalidOperationException)
{
    // Only swallow if we expect the table to be created later
    if (!createTableIfMissing)
        throw new InvalidOperationException(
            $"Target table '{tableName}' does not exist and CreateTableIfMissing is false.", ex);
}
```

---

### LOW: Inconsistent Error Message Formatting

**Issue:** Error messages use different formats:
```csharp
// Format 1
throw new InvalidOperationException(
    $"No file source registered for entity type '{typeof(T).Name}'. " +
    $"Register it via AddCsv<{typeof(T).Name}>(), AddParquet<{typeof(T).Name}>(), etc.");

// Format 2
throw new ArgumentException(
    $"Cannot infer output format from extension '{ext}'. Supported: .csv, .tsv, .parquet, .json, .ndjson",
    nameof(path));
```

**Fix:** Standardize on one format (preferably Format 2 with `nameof`).

---

## 4. Threading & Concurrency

### MEDIUM: Race Condition in _modified Tracking

**Issue:** `_modified` dictionary is concurrent, but the check-then-set pattern is not atomic:

```csharp
if (result > 0) _modified[typeof(T)] = true;  // ← Race condition if concurrent calls
```

**Impact:** Lost updates if multiple threads call CRUD operations simultaneously (unlikely but possible).

**Fix:** Use atomic operation:
```csharp
if (result > 0) _modified.TryAdd(typeof(T), true);  // Or use Interlocked
```

---

### LOW: CancellationToken Not Propagated to File I/O

**Issue:** `SaveAsync<T>(WriteBackMode.Overwrite)` doesn't pass cancellation token to file operations:

```csharp
File.Delete(originalPath);  // ← No cancellation
File.Move(tempPath, originalPath);
```

**Fix:** Use async file I/O with cancellation (requires .NET 8+ or custom wrapper).

---

## 5. Documentation & XML Comments

### LOW: Missing XML Comments on Internal Helpers

**Issue:** Several internal methods lack XML documentation:
- `ApplyPreload()`
- `InferFormatFromExtension()`
- `GetSourceOrThrow<T>()`

**Fix:** Add summary comments.

---

### LOW: Inconsistent Parameter Documentation

**Issue:** Some methods document `cancellationToken`, others don't:
```csharp
/// <param name="cancellationToken">A token to monitor for cancellation.</param>  // ← Present
// ...or missing entirely
```

**Fix:** Add to all async methods.

---

## 6. Test Coverage Gaps

### MEDIUM: No Tests for Edge Cases

**Missing test scenarios:**
1. Concurrent CRUD operations on same entity type
2. Expression compilation caching verification
3. Large batch insert (>10K entities)
4. DateTime column casting edge cases
5. Schema validation with nullable types

---

## 7. NativeAOT Compatibility

### MEDIUM: Reflection Usage May Break Trimming

**Issue:** While the abstractions package is marked `IsAotCompatible`, the reflection usage in:
- `GetColumnMappings()`
- `GetDateTimeColumnNames()`
- `EvaluateExpression()`

May be trimmed if not properly annotated.

**Fix:** Add `[DynamicallyAccessedMembers]` attributes:
```csharp
public static List<(string ColumnName, PropertyInfo Property)> GetColumnMappings(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] 
    Type entityType)
```

---

## Priority Matrix

| Issue | Severity | Effort | Priority |
|-------|----------|--------|----------|
| Missing Query<T>() | High | Low | P0 |
| Reflection in hot paths | High | Medium | P0 |
| Expression compilation | High | Medium | P0 |
| Inconsistent Dispose pattern | Medium | Low | P1 |
| LINQ allocations in import | Medium | Low | P1 |
| entities.ToList() eager materialization | Medium | Medium | P1 |
| Race condition in _modified | Medium | Low | P1 |
| Missing QueryAsync raw SQL | Medium | Low | P1 |
| Optional parameter vs overloads | Low | Low | P2 |
| StringBuilder not pre-sized | Low | Low | P2 |
| DuckDBParameter allocations | Low | High | P2 |
| Silent exception swallow | Low | Low | P2 |
| NativeAOT annotations | Medium | Low | P1 |

---

## Recommended Action Plan

### Phase 1 (Critical - P0)
1. Add `Query<T>()` method to interface and implementation
2. Cache column mappings to eliminate reflection
3. Cache compiled expressions

### Phase 2 (Important - P1)
4. Fix Dispose pattern consistency
5. Add `QueryAsync(string rawSql)` method
6. Optimize LINQ allocations in import pipeline
7. Add NativeAOT annotations
8. Fix race condition in _modified tracking

### Phase 3 (Nice-to-have - P2)
9. Convert optional parameters to overloads
10. Pre-size StringBuilders
11. Improve error handling consistency
12. Add missing XML documentation

---

## Conclusion

The Jaunty.FlatFiles implementation is **functionally complete** but has **performance and consistency issues** that should be addressed before v1.0 release. The most critical issues are:

1. **Missing Query<T>()** - Core functionality gap
2. **Uncached reflection** - Violates Constitution Article III
3. **Expression compilation** - Significant performance impact

Addressing these three issues should be the top priority.
