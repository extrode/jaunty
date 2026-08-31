# Performance Optimization Guide

**Version**: 2026.02.19  
**Status**: Active

---

## Overview

Jaunty is designed for high performance. This document details the optimization strategies used throughout the codebase.

---

## Memory Optimization

### 1. Pre-Size Collections

**Problem**: Dynamic resizing causes allocations and copies.

**Solution**: Pre-size collections when count is known or estimable.

```csharp
// BAD: Default capacity (4), will resize multiple times
var results = new List<T>();

// GOOD: Pre-size with expected count
var results = new List<T>(expectedRows);

// GOOD: Use capacity from metadata
var results = new List<T>(metadata.Properties.Length);
```

**Impact**: 50-80% reduction in allocations for large result sets.

---

### 2. Span<T> for Zero-Allocation Slicing

**Problem**: `Substring` allocates new strings.

**Solution**: Use `Span<T>` for zero-allocation slicing.

```csharp
// BAD: Allocates new string
var paramName = sql.Substring(start, length);

// GOOD: Zero allocation
ReadOnlySpan<char> sqlSpan = sql.AsSpan();
ReadOnlySpan<char> paramSpan = sqlSpan.Slice(start, length);

// Convert only when needed
var paramName = paramSpan.ToString();
```

**Impact**: Eliminates allocations in SQL parsing hot path.

---

### 3. FrozenDictionary for Lookups

**Problem**: `Dictionary<K,V>` has locking overhead and isn't optimized for reads.

**Solution**: Use `FrozenDictionary<K,V>` on .NET 8+.

```csharp
#if NET8_0_OR_GREATER
    private static readonly FrozenDictionary<string, int> _columnToIndex;
#else
    private static readonly Dictionary<string, int> _columnToIndex;
#endif

// Initialization
#if NET8_0_OR_GREATER
    _columnToIndex = properties.ToFrozenDictionary(p => p.ColumnName, p => p.Index);
#else
    _columnToIndex = properties.ToDictionary(p => p.ColumnName, p => p.Index);
#endif
```

**Impact**: 2-3x faster lookups, thread-safe without locking.

---

### 4. Compiled Delegates Instead of Reflection

**Problem**: `PropertyInfo.SetValue` is slow and allocates.

**Solution**: Compile expression trees once, reuse forever.

```csharp
// BAD: Slow reflection per row
property.SetValue(entity, reader.GetValue(columnIndex));

// GOOD: Compiled delegate (one-time cost)
private static Action<T, IDataRecord, int> CreateSetter(PropertyInfo property)
{
    // Build expression tree
    var targetParam = Expression.Parameter(typeof(T), "target");
    var readerParam = Expression.Parameter(typeof(IDataRecord), "reader");
    var indexParam = Expression.Parameter(typeof(int), "index");
    
    var getValueCall = Expression.Call(readerParam, nameof(IDataRecord.GetValue), null, indexParam);
    var convert = Expression.Convert(getValueCall, property.PropertyType);
    var assign = Expression.Assign(Expression.Property(targetParam, property), convert);
    
    // Compile once
    return Expression.Lambda<Action<T, IDataRecord, int>>(assign, targetParam, readerParam, indexParam).Compile();
}

// Usage: O(1) with no reflection
setter(entity, reader, columnIndex);
```

**Impact**: 10-100x faster property setting.

---

### 5. readonly struct for Small Value Types

**Problem**: Structs can cause hidden defensive copies.

**Solution**: Use `readonly struct` to prevent copies.

```csharp
// BAD: Potential defensive copies
public struct PropertyMetadata
{
    public string ColumnName { get; set; }
}

// GOOD: No defensive copies
public readonly struct PropertyMetadata
{
    public string ColumnName { get; init; }
}
```

**Impact**: Eliminates hidden allocations in tight loops.

---

## Execution Optimization

### 1. No LINQ in Hot Paths

**Problem**: LINQ allocates delegates and enumerators.

**Solution**: Use manual loops.

```csharp
// BAD: LINQ allocation
var columns = properties.Select(p => p.ColumnName).ToList();

// GOOD: Manual loop
var columns = new List<string>(properties.Length);
foreach (var p in properties)
{
    columns.Add(p.ColumnName);
}
```

**Impact**: 5-10x fewer allocations in hot paths.

---

### 2. Aggressive Inlining

**Problem**: Method call overhead in tight loops.

**Solution**: Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static string QuoteIdentifier(string identifier)
{
    return $"\"{identifier}\"";
}
```

**Impact**: Eliminates call overhead for small methods.

**Caution**: Don't inline large methods (increases code size, hurts I-cache).

---

### 3. ValueTask<T> for Async

**Problem**: `Task<T>` allocates even for synchronous completion.

**Solution**: Use `ValueTask<T>` on .NET 8+.

```csharp
#if NET8_0_OR_GREATER
public static async ValueTask<T> QuerySingleAsync<T>(...) where T : new()
{
    // May complete synchronously
}
#else
public static async Task<T> QuerySingleAsync<T>(...) where T : new()
{
    // Fallback for netstandard2.0
}
#endif
```

**Impact**: Zero allocation for synchronously-completing async operations.

---

### 4. ConfigureAwait(false)

**Problem**: Capturing `SynchronizationContext` causes overhead and potential deadlocks.

**Solution**: Always use `ConfigureAwait(false)` in library code.

```csharp
// GOOD: Doesn't capture context
await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
```

**Impact**: Reduced overhead, deadlock prevention.

---

### 5. Manual for Loops Over foreach

**Problem**: `foreach` allocates enumerator for some collections.

**Solution**: Use `for` loop for arrays and lists.

```csharp
// BAD: Potential enumerator allocation
foreach (var property in properties)
{
    // ...
}

// GOOD: No allocation
for (var i = 0; i < properties.Length; i++)
{
    var property = properties[i];
    // ...
}
```

**Impact**: Eliminates enumerator allocations.

---

## String Optimization

### 1. string.Create for Construction

**Problem**: String concatenation allocates intermediate strings.

**Solution**: Use `string.Create` for direct buffer writing.

```csharp
// BAD: Multiple allocations
var sql = "INSERT INTO " + tableName + " (" + columns + ") VALUES (" + values + ")";

// GOOD: Single allocation
var sql = string.Create(totalLength, (tableName, columns, values), (span, state) =>
{
    span.Slice(0, state.tableName.Length).CopyFrom(state.tableName);
    // ... write rest
});
```

**Impact**: Single allocation instead of multiple.

---

### 2. Ordinal String Comparison

**Problem**: Culture-aware comparison is slow.

**Solution**: Use `StringComparison.Ordinal`.

```csharp
// BAD: Culture-aware (slow)
if (columnName.ToLower() == parameterName.ToLower()) { }

// GOOD: Ordinal (fast)
if (columnName.Equals(parameterName, StringComparison.OrdinalIgnoreCase)) { }
```

**Impact**: 5-10x faster string comparison.

---

### 3. SearchValues<T> for Character Search

**Problem**: Multiple `IndexOf` calls for character search.

**Solution**: Use `SearchValues<T>` on .NET 8+.

```csharp
#if NET8_0_OR_GREATER
private static readonly SearchValues<char> s_commentStart = SearchValues.Create("--");
private static readonly SearchValues<char> s_stringChar = SearchValues.Create("'");

// Usage
var index = sqlSpan.IndexOfAny(s_commentStart);
#else
var index = sqlSpan.IndexOf("--");
#endif
```

**Impact**: 2-3x faster character searching.

---

## Caching Strategy

### Cache Hierarchy

| Cache | Scope | Lifetime | Access Pattern |
|-------|-------|----------|----------------|
| `MetadataCache<T>` | Per type | Application | Read-only after init |
| `ParameterCache<T>` | Per type | Application | Read-only after init |
| `SqlParameterParserCache` | Per SQL | Application | Read-heavy |
| `CrudSqlCache<T>` | Per type | Application | Read-only after init |
| `ParameterBinder` property getters | Per type | Application | Read-only after init |

### Cache Implementation

```csharp
// Static generic cache (thread-safe by CLR)
internal static class MetadataCache<T> where T : new()
{
    static MetadataCache() { /* One-time init */ }
    public static readonly EntityMetadata Metadata;
}

// ConcurrentDictionary for dynamic caching
internal static class SqlParameterParserCache
{
    // Size-capped: callers that embed literals or build SQL dynamically would otherwise leak
    // memory through an ever-growing set of distinct SQL-text keys.
    private static readonly BoundedCache<string, string[]> Cache = new(StringComparer.Ordinal);

    // AUD-R34-014: the same SQL text parses differently under MySQL/MariaDB, where a backslash
    // escapes the next character inside a string literal. Two caches rather than one composite
    // key, because the flag is fixed per engine.
    private static readonly BoundedCache<string, string[]> BackslashEscapedCache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql, bool backslashEscapes = false)
    {
        return backslashEscapes
            ? BackslashEscapedCache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s, backslashEscapes: true))
            : Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s));
    }
}
```

---

## Performance Checklist

### Code Review Checklist

- [ ] Pre-sized collections where count is known
- [ ] `Span<T>` for string/array slicing
- [ ] `FrozenDictionary` on .NET 8+
- [ ] Compiled delegates instead of reflection
- [ ] `readonly struct` for small value types
- [ ] No LINQ in hot paths
- [ ] `for` loops instead of `foreach` for arrays
- [ ] `ConfigureAwait(false)` on all async
- [ ] `StringComparison.Ordinal` for string comparison
- [ ] `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small methods
- [ ] `ValueTask<T>` for async methods that may complete synchronously

### Profiling Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| First query (cold) | <10ms | Includes metadata build |
| Subsequent queries | <1ms per 1000 rows | Warm cache |
| Allocations per row | <1KB | Depends on entity size |
| GC pressure | Gen 0 <10ms | During query execution |

---

## Benchmarking

### BenchmarkDotNet Setup

```csharp
[MemoryDiagnoser]
public class QueryBenchmarks
{
    private IDbConnection _connection;
    private const string Sql = "SELECT * FROM products WHERE category_id = @CategoryId";
    
    [GlobalSetup]
    public void Setup()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
    }
    
    [Benchmark]
    public List<Product> Query()
    {
        return _connection.Query<Product>(Sql, new { CategoryId = 1 }).ToList();
    }
}
```

### Running Benchmarks

```bash
dotnet run -c Release --project benchmarks/Jaunty.Benchmarks
```

---

## See Also

- [`architecture-specification.md`](architecture-specification.md) - Full architecture
- [`metadata-system-spec.md`](metadata-system-spec.md) - Metadata caching
- [`parameter-binding-spec.md`](parameter-binding-spec.md) - Parameter binding
