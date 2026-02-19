# Optimization Progress Checklist

This checklist tracks the implementation of performance and memory optimizations for Jaunty. 

## Roadmap & Progress

### Phase 1: Hybrid Fast-Path Foundation
- [x] **Task 1: Compiled Write Binders (Tight Loop)**
  - **Goal**: Zero-reflection parameter binding for INSERT/UPDATE.
  - **Result**: `WriteParameterCache<T>` implemented with optimized array-based iteration.
- [x] **Task 2: Zero-Boxing FastSetters**
  - **Goal**: Use `DbDataReader.GetFieldValue<T>` for direct mapping.
  - **Result**: `MetadataCache<T>` updated with `FastSetter` and `PropertySetter.SetFast`.
- [ ] **Task 3: Execution Core Fast-Path Branching**
  - **Goal**: Detect `DbConnection` and switch to `DbCommand`/`DbDataReader` flows.
  - **Target**: `QueryCore.cs`, `ExecuteReader.cs`.

### Phase 2: Parameter Template System
- [ ] **Task 4: Parameter Template Cloning**
  - **Goal**: Avoid `CreateParameter` and `Add` calls in loops.
  - **Target**: `WriteParameterCache<T>`, `ParameterBinder.cs`.

## Current Baseline
- **Date**: 2026-02-19
- **Total Tests**: 1376
- **Baseline Failures**: 61 (Pre-existing in branch `dev`)
- **Baseline Warnings**: ~31
