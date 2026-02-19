# Optimization Progress Checklist

This checklist tracks the implementation of performance and memory optimizations for Jaunty. 

## Roadmap & Progress

### Phase 1: Critical Hot-Path Optimizations
- [x] **Task 1: Eliminate Reflection in Write Operations**
  - **Goal**: Use compiled expression trees instead of `PropertyInfo.GetValue`.
  - **Files**: `src/Jaunty/Internals/Write/WriteParameterCache.cs`, `InsertCore.cs`, `UpdateCore.cs`.
  - **Result**: Reflection removed from `BindInsertParameters` and `BindUpdateParameters`.
- [x] **Task 2: Optimize Entity ID Population**
  - **Goal**: Cache the ID property setter during metadata build.
  - **Files**: `src/Jaunty/Internals/Write/WriteParameterCache.cs`, `InsertCore.cs`.
  - **Result**: `PopulateEntityId` now uses a cached, compiled delegate.
- [x] **Task 3: Refactor Collection Parameter Expansion**
  - **Goal**: Reduce allocations/boxing in `WHERE IN @Ids` scenarios.
  - **Target**: `src/Jaunty/Internals/Parameters/ParameterBinder.cs`.
  - **Result**: `IsCollection` and `ExpandCollectionParameters` refactored to avoid `List<object?>` allocation. Pre-counting and `StringBuilder` used for efficient expansion.

### Phase 2: Execution Engine Refinement
- [x] **Task 4: High-Performance Expression Tree Setters**
  - **Goal**: Use direct casts/specialized conversion methods for primitives.
  - **Target**: `src/Jaunty/Internals/Entity/MetadataCache.cs`.
  - **Result**: `CreateSetter` refactored to use `Convert.ToX` methods and specialized GUID/Enum handling, avoiding generic `Convert.ChangeType` overhead.
- [x] **Task 5: Implement Multi-Entity Mapper Caching**
  - **Goal**: Cache `MultiEntityMapper` strategy based on reader signature.
  - **Target**: `src/Jaunty/Internals/MultiEntityMapper.cs`.
  - **Result**: `MultiEntityMapper` now uses a `ConcurrentDictionary` to cache mapper instances based on the `IDataReader` column signature. It also reuses pre-compiled setters from `MetadataCache<T>`.

### Phase 3: Memory Efficiency & Modernization
- [x] **Task 6: Zero-Allocation SQL Parameter Parsing (.NET 8+)**
  - **Goal**: Use `ReadOnlySpan<char>` for parsing on modern frameworks.
  - **Target**: `src/Jaunty/Internals/Parameters/SqlParameterParser.cs`.
  - **Result**: Refactored to use `ReadOnlySpan<char>` for efficient parsing on .NET 8+, while maintaining compatibility and behavioral requirements (returning all occurrences for deduping later).
- [x] **Task 7: Cache Column-to-Property Mapping Signatures**
  - **Goal**: Reuse `PropertySetter<T>[]` arrays for identical query shapes.
  - **Target**: `src/Jaunty/Internals/Entity/MetadataCache.cs`.
  - **Result**: `GetSetters` now uses a `ConcurrentDictionary` to cache and reuse mapping arrays based on the `IDataReader` column signature and `MappingMode`, significantly reducing per-query array allocations and lookup logic.

## Current Baseline
- **Date**: 2026-02-19
- **Total Tests**: 1376
- **Baseline Failures**: 61 (Pre-existing in branch `dev`)
- **Baseline Warnings**: ~31
