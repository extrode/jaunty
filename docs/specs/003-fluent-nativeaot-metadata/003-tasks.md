# Tasks: Fluent NativeAOT-Safe Metadata Resolution

**Input**: `docs/specs/003-fluent-nativeaot-metadata/`
**Prerequisites**: `plan.md` (required), `spec.md` (required)

---

## Format

```
[ID] [P] [Story] Description
```

- `[ID]`: Task identifier (T001, T002, ...)
- `[P]`: Parallel-capable (different files, no dependencies)
- `[Story]`: User story mapping (US1–US4)

---

## Phase 1: Foundational (Blocks User Stories)

- **T001**: Source generator (`JauntyGenerator.cs`) emits `TableName` (string), `SchemaName`
  (string?), `PrimaryKeyColumnNames` (string[]) as public static properties on generated
  entities, alongside existing `ColumnInfo` collections. Add/update
  `Jaunty.SourceGenerator.Tests` snapshot or emission test.
- **T002** (done 2026-07-07): Audited every consumer of `ColumnMetadata.Property`
  (`PropertyInfo`) codebase-wide (broader than the two folders originally scoped —
  `EntityDataReader.cs` under `BulkCopy/` was also a consumer). Found 4 usage categories (A:
  name-only lookup, ~18 sites; B: uncached value get/set, 3 sites; C: cached compiled-getter
  construction, 2 sites — empirically verified NativeAOT-safe via a scratch `PublishAot`
  project; D: type-only, 1 site). Decision written into `plan.md` Key Design Decision 2: make
  `Property` nullable, hoist `PropertyName`/`PropertyType` to always-populated fields (kills
  Category A/D's `PropertyInfo` dependency outright), add `Getter`/`Setter` delegate fields
  populated by real emitted C# lambdas on the source-gen path (no `Expression.Compile()`
  involved), with Category B/C consumers preferring `Getter`/`Setter` and falling back to
  `Property!.GetValue`/`SetValue`.
- **T003**: Implement the `ColumnMetadata` change from T002: add `PropertyName` (string),
  `PropertyType` (Type), `Getter`/`Setter` (nullable delegate) fields; make `Property` nullable.
  Update every consumer identified in T002's audit: migrate Category A (~18 call sites) from
  `.Property.Name` to `.PropertyName`; migrate Category D (`EntityDataReader.cs`) from
  `.Property.PropertyType` to `.PropertyType`; update Category B/C (`Upsert.cs`,
  `InsertBuilder.cs`, `JoinedQueryBuilder.cs`, `WriteParameterCache.cs`,
  `MultiRowInsertCache.cs`) to prefer `Getter`/`Setter` over `Property!.GetValue`/`SetValue`.
  `MetadataBuilder.Build<T>()` (reflection path) populates `PropertyName`/`PropertyType` from
  the `PropertyInfo` it already has; leaves `Getter`/`Setter` null for now.

**Checkpoint**: Foundation ready — source-gen emits table identity; `ColumnMetadata` shape
finalized for the reflection-free path.

---

## Phase 2: User Story 1 (P1) MVP — Fluent read queries without reflection package

**Goal**: `connection.From<T>()...` works against source-generated entities with zero calls to
`UseReflectionMapping()`.

**Implementation**:
- **T004** [US1] Add source-gen-first lookup tier to `FluentMetadataCache.GetMetadata<T>()`
  (`src/Jaunty.Fluent/Internals/FluentMetadataCache.cs`): reflect for the T001 static members
  once per type, synthesize `EntityMetadata`, cache; fall through to
  `ReflectionTableMetadataResolver` if not found.
- **T005** [P] [US1] Unit tests: `FluentMetadataCache` resolves a source-generated entity with
  no `ReflectionTableMetadataResolver` registered.
- **T006** [P] [US1] Integration test (`Jaunty.Fluent.Tests`): full fluent query
  (`.From<T>().Where().Select()`, a 2-way join, a `GroupBy`) against a source-generated entity,
  asserting success with no `UseReflectionMapping()` call anywhere in the test process.

**Checkpoint**: US1 complete + tested.

---

## Phase 3: User Story 2 (P1) MVP — Fluent CRUD without reflection package

**Goal**: fluent `Create`/`Update`/`Delete`/`Upsert` work against source-generated entities
with zero calls to `UseReflectionMapping()`.

**Implementation**:
- **T007** [US2] Add the identical source-gen-first lookup tier to
  `CrudSqlCache.TryResolveMetadata<T>()` (`src/Jaunty/Internals/Write/CrudSqlCache.cs`),
  resolving the existing acknowledging code comment there.
- **T008** [P] [US2] Unit tests: `CrudSqlCache` resolves a source-generated entity with no
  `ReflectionTableMetadataResolver` registered.
- **T009** [P] [US2] Integration test: fluent `Create`/`Update`/`Delete`/`Upsert` round-trip
  against a source-generated entity, no `UseReflectionMapping()`, all 4 real dialects.

**Checkpoint**: US2 complete + tested.

---

## Phase 4: User Story 3 (P1) — No regression for existing reflection path

**Goal**: confirm zero behavior change for non-source-generated POCOs still using
`UseReflectionMapping()`.

**Implementation**:
- **T010** [US3] Re-run full existing `Jaunty.Fluent.Tests` and `Jaunty.Tests` suites
  unmodified; confirm 100% pass, no SQL-output diffs, across all 4 real dialects + SQLite.

**Checkpoint**: US3 complete — no regression confirmed.

---

## Phase 5: User Story 4 (P2) — Actionable failure message

**Implementation**:
- **T011** [US4] Update the no-metadata-found exception in `FluentMetadataCache`/
  `CrudSqlCache` to name both remedies (source generator vs. `UseReflectionMapping()`).
- **T012** [P] [US4] Unit test asserting the new message content for a type with neither
  metadata source available.

---

## Phase 6: NativeAOT Proof & Torture-Test Closure

- **T013**: New sample `samples/NativeAOT-FluentQuery/` — fluent query (not `Query<T>`)
  compiling and running under `PublishAot=true`, no `Jaunty.Extensions.Reflection` reference.
  `dotnet publish -p:PublishAot=true`, confirm no reflection-related AOT warnings (SC-1).
- **T014**: Update `samples/torture-test-sakila-queries/` — drop `Jaunty.Extensions.Reflection`
  project reference and `UseReflectionMapping()` call; re-run all 15 queries against all 5
  dialects, confirm identical results to the Part 2 baseline (SC-4).
- **T015**: Update `docs/jaunty-torture-test-gaps-log.md` gap #11 status to "fixed", pointing
  at this branch/spec.

---

## Phase 7: Polish

- XML docs on all new public surface.
- `dotnet test` full solution, all dialects.
- Validate against every `spec.md` acceptance scenario and success criterion explicitly.

---

## Execution Order

1. Phase 1 (Foundational) — must complete first, including the T002 audit decision.
2. Phase 2 (US1) and Phase 3 (US2) — can proceed in parallel once Phase 1 is done (different
   files: `FluentMetadataCache.cs` vs. `CrudSqlCache.cs`).
3. Phase 4 (US3 regression check) — after Phase 2 + 3.
4. Phase 5 (US4) — after Phase 2 + 3 (touches the same two files' error paths).
5. Phase 6 (NativeAOT proof + torture-test closure) — after all user stories pass.
6. Phase 7 (Polish) — last.

**Parallel**: All `[P]` tasks within a phase can run concurrently.

---

**Commit**: After each task | **Test**: `dotnet test` (+ `dotnet publish -p:PublishAot=true`
for Phase 6)
