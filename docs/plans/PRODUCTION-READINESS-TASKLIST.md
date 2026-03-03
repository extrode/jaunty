# Production Readiness Tasklist

Last updated: 2026-03-03

This is the active execution plan. Priorities are ordered by production risk.

## P0 - Correctness and Trust

### PRD-001: Make generated ordinal caching schema-safe
- Priority: `P0`
- Status: `In Progress (implementation + regression tests added; runtime validation pending)`
- Scope:
  - Update source generator to avoid stale ordinal reuse across different result shapes.
  - Add regression coverage for shape/order changes.
- Acceptance criteria:
  - Same entity queried with different column orders maps correctly.
  - Same entity queried with different column subsets fails/passes according to strict/partial rules, not stale cache.
  - No mapping corruption in multi-result-set flows.
- Estimate: 2-4 days

### PRD-002: Benchmark result hygiene
- Priority: `P0`
- Status: `Planned`
- Scope:
  - Standardize benchmark matrix and annotate unsupported scenarios.
  - Remove ambiguous summary claims not backed by current artifacts.
- Acceptance criteria:
  - Reproducible benchmark command set.
  - Report tables with explicit supported/unsupported markers.
- Estimate: 2-3 days

## P1 - API Consistency and AOT Integrity

### PRD-003: Async API consistency pass
- Priority: `P1`
- Status: `Planned`
- Scope: align `IDbConnection`/`DbConnection` behavior and docs for async methods.
- Acceptance criteria: no surprise runtime throws for documented async pathways.
- Estimate: 3-5 days

### PRD-004: Remove reflection from core hot/runtime paths
- Priority: `P1`
- Status: `Planned`
- Scope: reduce reflection in mapper/binder resolution and startup extension bootstrap.
- Acceptance criteria: AOT verification passes with documented reflection boundaries.
- Estimate: 4-7 days

### PRD-005: Sync/async query parity tuning
- Priority: `P1`
- Status: `Planned`
- Scope: align async list sizing and row count hints with sync path.
- Acceptance criteria: async path uses equivalent capacity strategy and avoids unnecessary reallocations.
- Estimate: 1-2 days

### PRD-006: Upsert hot-path optimization
- Priority: `P1`
- Status: `Planned`
- Scope: replace per-property reflection reads with cached compiled getters.
- Acceptance criteria: lower allocations/CPU in upsert microbenchmarks.
- Estimate: 2-4 days

## P2 - Feature Completeness

### PRD-007: Fluent 3-way join parity
- Priority: `P2`
- Status: `Planned`
- Scope: async/order/paging parity for `IJoinedQuery3<>`.
- Estimate: 3-5 days

### PRD-008: CTE async method completion
- Priority: `P2`
- Status: `Planned`
- Scope: add missing async first/first-or-default CTE methods.
- Estimate: 1-2 days

### PRD-009: Scaffolding navigation properties
- Priority: `P2`
- Status: `Planned`
- Scope: generate opt-in reference/collection navigation properties from FK metadata.
- Estimate: 4-7 days

## P3 - Production Ecosystem

### PRD-010: Interception and observability hooks
- Priority: `P3`
- Status: `Planned`
- Scope: command lifecycle hooks + diagnostic integration.
- Estimate: 5-8 days

### PRD-011: Retry/resilience abstraction
- Priority: `P3`
- Status: `Planned`
- Scope: optional transient retry policy integration.
- Estimate: 3-5 days

## Work Log

- 2026-03-03: Created readiness report and active prioritized tasklist.
- 2026-03-03: Started `PRD-001` implementation (schema-safe generated ordinal cache).
- 2026-03-03: Added source-generator regression tests for reordered columns across reader instances and result sets.
