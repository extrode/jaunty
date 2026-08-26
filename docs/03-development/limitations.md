# Jaunty Known Limitations

Last reviewed: 2026-03-03

This file tracks currently known product limitations that impact API completeness, consistency, or production adoption.

## High Priority

### 1. Source-generated ordinal caching is not schema-shape aware
- Risk: incorrect column mapping when the same entity type is read with different column order/shape across executions.
- Scope: generated `IMapped<T>` path.
- Status: active fix in `P0` roadmap.

### 2. Async API requires `DbConnection` in most methods
- Sync API is `IDbConnection`-based, async API is mostly `DbConnection`-based.
- Impact: inconsistent ergonomics for consumers using interface-only abstractions.

### 3. Reflection still exists in core assembly paths
- Runtime mapper/binder resolution and extension bootstrap still use reflection in `Jaunty` core.
- Impact: reduces confidence in strict NativeAOT/zero-reflection claims.

## Medium Priority

### 4. Fluent API parity gaps
- `IJoinedQuery3<T1,T2,T3>` is missing async/paging/sorting parity with 2-way join flows.
- Some distinct/CTE method families are still inconsistent.

### 5. Scaffolding lacks navigation property generation
- Foreign key metadata is discovered but not fully emitted as navigation properties.

### 6. Benchmark comparability quality is uneven
- Some benchmark result sets include incomplete competitor rows.
- Impact: weakens external performance claims unless clearly scoped.

## Low Priority / Design Tradeoffs

### 7. Provider-specific scalar type differences
- `COUNT(*)` type differs by provider (`int` vs `long`).
- Workaround: use `long` for cross-provider scalar counts.

### 8. `IMapped<T>.ReadEntity` is strict-shape oriented
- Custom mapper assumes required columns exist.
- Workaround: use `QueryPartial*` with projection-safe mapper for partial shapes.

## Tracking
- Active roadmap: [../06-releases/tasklists/production-readiness-tasklist.md](../06-releases/tasklists/production-readiness-tasklist.md)
- Issues: https://github.com/extrode/jaunty/issues
