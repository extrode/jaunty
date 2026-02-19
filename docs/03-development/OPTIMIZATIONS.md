# Jaunty Optimization Roadmap

This document outlines the prioritized tasks for improving the performance, memory utilization, and code quality of the Jaunty micro-ORM.

## Phase 1: Critical Hot-Path Optimizations (High Impact)

### Task 1: Eliminate Reflection in Write Operations
- **Target Files**: `src/Jaunty/Internals/Write/InsertCore.cs`, `UpdateCore.cs`
- **Context**: `BindInsertParameters` and `BindUpdateParameters` currently call `PropertyInfo.GetValue(entity)` in a loop for every row.
- **Implementation**: Create an internal `WriteParameterCache<T>` that compiles and caches a single `Action<IDbCommand, T>` delegate for each entity type. This delegate should perform direct property access and add parameters to the command.
- **Goal**: Zero reflection and zero boxing during INSERT/UPDATE parameter binding.

### Task 2: Optimize Entity ID Population
- **Target Files**: `src/Jaunty/Internals/Write/InsertCore.cs` (`PopulateEntityId` method)
- **Context**: Currently uses `GetProperty("Id")` and `SetValue` on every successful insert.
- **Implementation**: Move the ID-property resolution logic to `MetadataBuilder`. Store a compiled `Action<T, long>?` setter in `EntityMetadata`.
- **Goal**: Replace runtime reflection with a cached, compiled delegate call.

### Task 3: Refactor Collection Parameter Expansion
- **Target Files**: `src/Jaunty/Internals/Parameters/ParameterBinder.cs` (`IsCollection` and `ExpandCollectionParameters`)
- **Context**: `IsCollection` currently allocates a `new List<object?>` and iterates the source `IEnumerable`, causing boxing for value types (e.g., `int` IDs).
- **Implementation**: Implement specialized checks for common types (e.g., `int[]`, `List<int>`, `string[]`). Use a non-allocating iteration pattern or `ArrayPool<object>` for heterogeneous collections.
- **Goal**: Reduce heap allocations and boxing during `WHERE IN @Ids` query preparation.

---

## Phase 2: Execution Engine Refinement (Medium-High Impact)

### Task 4: High-Performance Expression Tree Setters
- **Target Files**: `src/Jaunty/Internals/Entity/MetadataCache.cs` (`CreateSetter` method)
- **Context**: Currently uses `Convert.ChangeType` for all assignments.
- **Implementation**: Update the expression generator to check for primitive types. Emit direct casts or calls to `IDataRecord.GetInt32`, `GetString`, etc., for common types. Fall back to `Convert.ChangeType` only for complex conversions.
- **Goal**: Reduce the CPU overhead of mapping database values to entity properties.

### Task 5: Implement Multi-Entity Mapper Caching
- **Target Files**: `src/Jaunty/Internals/MultiEntityMapper.cs`, `src/Jaunty/Internals/QueryCore.cs`
- **Context**: `MultiEntityMapper<T1, T2>.Build(reader)` compiles expression trees every time a multi-entity query (tuples) is executed.
- **Implementation**: Introduce a `ConcurrentDictionary` in `MultiEntityMapper` to cache the mapping strategy. The key should be a combination of the column names and their ordinals from the `IDataReader`.
- **Goal**: Avoid expensive expression compilation on every multi-entity query.

---

## Phase 3: Memory Efficiency & Modernization (Medium Impact)

### Task 6: Zero-Allocation SQL Parameter Parsing (.NET 8+)
- **Target Files**: `src/Jaunty/Internals/Parameters/SqlParameterParser.cs`
- **Context**: Uses `string.Substring` when extracting parameter names.
- **Implementation**: Use `#if NET8_0_OR_GREATER` to utilize `ReadOnlySpan<char>` for parsing. Use `string.Intern` or a small internal pool for frequently seen parameter names if applicable.
- **Goal**: Eliminate temporary string allocations during the SQL parsing phase.

### Task 7: Cache Column-to-Property Mapping Signatures
- **Target Files**: `src/Jaunty/Internals/Entity/MetadataCache.cs` (`GetSetters` method)
- **Context**: Returns a `new PropertySetter<T>[]` array on every execution.
- **Implementation**: For common queries (like `SELECT *`), cache the `PropertySetter<T>[]` array based on the `reader.FieldCount` and a hash of column names.
- **Goal**: Reduce per-query array allocations.

---

## Validation Strategy
1. **Benchmarks**: Use `BenchmarkDotNet` to verify performance gains.
2. **Regression**: Run existing integration tests (`dotnet test`) to ensure mapping logic remains "Strict" and correct.
3. **Memory**: Use a profiler or `GC.GetTotalAllocatedBytes` checks in tests to confirm reduced allocations.
