# Feature Specification: Fluent NativeAOT-Safe Metadata Resolution

**Branch**: `003-fluent-nativeaot-metadata`
**Created**: `2026-07-07`
**Status**: Draft

---

## Context / Problem Statement

Torture-test gap #11 (`docs/jaunty-torture-test-gaps-log.md`): every `Jaunty.Fluent` query
(`.From<T>()`, joins, GroupBy, fluent CRUD) throws unless `Jaunty.Extensions.Reflection`'s
`UseReflectionMapping()` has been called, because `FluentMetadataCache.GetMetadata<T>()`
(`src/Jaunty.Fluent/Internals/FluentMetadataCache.cs`) has exactly one metadata source:
`JauntyConfig.ReflectionTableMetadataResolver` (`Func<Type, object>?`), populated only by that
reflection package. This contradicts the constitution's "no runtime reflection in core"
principle for Jaunty's primary, most-advertised API surface.

The gap is avoidable, not fundamental: the source generator (`src/Jaunty.SourceGenerator`)
already emits, per entity implementing `IMapped<T>`, static `ColumnInfo[]` collections
(`InsertColumns`, `UpdateColumns`, `DeleteColumns`, `ParameterMap`) with column name, PK, and
identity flags — all reflection-free, compile-time data. It does not currently emit
`TableName`/`SchemaName` as static members. The plain non-fluent read path (`Query<T>`,
`ReadEntity`) already consumes `IMapped<T>` reflection-free via `MappedCache<T>`
(`src/Jaunty/Internals/Read/MappedCache.cs`). The fluent write path has the same gap and even
an existing acknowledging comment: `CrudSqlCache.TryResolveMetadata<T>()`
(`src/Jaunty/Internals/Write/CrudSqlCache.cs`, ~line 138) only checks
`ReflectionTableMetadataResolver`, with a comment noting `IMapped<T>` could supply metadata
"but for now we'll rely on the extension hook."

**Goal**: fluent queries and fluent CRUD against source-generated entities work with zero
runtime reflection and zero dependency on `Jaunty.Extensions.Reflection`, while leaving that
package's behavior unchanged for hand-written POCOs that don't use the source generator.

---

## 1. User Scenarios & Testing

### User Story 1: Fluent read queries without the reflection package

**Priority**: `P1` (MVP)

**Description**: A developer using only the source generator (no `Jaunty.Extensions.Reflection`
reference, no `UseReflectionMapping()` call anywhere) writes a fluent query against a
`partial` entity class and it works.

**Why This Priority**: This is the core of gap #11 — the fluent API's primary use case is
currently broken for the NativeAOT-safe path the rest of the library promises.

**Independent Test**: Build a minimal console project referencing only `Jaunty` +
`Jaunty.Fluent` + `Jaunty.SourceGenerator` (as Analyzer), no `Jaunty.Extensions.Reflection`;
run `connection.From<T>().Where(...).Select()`; assert it succeeds under `PublishAot=true`.

**Acceptance Scenarios**:
```gherkin
Scenario: Fluent query against source-generated entity, no reflection package
Given a partial entity class with [Table]/[Column]/[Key] attributes, source-generator-processed
And no call to UseReflectionMapping() anywhere in the process
When connection.From<T>().Where(x => x.Prop == value).Select() is called
Then the query executes successfully using metadata sourced entirely from the
  source-generated IMapped<T> implementation, with no reflection call

Scenario: Joins and GroupBy also work reflection-free
Given two source-generated entities
When connection.From<T1>().InnerJoin<T2>().On(...).SelectAll() or .GroupBy(...) is called
Then it succeeds without UseReflectionMapping()
```

### User Story 2: Fluent CRUD without the reflection package

**Priority**: `P1` (MVP)

**Description**: `CrudSqlCache.TryResolveMetadata<T>()` gains the same source-gen-first lookup
already required for User Story 1, closing the specific TODO-style comment in that file.

**Acceptance Scenarios**:
```gherkin
Scenario: Fluent Create/Update/Delete/Upsert without reflection package
Given the same source-generated entity as Story 1
When connection.Create(entity) / .Update(entity) / .Delete(entity) / .Upsert(entity) is called
  via the fluent API
Then it succeeds without UseReflectionMapping()
```

### User Story 3: No regression for non-source-generated POCOs

**Priority**: `P1` (MVP)

**Description**: Entities that are hand-written POCOs — not `partial`, not source-generator
processed, no `IMapped<T>` — continue to require `Jaunty.Extensions.Reflection` and
`UseReflectionMapping()` exactly as today. This story exists to make explicit that Story 1/2
must not regress the existing reflection path.

**Acceptance Scenarios**:
```gherkin
Scenario: Existing reflection-based fluent usage unaffected
Given a non-source-generated POCO entity and UseReflectionMapping() called
When any existing fluent query or CRUD operation runs
Then behavior and SQL output are identical to current behavior (no regression)
```

### User Story 4: Actionable failure when neither path is available

**Priority**: `P2`

**Description**: If a fluent query targets a type with neither source-generated metadata nor a
registered reflection resolver, the resulting exception explains both remedies.

**Acceptance Scenarios**:
```gherkin
Scenario: Clear error when no metadata source is available
Given a type with no IMapped<T> implementation and no ReflectionTableMetadataResolver set
When a fluent query against that type is attempted
Then an InvalidOperationException is thrown naming both remedies: use the Jaunty source
  generator, or call Jaunty.Extensions.Reflection's UseReflectionMapping()
```

### Edge Cases

- Entity implements `IMapped<T>` from a source-generator version older than this feature
  (generated code lacks the new static table-metadata members) — must not silently misbehave;
  resolve explicitly in `plan.md` (compile error vs. graceful fallback to reflection resolver).
- Schema-qualified table names (`[Table("dbo.Something")]` or a separate schema attribute) must
  round-trip through the new static metadata surface unchanged.

---

## 2. Requirements

### Functional Requirements

- `FR-001`: Source generator emits additional static, reflection-free metadata per entity
  (table name, nullable schema name, primary-key column names) alongside the already-emitted
  `InsertColumns`/`UpdateColumns`/`DeleteColumns`/`ParameterMap` `ColumnInfo` collections.
- `FR-002`: `FluentMetadataCache.GetMetadata<T>()` checks for source-generated metadata first,
  synthesizing an `EntityMetadata` instance from it with no reflection call, before falling
  back to `JauntyConfig.ReflectionTableMetadataResolver`.
- `FR-003`: `CrudSqlCache.TryResolveMetadata<T>()` gains the identical source-gen-first lookup,
  resolving the existing acknowledging code comment.
- `FR-004`: Zero behavior change for entities without source-generated metadata — the
  reflection resolver remains the fallback; `Jaunty.Extensions.Reflection` itself is untouched.
- `FR-005`: A new or updated NativeAOT sample project (alongside `NativeAOT-Basic`,
  `NativeAOT-CustomMapper`, `NativeAOT-WithReflection`) demonstrates a fluent query — not just
  `Query<T>` — compiling and running under `PublishAot=true` with no reference to
  `Jaunty.Extensions.Reflection`.
- `FR-006`: The failure path in User Story 4 produces a specific, actionable message (not the
  current generic throw).
- `FR-007`: Existing `Jaunty.Fluent.Tests` / `Jaunty.Tests` integration suites remain green,
  unmodified in expected behavior — this is a metadata-resolution mechanism change, not a
  SQL-generation change, across all 4 dialects.

### Key Entities

| Entity | Description | Key Attributes |
|--------|-------------|-----------------|
| `EntityMetadata` | Existing internal metadata shape consumed by fluent SQL builders | TableName, SchemaName, Columns, PrimaryKeys, InsertColumns, UpdateColumns, DeleteColumns, ParameterMap |
| `ColumnMetadata` | Existing per-column metadata; currently carries a `PropertyInfo` | ColumnName, IsPrimaryKey, IsIdentity, IsComputed, Property |
| New source-gen metadata surface | Static, compile-time-emitted table identity (name/schema/PK names), name TBD in `plan.md` | TableName, SchemaName, PrimaryKeyColumnNames |

---

## 3. Success Criteria

- `SC-1`: A fluent query against a source-generated entity succeeds under
  `dotnet publish -p:PublishAot=true`, with no reflection-related AOT warnings and no
  reference to `Jaunty.Extensions.Reflection` in the consuming project.
- `SC-2`: Existing `Jaunty.Fluent.Tests` and `Jaunty.Tests` suites remain 100% green.
- `SC-3`: `CrudSqlCache`'s existing reflection-only gap (see comment referenced above) is
  closed — fluent `Create`/`Update`/`Delete`/`Upsert` work reflection-free for
  source-generated entities.
- `SC-4`: `samples/torture-test-sakila-queries/` can drop its `Jaunty.Extensions.Reflection`
  reference and `UseReflectionMapping()` call and still pass all 15 queries against all 5
  dialects — this closes gap #11 concretely, not only in the abstract.

---

## 4. Non-Goals / Out of Scope

- Redesigning or removing `Jaunty.Extensions.Reflection` — still required and unchanged for
  non-source-generated POCOs.
- Any change to SQL generation, dialect translation, or query semantics — this is purely about
  *how* metadata is resolved, not what queries produce.
- Retroactively regenerating already-built generator output in consumer projects outside this
  repo (their own rebuild picks up the new generator version).

---

## 5. Design Notes (Planning Phase)

1. **New interface shape**: extend `IMapped<T>` itself with the new static members, vs. a
   separate marker interface (e.g. `ITableMetadata<T>`) implemented alongside it. A separate
   interface is likely safer — avoids a breaking static-member addition to every existing
   `IMapped<T>` consumer and can be adopted incrementally. Needs an explicit decision in
   `plan.md`.
2. **The `ColumnMetadata.Property` problem**: `EntityMetadata`/`ColumnMetadata`
   (`src/Jaunty/Internals/Entity/ColumnMetadata.cs`) currently carries a `PropertyInfo` per
   column — itself something reflection normally supplies. If the goal is *actually* zero
   reflection on the source-gen path (not just zero calls to `UseReflectionMapping()`), this is
   the trickiest point and must be resolved explicitly in `plan.md`, not glossed over: either
   `ColumnMetadata` gains a reflection-free variant, or `PropertyInfo` access is deferred/avoided
   entirely on the synthesized-from-source-gen path.
3. **Caching**: `FluentMetadataCache` already has a `ConcurrentDictionary<Type, EntityMetadata>`
   tier — the new source-gen lookup only changes what populates it on a cache miss.
4. **Older-generator-version edge case** (see Edge Cases above): resolve explicitly in
   `plan.md` whether an `IMapped<T>` without the new static members is a compile error, a
   silent fallback to the reflection resolver, or something else.

---

## Section Summary

| Section | Required | Purpose |
|---------|----------|---------|
| User Scenarios | Yes | User stories + acceptance |
| Requirements | Yes | Functional capabilities |
| Success Criteria | Yes | Measurable outcomes |

---

**Next**: `plan.md` — implementation plan resolving the `ColumnMetadata`/`PropertyInfo`
question above.
