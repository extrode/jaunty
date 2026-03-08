# Jaunty Hybrid Fast-Path Design

This document outlines the strategy for "piercing the interface ceiling" by shifting Jaunty's internal execution from `IDbCommand` to `DbCommand` while maintaining database agnosticism and `IDbConnection` compatibility.

## Goal
To achieve "Absolute Maximum Speed" by eliminating boxing of value types, reducing virtual method dispatch overhead, and enabling zero-allocation parameter binding.

## Architecture: Fast Path vs. Slow Path

All public extension methods will continue to accept `IDbConnection`. Internally, Jaunty will branch based on the underlying type:

```csharp
public static List<T> Query<T>(this IDbConnection connection, string sql)
{
    if (connection is DbConnection dbConn)
        return QueryFastPath<T>(dbConn, sql); // Uses DbCommand, GetFieldValue<T>, and Parameter Reuse

    return QueryLegacyPath<T>(connection, sql); // Current interface-based logic
}
```

## Optimization Pillars

### 1. Parameter Template Cloning
Instead of calling `CreateParameter()` and setting `ParameterName` in a loop, we cache an array of "Template Parameters" for a given SQL string.
- **The Gain**: No string allocations for names, no internal provider dictionary lookups.
- **The Risk**: `DbParameter` instances are not thread-safe.
- **The Fix**: Use `((ICloneable)template).Clone()` or a specialized `FastClone` method to create local copies for the command.

### 2. Zero-Boxing Row Mapping
Shift from `IDataRecord.GetValue(i)` (returns `object`) to `DbDataReader.GetFieldValue<T>(i)`.
- **The Gain**: Primitives (int, long, Guid, DateTime) are read directly into CPU registers without heap allocation.
- **The Risk**: Throws if the database type doesn't perfectly match `T`.
- **The Fix**: Optimized expression trees will attempt `GetFieldValue<T>` first, falling back to `GetValue` + `Convert` only when necessary.

### 3. Native Async Execution
Use `DbCommand.ExecuteReaderAsync` directly.
- **The Gain**: Avoids the overhead of interface-dispatch wrappers and provides better integration with the JIT's async machinery.

---

## Risk Assessment

### 1. The Wrapper Problem
**Scenario**: A user is using a logging or profiling tool that wraps `IDbConnection` but does not inherit from `DbConnection`.
- **Consequence**: `connection is DbConnection` returns `false`.
- **Mitigation**: The "Slow Path" fallback ensures the library still works, just at the current (interface-limited) speed.

### 2. Thread Safety and Parameter Pooling
**Scenario**: Two threads execute the same query simultaneously and try to use the same cached parameter instances.
- **Consequence**: One query's parameters overwrite the other's, leading to incorrect data or crashes.
- **Mitigation**: We must NEVER reuse the actual instance. We reuse the *metadata* to pre-size and pre-configure a new parameter array, or we use `ThreadStatic` storage.

### 3. Provider-Specific Behavior
**Scenario**: `Microsoft.Data.Sqlite` vs `System.Data.SqlClient` have different rules for when `DbType` must be set.
- **Consequence**: Inconsistent behavior across databases.
- **Mitigation**: Rely on the provider's own `DbCommand.CreateParameter()` logic for the templates to ensure the base parameter is "native" to that provider.

## Implementation Roadmap

1. **Phase 1**: Update `MetadataCache<T>` to generate two versions of setters: `ObjectSetter` (legacy) and `GenericSetter` (using `GetFieldValue<T>`).
2. **Phase 2**: Refactor `QueryCore` and `ExecuteReader` to support `DbConnection` branching.
3. **Phase 3**: Implement the `ParameterTemplate` system in `WriteParameterCache<T>` to avoid `CreateParameter` calls in INSERT/UPDATE loops.
4. **Phase 4**: Verification via `BenchmarkDotNet` to prove the allocation reduction.
