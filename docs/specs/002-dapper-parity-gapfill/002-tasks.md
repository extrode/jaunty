# Tasks: Micro-ORM Parity Gap-fill

**Input**: `docs/specs/002-dapper-parity-gapfill/`
**Prerequisites**: `plan.md` (required), `spec.md` (required)

## Format

[ID] [P] [Story] Description

## Phase 1: Foundational Setup (Blocks All User Stories)

- T001: [P] Create TypeHandlers/ folder structure in src/Jaunty/
- T002: [P] Create Execute/ folder in src/Jaunty/
- T003: [P] Create Attributes/EnumStorageAttribute.cs with EnumStorage enum and attribute definition
- T004: [US3] Analyze existing multi-table mapper (QueryMultiEntity) vs Dapper splitOn use cases (non-Id boundary columns, duplicate column names, keyless projections); design gap-fill within Jaunty's own multi-table mapper conventions — no Split API, no Split.cs type
- T005: Create ITypeHandler interface and TypeHandler<T> abstract base class in TypeHandlers/
- T006: Add JauntyConfig.RegisterTypeHandler<T>() and type handler registry (ConcurrentDictionary<Type, ITypeHandler>)
- T007: Add JauntyConfig.DefaultEnumStorage property (default: EnumStorage.Numeric)
- T008: Integrate type-handler lookup in ParameterBinder (write path for custom types)

**Checkpoint**: Foundation complete - type handlers, enum storage, split infrastructure in place
**Checkpoint**: Foundation complete - type handlers, enum storage, split infrastructure in place

---

## Phase 2: US1 - Execute (P1 MVP)

- [x] T009: [US1] Create Execute.cs with Execute(sql, params, options) overloads
- [x] T010: [US1] Implement ExecuteAsync with ValueTask<int>, CancellationToken support
- [x] T011: [US1] Wire interceptor pipeline and logging in Execute path
- [x] T012: [US1] Create ExecuteBatch.cs for IEnumerable parameter sets
- [x] T013: [US1] Implement prepared-command reuse + bind-per-element in ExecuteBatch
- T014: [P] [US1] Unit tests for Execute and ExecuteBatch (ExecuteTests.cs)
- T015: [P] [US1] Integration tests for all 4 dialects (ExecuteIntegrationTests.cs)
- T016: [P] [US1] ExecuteBenchmarks.cs vs Dapper 2.1.15+ (SC-1, SC-2)

**Checkpoint**: US1 complete - Execute and batch execute production-ready

---

## Phase 3: US2 - Get/GetAll (P1 MVP)

- [x] T017: [P] Extend existing CRUD SQL cache to include SELECT-by-key per type/dialect
- [x] T018: [US2] Create Get.cs with Get<T>(id) / GetStrict<T>(id) overloads
- [x] T019: [US2] Implement Get<T, TId>(id) typed-key variant
- [x] T020: [US2] Create GetAll.cs with GetAll<T>() and GetAllAsync<T>()
- [x] T021: [US2] Implement GetAllStream<T>() / GetAllStreamAsync<T>() as IAsyncEnumerable<T>
- [x] T022: [US2] Wire GetAll to cached SELECT generation per dialect
- [x] T023: [P] [US2] Unit tests: Get, GetStrict, GetAll, GetStream (GetTests.cs, GetAllTests.cs)
- [x] T024: [P] [US2] Integration tests all 4 dialects (Get/GetAll with [Key], Id, [Mapper] attributes)
- T025: [P] [US2] GetBenchmarks.cs comparison vs hand-written Query (SC-3)

**Checkpoint**: US2 complete - Key-based entity reads production-ready

---

## Phase 4: US3 - Multi-entity Mapping (P1 MVP)

- T026: Extend QueryMultiEntity.cs arity from 2 to 3, 4, 5, 6, 7 (sync + async)
- T027: [US3] Implement explicit boundary control in multi-entity mapping via mapper convention
- T028: [US3] Implement auto-split default: detect [Key]/Id property names per type
- T029: [US3] Add arity 2-7 with Func<T1, T2, ..., T7, TResult> map parameter
- T030: [P] [US3] Unit tests per arity (2, 3, 5, 7) with custom map functions
- T031: [P] [US3] Integration tests: 1-to-many, N-to-M joins, split boundary correctness (QueryMultiEntityIntegrationTests.cs)
- T032: [P] [US3] Partial-mapping variants for Get, GetAll, QueryMultiEntity (SC-7)

**Checkpoint**: US3 complete - Multi-entity mapping arity 2-7 production-ready

---

## Phase 5: US4 - Type Handlers + Enum Storage (P1 MVP)

- T033: [US4] Integrate type-handler lookup in reflection mapper read path (Jaunty.Extensions.Reflection)
- T034: [US4] Apply EnumStorage strategy (Numeric/String) in reflection mapper for enum properties
- T035: [US4] Update source generator to emit handler registry checks for non-primitive property types
- T036: [US4] Apply EnumStorage in source generator enum handling (string TryParse case-insensitive)
- T037: [US4] Enum round-trip: parse numeric and string values case-insensitively in both mappers
- T038: [P] [US4] Unit tests: type handler round-trip (JSON POCO, Guid-as-string, value object, custom enum)
- T039: [P] [US4] TypeHandlerRegistryTests.cs (lookup, registration, fallback to built-in conversions)
- T040: [P] [US4] EnumStorageTests.cs (Numeric/String round-trip per property, all dialects) (SC-6)
- T041: [P] [US4] Source generator snapshot tests for type-handler-aware mapping

**Checkpoint**: US4 complete - Type handlers and enum storage production-ready

---

## Phase 6: US5 - P2 Ad-hoc Query Ergonomics

- T042: [US5] Create JauntyRow.cs untyped/dynamic row type (IDictionary<string, object?> + IDynamicMetaObjectProvider)
- T043: [US5] Add Query overload returning JauntyRow instead of typed T
- T044: [P] [US5] Create JauntyParameters.cs general parameter bag (input/output/return/return-value)
- T045: [P] [US5] Wire JauntyParameters in Execute/Query for CommandType.Text
- T046: [P] [US5] Post-execution output/return parameter retrieval
- T047: [P] [US5] SQL Server TVP support: TableValuedParameter(typeName, DataTable) wrapper in ParameterBinder

**Checkpoint**: US5 complete - Ad-hoc query and parameter bag ergonomics working

---

## Phase 7: US6 - P3 Composite Primary Keys

- T048: [US6] Update KeyAttribute docs to support multiple [Key] properties per entity
- T049: [US6] Extend CRUD SQL generation (Get/Update/Delete/Upsert) to handle multiple keys with AND compound WHERE
- T050: [US6] Add Get overload variants for 2-7 composite key values (value-tuple or explicit params)
- T051: [P] [US6] Unit + integration tests: composite keys in Get, Update, Delete, Upsert (SC-8)

**Checkpoint**: US6 complete - Composite key support across CRUD

---

## Phase 8: Polish & Verification

- T052: [P] Run full test suite (dotnet test) - all phases complete
- T053: [P] Validate backward compatibility - existing tests pass without config changes
- T054: XML docs on all new public APIs: Execute, ExecuteBatch, Get, GetAll, GetStream, QueryMultiEntity (arities 2-7), TypeHandler, EnumStorage, JauntyRow, JauntyParameters (SC-4)
- T055: Update README with feature comparison table: Execute vs Dapper, Get/GetAll, multi-entity mapping, type handlers, enum storage
- T056: [P] Run full benchmark suite (ExecuteBenchmarks, GetBenchmarks, multi-entity benchmarks) - verify SC-1, SC-2, SC-3, SC-5 criteria
- T057: [P] Verify NativeAOT compat: core package compiles AOT, no reflection in core paths
- T058: Update .PublicAPI.txt with new public types and methods
- T059: Final integration test pass on all 4 dialects (SQL Server, PostgreSQL, MySQL, SQLite)

**Checkpoint**: Feature complete and production-ready

---

## Execution Order

1. **Phase 1 (Foundational)** - T001-T008: Start immediately (T001-T003 parallel)
   - Blocks: All user stories
   - Dependencies: None

2. **Phase 2 (US1 Execute)** - T009-T016: After T008 complete
   - Blocks: Nothing
   - Dependencies: T001-T008

3. **Phase 3 (US2 Get/GetAll)** - T017-T025: After T008 complete, parallel with Phase 2
   - Blocks: Nothing
   - Dependencies: T001-T008

4. **Phase 4 (US3 Multi-entity)** - T026-T032: After T008 complete, parallel with Phase 2-3
   - Blocks: Nothing
   - Dependencies: T001-T008

5. **Phase 5 (US4 Handlers+Enum)** - T033-T041: After T008 complete, parallel with Phase 2-4
   - Blocks: Nothing
   - Dependencies: T001-T008

6. **Phase 6 (US5 P2)** - T042-T047: After T033 (type handlers must work)
   - Blocks: Nothing
   - Dependencies: T001-T008, T033-T041

7. **Phase 7 (US6 P3)** - T048-T051: After T018 (Get must exist)
   - Blocks: Nothing
   - Dependencies: T001-T008, T017-T025

8. **Phase 8 (Polish)** - T052-T059: After all user story phases complete
   - Blocks: Release
   - Dependencies: All T001-T051

---

## Task Summary by Phase

| Phase | Tasks | Dependencies | Parallel |
|-------|-------|--------------|----------|
| 1: Foundational | T001-T008 | None | T001-T003 |
| 2: US1 Execute | T009-T016 | T001-T008 | T014-T016 |
| 3: US2 Get/GetAll | T017-T025 | T001-T008 | T023-T025 |
| 4: US3 Multi-entity | T026-T032 | T001-T008 | T030-T032 |

## Phase 8: API Documentation Site (P1 MVP)

- T052: Create docs/ folder structure at repo root (docs/index.html, docs/styles.css)
- T053: Create docs/styles.css with semantic HTML5-compatible styling (no frameworks)
- T054: [P] Create docs/execute.html with Execute, ExecuteBatch API signatures and examples
- T055: [P] Create docs/get.html with Get, GetStrict, GetAll, GetAllStream signatures and examples
- T056: [P] Create docs/query-multi-entity.html with QueryMultiEntity arity 2-7 signatures and examples
- T057: [P] Create docs/type-handlers.html with TypeHandler, EnumStorage documentation and examples
- T058: [P] Create docs/configuration.html with JauntyConfig, dialect-specific settings
- T059: [P] Create docs/attributes.html with [Key], [EnumStorage], [Mapper] attribute documentation
- T060: [P] Create docs/dialects.html with SQL Server, PostgreSQL, MySQL, SQLite compatibility notes
- T061: Create docs/index.html with navigation to all area pages, offline-capable
- T062: [P] Review pass: verify every public method appears with signature and example (SC-9)

**Checkpoint**: API documentation site complete and production-ready

---

## Phase 9: Polish & Verification

- T063: [P] Run full test suite (dotnet test) - all phases complete
- T064: [P] Validate backward compatibility - existing tests pass without config changes
- T065: XML docs on all new public APIs: Execute, ExecuteBatch, Get, GetAll, GetStream, QueryMultiEntity (arities 2-7), TypeHandler, EnumStorage, JauntyRow, JauntyParameters (SC-4)
- T066: Update README with feature comparison table: Execute vs Dapper, Get/GetAll, multi-entity mapping, type handlers, enum storage
- T067: [P] Run full benchmark suite (ExecuteBenchmarks, GetBenchmarks, multi-entity benchmarks) - verify SC-1, SC-2, SC-3, SC-5 criteria
- T068: [P] Verify NativeAOT compat: core package compiles AOT, no reflection in core paths
- T069: Update .PublicAPI.txt with new public types and methods
- T070: Final integration test pass on all 4 dialects (SQL Server, PostgreSQL, MySQL, SQLite)

**Checkpoint**: Feature complete and production-ready

---

## Execution Order

1. **Phase 1 (Foundational)** - T001-T008: Start immediately (T001-T003 parallel)
   - Blocks: All user stories
   - Dependencies: None

2. **Phase 2 (US1 Execute)** - T009-T016: After T008 complete
   - Blocks: Nothing
   - Dependencies: T001-T008

3. **Phase 3 (US2 Get/GetAll)** - T017-T025: After T008 complete, parallel with Phase 2
   - Blocks: Nothing
   - Dependencies: T001-T008

4. **Phase 4 (US3 Multi-entity)** - T026-T032: After T008 complete, parallel with Phase 2-3
   - Blocks: Nothing
   - Dependencies: T001-T008

5. **Phase 5 (US4 Handlers+Enum)** - T033-T041: After T008 complete, parallel with Phase 2-4
   - Blocks: Nothing
   - Dependencies: T001-T008

6. **Phase 6 (US5 P2)** - T042-T047: After T033 (type handlers must work)
   - Blocks: Nothing
   - Dependencies: T001-T008, T033-T041

7. **Phase 7 (US6 P3)** - T048-T051: After T018 (Get must exist)
   - Blocks: Nothing
   - Dependencies: T001-T008, T017-T025

8. **Phase 8 (API Docs)** - T052-T062: After T016, T025, T032, T041 (all feature phases)
   - Blocks: Nothing
   - Dependencies: T001-T051

9. **Phase 9 (Polish)** - T063-T070: After all user story phases complete
   - Blocks: Release
   - Dependencies: All T001-T062

---

## Task Summary by Phase

| Phase | Tasks | Dependencies | Parallel |
|-------|-------|--------------|----------|
| 1: Foundational | T001-T008 | None | T001-T003 |
| 2: US1 Execute | T009-T016 | T001-T008 | T014-T016 |
| 3: US2 Get/GetAll | T017-T025 | T001-T008 | T023-T025 |
| 4: US3 Multi-entity | T026-T032 | T001-T008 | T030-T032 |
| 5: US4 Handlers+Enum | T033-T041 | T001-T008 | T038-T041 |
| 6: US5 P2 Features | T042-T047 | T001-T008, T033 | T044-T047 |
| 7: US6 P3 Keys | T048-T051 | T001-T008, T017 | T051 |
| 8: API Docs | T052-T062 | T001-T051 | T054-T060 |
| 9: Polish | T063-T070 | All T001-T062 | T063-T064, T067-T069 |

**MVP Complete**: After Phase 5 (T041) - Core four user stories production-ready
**Full Release**: After Phase 9 (T070) - All features complete, benchmarks verified, docs updated

---

## Commit Convention

Commit format: feat(Jaunty): T00X - [description]

Examples:
- feat(Jaunty): T001 - Create TypeHandlers folder structure
- feat(Jaunty): T009 - Implement Execute with parameter binding and interceptors
- feat(Jaunty): T054 - Add Execute.html documentation page

---

Test: dotnet test after each phase checkpoint
Verify: SC-1 through SC-9 against success criteria in spec.md
