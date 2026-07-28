# Implementation Plan: Micro-ORM Parity Gap-fill

**Branch**: `002-dapper-parity-gapfill`
**Date**: 2026-07-03
**Spec**: [spec.md](spec.md)
**Input**: `/specs/002-dapper-parity-gapfill/spec.md`

---

## Summary

**Primary Requirement**: Achieve capability parity with Dapper and Dapper.Contrib through Jaunty-style APIs supporting Execute (arbitrary non-query), Get/GetAll (key-based entity reads), multi-entity mapping (arity 2-7), type handlers with custom value converters, and enum storage strategy configuration.

**Technical Approach**:
1. Implement type-handler infrastructure (ITypeHandler/TypeHandler<T>, registry in JauntyConfig, read/write path integration)
2. Add enum storage strategy (global default + per-property [EnumStorage] attribute) in mappers and parameter binding
3. Implement Execute/ExecuteAsync/ExecuteBatch for arbitrary SQL with parameter binding and prepared-command reuse
4. Implement Get<T>/GetStrict<T> and GetAll<T>/GetAllStreamAsync with cached per-dialect SELECT generation
5. Extend multi-entity mapping (QueryMultiEntity) from arity 2 to arity 7 with explicit boundary control
6. Phase 2: Add untyped row types (JauntyRow), parameter bags (JauntyParameters with output/return params), SQL Server TVP support
7. Phase 3: Support composite [Key] properties across CRUD operations

---

## Technical Context

| Field | Value |
|-------|-------|
| **Language** | C# (netstandard2.0, net8.0) |
| **Dependencies** | Zero (core), reflection in extensions |
| **Storage** | SQL Server, PostgreSQL, MySQL, SQLite (4 dialects) |
| **Testing** | xUnit, integration tests on all dialects, benchmarks vs Dapper |
| **Platform** | Cross-platform (.NET), NativeAOT-compatible core |
| **Project Type** | Library |
| **Performance** | Within 5% of Dapper; batch execute 5x faster than loop |
| **Constraints** | NativeAOT core (registry-based type handlers only), no runtime reflection in core |

---

## Constitution Check

- [x] Performance-first upheld (within 5% Dapper, batch 5x faster)
- [x] NativeAOT-compatible (registry-based handlers, no reflection in core)
- [x] Zero dependencies (core), extensions as optional
- [x] Tests before implementation (unit + integration all dialects)

---

## Project Structure

### Documentation (this feature)

```
docs/specs/002-dapper-parity-gapfill/
├── spec.md          # Feature spec
├── plan.md          # This file
├── tasks.md         # Task list
└── research.md      # Technical research
```

### Source Code (Phase 1-3)

```
src/
├── Jaunty/
│   ├── TypeHandlers/
│   │   ├── ITypeHandler.cs
│   │   └── TypeHandler.cs (abstract base)
│   ├── Execute/
│   │   ├── Execute.cs
│   │   └── ExecuteBatch.cs
│   ├── Read/
│   │   ├── Get.cs
│   │   ├── GetAll.cs
│   │   ├── QueryMultiEntity.cs (extend to arity 7)
│   │   └── JauntyRow.cs (P2)
│   ├── Attributes/
│   │   ├── EnumStorageAttribute.cs
│   │   └── KeyAttribute.cs (update docs)
│   ├── Core/
│   │   ├── CommandOptions.cs (no change)
│   │   └── JauntyParameters.cs (P2)

5. **Multi-entity mapper extension**: Extend QueryMultiEntity from arity 2 to 3–7. Analysis task identifies gaps vs Dapper splitOn (non-Id boundaries, duplicate columns, entities without key columns in projection) and closes them within the mapper's convention.

6. **Type handler API**: Delegate-first registration (Func<object, T> fromDb, Func<T, object?> toDb); optional TypeHandler<T> abstract base for structured handler implementations, wraps internally to same registry.

7. **Enum mismatch**: Throw, default to 0, or null.

8. **Composite key overloads**: Up to 7 keys via value tuples or explicit params.

9. **Dynamic row API**: IDynamicRow or ExpandoObject-like wrapper.

10. **Parameter bag**: New Jaunty API vs. existing DynamicParameters compat.

11. **API documentation**: Hand-authored HTML pages in docs/ folder (index.html, styles.css, area pages like docs/execute.html, docs/get.html, docs/querymulti.html, docs/type-handlers.html, etc.); single shared stylesheet; semantic HTML5 only (no React, Tailwind, no JS frameworks).

    └── RegistryEmitter.cs (emit handler registry check)

tests/
└── Jaunty.Tests/
    ├── Execute/
    │   ├── ExecuteTests.cs
    │   ├── ExecuteBatchTests.cs
    │   └── ExecuteIntegrationTests.cs (all 4 dialects)
    ├── Read/
    │   ├── GetTests.cs
    │   ├── GetAllTests.cs
    │   ├── QueryMultiEntityTests.cs (arity 2-7, split control)
    │   └── QueryMultiEntityIntegrationTests.cs
    ├── TypeHandlers/
    │   ├── TypeHandlerRegistryTests.cs
    │   ├── TypeHandlerIntegrationTests.cs (JSON, Guid-as-string, value object)
    │   └── EnumStorageTests.cs (round-trip string/numeric, all dialects)
    └── Benchmarks/
        ├── ExecuteBenchmarks.cs (vs Dapper)
        └── GetBenchmarks.cs
```

---

## Implementation Phases

### Phase 1: Foundational (Blocks All User Stories)
- ITypeHandler/TypeHandler<T> and registry in JauntyConfig
- ParameterBinder integration with type handler lookup
- EnumStorage enum, [EnumStorage] attribute, JauntyConfig.DefaultEnumStorage

### Phase 2: MVP User Stories 1-4 (Execute, Get/GetAll, Multi-entity, Type Handlers + Enum)
- Execute/ExecuteAsync with parameter binding and transaction/timeout/logging support
- ExecuteBatch with prepared-command reuse and cumulative rows affected
- Get<T>/GetStrict<T>, GetAll<T> with cached SQL per type/dialect
- Reflection mapper read-path type-handler integration
- Source generator read-path handler awareness and enum handling
- Parameter binding enum storage strategy application

### Phase 3: User Stories 5-6 P2/P3 Features
- JauntyRow untyped/dynamic row type
- JauntyParameters with output/return/return-value parameter retrieval
- SQL Server TVP support (DataTable wrapping)
- Composite [Key] support across Get/Update/Delete/Upsert

### Phase 4: Verification & Polish
- Benchmark suite vs Dapper (Execute, batch execute, Get)
- XML documentation on all new public APIs
- Integration test pass on all 4 dialects
- README updates with Dapper comparison table
- Backward compatibility verification

---

## Key Design Decisions

1. **Type Handler Registry**: Dictionary<Type, ITypeHandler> in JauntyConfig, checked before built-in conversions; zero reflection in core path.

