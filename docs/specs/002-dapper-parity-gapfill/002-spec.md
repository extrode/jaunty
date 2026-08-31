# Feature Specification: Micro-ORM Parity Gap-fill

**Branch**: `002-dapper-parity-gapfill`  
**Created**: `2026-07-03`  
**Status**: Draft  

---

## Context / Problem Statement

Jaunty is a .NET micro-ORM supporting SQL Server, PostgreSQL, MySQL, and SQLite with netstandard2.0 + net8.0 targeting, source-generated mappers with reflection fallback, strict/partial mapping modes, and an interceptor pipeline. A verified gap analysis against Dapper and Dapper.Contrib identified missing capabilities.

**Goal**: Capability parity with Dapper and Dapper.Contrib via Jaunty-style APIs (CommandOptions, ValueTask async with CancellationToken, strict/partial mapping, sync+async duality).

---

## 1. User Scenarios & Testing

### User Story 1: Execute Arbitrary Non-Query SQL

**Priority**: `P1` (MVP)

Execute INSERT, UPDATE, DELETE, DDL with parameters, transactions, timeouts, cancellation, and batch operations.

**Acceptance Scenarios**:
```gherkin
Scenario: Execute single statement
Given a SQL statement
When Execute(sql, optionalParameters, optionalOptions) called
Then statement runs and returns rows affected

Scenario: Batch execute with reuse
Given IEnumerable<object> parameter sets
When Execute(sql, paramSets, options) called
Then statement executed per element with prepared-command reuse, cumulative rows returned
```

### User Story 2: Key-based Entity Reads

**Priority**: `P1` (MVP)

Fetch by PK or retrieve all rows with generated SQL cached per type per dialect.

**Acceptance Scenarios**:
```gherkin
Scenario: Get by key
Given entity with [Key] or Id convention
When Get<T>(keyValue) called
Then matching row returned or null (GetOrDefault semantics)

Scenario: GetAll
Given GetAll<T>() or GetAllAsync<T>()
Then all rows returned as List<T>, SQL cached

Scenario: Stream for large tables
Given GetAllStream<T>() / GetAllStreamAsync<T>()
Then rows yielded one at a time as IAsyncEnumerable<T>
```

### User Story 3: Multi-entity Mapping (Arity 2–7)

**Priority**: `P1` (MVP)

Extend existing multi-table mapper (QueryMultiEntity) from arity 2 to arity 3–7 with explicit boundary control.

**Acceptance Scenarios**:
```gherkin
Scenario: 2-entity join
Given Query<T1, T2, TResult>(sql, map, options)
When executed
Then result split into T1 and T2, passed to map, results returned as List<TResult>

Scenario: Higher arities
Given Query<T1, T2, T3, TResult>, ..., Query<T1..T7, TResult>
When executed
Then entities split and mapped with explicit boundary control via mapper convention
```

### User Story 4: Type Handlers and Enum Storage

**Priority**: `P1` (MVP)

Custom value converters for JSON columns, value objects, enums with round-trip serialization.

**Acceptance Scenarios**:
```gherkin
Scenario: Type handler registration
Given RegisterTypeHandler<T>(...) on JauntyConfig
When querying/writing entities with property of type T
Then handler converts in both directions

Scenario: Enum storage strategy
Given global JauntyConfig enum strategy (default Numeric)
When set to String
Then enums written as names, parsed case-insensitively
```

### User Story 5 (P2): Ad-hoc Query Ergonomics

Untyped/dynamic queries beyond QueryPartialList; parameter bags with output/return params.

### User Story 6 (P3): Composite Primary Keys

Multiple [Key] properties; Get/Update/Delete/Upsert generate compound WHERE.

### User Story 7: API Documentation Site

**Priority**: `P1` (MVP)

As a developer I can browse Jaunty's full API documentation as a static HTML site.

**Acceptance Scenarios**:
```gherkin
Scenario: Static HTML documentation
Given API documentation site built with semantic HTML5 and plain CSS
When visiting docs/index.html offline
Then index provides navigable entry points to all public API areas

Scenario: Complete API coverage
Given Execute, Get, GetAll, QueryMultiEntity, TypeHandlers, EnumStorage, attributes, dialects, and configuration areas
When browsing documentation
Then every public method and property appears with signature and at least one code example
```

---

## 2. Requirements

### Functional Requirements

- **FR-001**: Execute(sql), Execute(sql, parameters), Execute(sql, parameters, options) on IDbConnection returning int; ExecuteAsync equivalents returning ValueTask<int> with CancellationToken.

- **FR-002**: Batch Execute accepting IEnumerable<object> parameter sets, prepared-command reuse, cumulative rows affected.

- **FR-003**: Get<T>(id) / GetOrDefault + Get<T, TId>(id) typed-key overloads; Get throwing variant (QueryFirst semantics); sync + async.

- **FR-004**: GetAll<T>() / GetAllAsync<T>() List<T> / ValueTask<List<T>>; streaming variant GetAllStream / IAsyncEnumerable<T>; cached SELECT per type/dialect.

- **FR-005**: Query<T1..T7, TResult>(sql, map, options) arities 2–7; sync + async; List<TResult> / ValueTask<List<TResult>>.

- **FR-006**: Extend existing multi-table mapper (QueryMultiEntity) from arity 2 to arity 3–7 (sync+async, strict+partial). Analyze use cases the current mapper cannot handle vs Dapper splitOn: joined result sets with non-Id boundary columns, duplicate column names across entities, entities without key columns in projection.

- **FR-007**: Public type-handler registration (JauntyConfig.RegisterTypeHandler<T>(Func<object, T> fromDb, Func<T, object?> toDb)) with optional abstract class API TypeHandler<T> (Parse/SetValue) as secondary supported variant; applied in result mapping (source-gen + reflection) and entity binding.

- **FR-008**: Enum storage — global JauntyConfig setting (default Numeric) + per-property [EnumStorage(String | Numeric)]; string round-trips by name, case-insensitive.

- **FR-009 (P2)**: Untyped/dynamic API (QueryDynamic, QueryPartialDynamic) supporting indexer + member access.

- **FR-010 (P2)**: Parameter bag (Execute/Query CommandType.Text) with input/output/return/return-value params; post-execution retrieval.

- **FR-011 (P2)**: SQL Server TVP — DataTable / IEnumerable<T> as SqlDbType.Structured with type name.

- **FR-012 (P3)**: Composite [Key] across CRUD (Get, Update, Delete, Upsert); overloads for multiple key values; AND compound WHERE.

- **FR-013**: All new APIs sync + async, interceptor pipeline participation, CommandOptions (transaction/timeout), all four dialects (except FR-011 SQL Server only).

- **FR-014**: NativeAOT compatibility — no unavoidable core runtime reflection; reflection parts in Jaunty.Extensions.Reflection or source generator; PublicAPI updated.

- **FR-015**: XML docs (summary, param, returns, remarks, examples) for all new public APIs; CI tests docs.

- **FR-016**: Unit tests (happy path + edge cases); integration tests all four dialects in CI.

- **FR-017**: Static API documentation site in docs/ at repo root with semantic HTML5 + plain CSS (no JavaScript frameworks, no CSS frameworks, minimal JavaScript); hand-authored pages covering all public API areas (Execute, Get, GetAll, QueryMultiEntity arities 2-7, TypeHandlers, EnumStorage, Attributes, Dialects, Configuration); navigable index; works offline from file system.

---

## 3. Success Criteria

- **SC-1**: Execute overhead within 5% of Dapper Execute (benchmark vs. Dapper 2.1.15+).

- **SC-2**: Batch execute 1,000 parameter sets >= 5x faster than naive loop; prepared reuse verified.

- **SC-3**: Get<T> / GetAll<T> within 5% of hand-written Query<T> across all four dialects; caching verified.

- **SC-4**: 100% of new public APIs have XML docs + unit tests; integration tests pass all dialects.

- **SC-5**: No existing benchmark regression (>2% slowdown fails).

- **SC-6**: Type handler round-trip correctness: JSON POCO, Guid-as-string, custom value object, enum-as-string; all dialects.

- **SC-7**: Multi-entity Query correctness: 1-to-many, N-to-M join splits/maps correctly; all dialects.

- **SC-8**: Composite key compound WHERE generation; Get/Update/Delete/Upsert participate.

- **SC-9**: Every public extension method appears on API documentation site with at least one code example.

## 3. Success Criteria

- **SC-1**: Execute overhead within 5% of Dapper Execute (benchmark vs. Dapper 2.1.15+).

- **SC-2**: Batch execute 1,000 parameter sets >= 5x faster than naive loop; prepared reuse verified.

- **SC-3**: Get<T> / GetAll<T> within 5% of hand-written Query<T> across all four dialects; caching verified.

- **SC-4**: 100% of new public APIs have XML docs + unit tests; integration tests pass all dialects.

- **SC-5**: No existing benchmark regression (>2% slowdown fails).

- **SC-6**: Type handler round-trip correctness: JSON POCO, Guid-as-string, custom value object, enum-as-string; all dialects.

- **SC-7**: Multi-entity Query correctness: 1-to-many, N-to-M join splits/maps correctly; all dialects.

- **SC-8**: Composite key compound WHERE generation; Get/Update/Delete/Upsert participate.

---

## 4. Non-Goals / Out of Scope

- Dapper API signature verbatim copy (capability parity only).
- RepoDb paging / Count / Exists.
- Dapper literal replacement ({=value}).
- LINQ provider or change tracking.
- Automatic FK navigation.

---

## 5. Design Notes (Planning Phase)

1. **Multi-entity mapper extension**: Extend QueryMultiEntity from arity 2 to 3–7. Analysis task identifies gaps vs Dapper splitOn (non-Id boundaries, duplicate columns, entities without key columns in projection) and closes them within the mapper's convention.
2. **Type handler API**: Interface ITypeHandler<T> vs. Func pair.
3. **Enum mismatch**: Throw, default to 0, or null.
4. **Composite key overloads**: Up to 7 keys via value tuples or explicit params.
5. **Dynamic row API**: IDynamicRow or ExpandoObject-like wrapper.
6. **Parameter bag**: New Jaunty API vs. existing DynamicParameters compat.

---

**Next**: Implementation plan + detailed API design.
