# Tasks: GroupBy on Joined Queries

**Input**: `docs/specs/004-fluent-groupby-joins/`
**Prerequisites**: `plan.md` (required), `spec.md` (required)

---

## Format

```
[ID] [P] [Story] Description
```

- `[ID]`: Task identifier (T001, T002, ...)
- `[P]`: Parallel-capable (different files, no dependencies)
- `[Story]`: User story mapping (US1–US3)

---

## Phase 1: Foundational (Blocks User Stories)

- **T001**: Extract `HavingExpressionHelpers` (or equivalent shared internal helper) from
  `GroupedQueryBuilder.EvaluateExpression`/`FormatHavingLiteral` (the gap #14 fix) so it is
  reusable by the new joined-group `Having` implementations. Regression test: existing
  single-entity `HAVING` behavior (including the closure-captured-variable test added for gap
  #14) unchanged.
- **T002**: Add internal state-reuse seam to `JoinedQueryBuilder<TFrom,TJoin>` exposing
  accumulated `_joins`/`_conditions`/parameters to a same-assembly grouped-joined builder
  constructor.
- **T003** [P]: Same seam for `JoinedQueryBuilder3<T1,T2,T3>`.
- **T004** [P]: Same seam for `JoinedQueryBuilder4<T1,T2,T3,T4>`.
- **T005**: Generalize `GroupByExpressionVisitor` to accept an ordered array of
  `EntityMetadata` (one per joined entity) for multi-parameter key/aggregate selector
  translation; single-entity call sites delegate to the new overload. Regression test: existing
  single-entity `GroupBy` SQL output byte-for-byte unchanged.

**Checkpoint**: Foundation ready — shared HAVING fix, state-reuse seam, and multi-entity
expression translation all in place and independently tested before any new public API is
added.

---

## Phase 2: User Story 1 (P1) MVP — 2-way join GroupBy

**Goal**: `.InnerJoin<TJoin>()....GroupBy((t1,t2) => ...)` works end-to-end.

**Implementation**:
- **T006** [US1] `IGroupingJoined<TKey,TFrom,TJoin>` interface: `Key`, `Count()`,
  `Count<TResult>(Expression<Func<TFrom,TJoin,TResult>>)`, `Sum`/`Avg`/`Min`/`Max` with the
  same multi-parameter selector shape.
- **T007** [US1] `IGroupedJoinedQuery<TFrom,TJoin,TKey>` interface: `Having(...)`,
  `Select<TResult>(Expression<Func<IGroupingJoined<TKey,TFrom,TJoin>,TResult>>)`.
- **T008** [US1] `GroupedJoinedQueryBuilder<TFrom,TJoin,TKey>` implementation, consuming the
  T002 seam; builds `SELECT ... FROM ... JOIN ... WHERE ... GROUP BY ... HAVING ...` reusing
  existing dialect escaping.
- **T009** [US1] `.GroupBy<TKey>(Expression<Func<TFrom,TJoin,TKey>>)` added to
  `IJoinedQuery<TFrom,TJoin>` / `JoinedQueryBuilder<TFrom,TJoin>`.
- **T010** [P] [US1] Integration tests (`FluentGroupByJoinTests.cs`): GroupBy + each of
  Count/Sum/Avg/Min/Max + DTO `Select`, all 4 real dialects + SQLite.
- **T011** [P] [US1] Test: GroupBy key drawn from the joined (not base) entity's column.
- **T012** [P] [US1] Test: column-name-collision edge case (both entities have a same-named
  column) resolves correctly via the multi-parameter selector.

**Checkpoint**: US1 complete + tested.

---

## Phase 3: User Story 3 (P1) MVP — HAVING closure-safety verification

- **T013** [US3] Integration test: `.Having(g => g.Sum((t1,t2) => ...) > capturedVariable)` on
  the 2-way case succeeds without `NotSupportedException` (verifies T001's extraction actually
  covers the new code path — this should require no new production code if T001 was done
  correctly; if it fails, that's a signal T001's extraction was incomplete).

**Checkpoint**: US3 complete — SC-4 verified.

---

## Phase 4: User Story 2 (P2) — 3-way and 4-way joins

- **T014** [US2] `IGroupingJoined3<TKey,T1,T2,T3>` + `IGroupedJoinedQuery3<T1,T2,T3,TKey>` +
  `GroupedJoinedQueryBuilder3<T1,T2,T3,TKey>`, using the T003 seam.
- **T015** [P] [US2] `.GroupBy<TKey>(Expression<Func<T1,T2,T3,TKey>>)` on `IJoinedQuery3`.
- **T016** [P] [US2] Integration tests (`FluentGroupByJoin3Tests.cs`), all 4 real dialects.
- **T017** [US2] Same for 4-way: `IGroupingJoined4`/`IGroupedJoinedQuery4`/
  `GroupedJoinedQueryBuilder4`, using the T004 seam.
- **T018** [P] [US2] `.GroupBy<TKey>(Expression<Func<T1,T2,T3,T4,TKey>>)` on `IJoinedQuery4`.
- **T019** [P] [US2] Integration tests (`FluentGroupByJoin4Tests.cs`), all 4 real dialects.

**Checkpoint**: US2 complete + tested.

---

## Phase 5: Torture-Test Closure (SC-2)

- **T020**: Rewrite `samples/torture-test-sakila-queries/Queries.cs` `Q02`
  (`Top5FilmsByRevenue`) using the new fluent `GroupBy`-on-join API; remove its raw-SQL
  equivalent from `DialectSql.cs`.
- **T021**: Same for `Q07` (`AverageRentalRateByCategory`).
- **T022**: Re-run all 15 `samples/torture-test-sakila-queries` queries against all 5 dialects;
  confirm identical results to the Part 2 baseline (`docs/jaunty-torture-test-results.md`).
- **T023**: Update `docs/jaunty-torture-test-gaps-log.md` gap #13 status to "fixed" (scoped —
  `MonthlyRentalCountPerStore` stays raw SQL per plan.md Key Design Decision 5), pointing at
  this branch/spec.

---

## Phase 6: Polish

- XML docs on all new public surface.
- `dotnet test` full solution, all dialects.
- Validate against every `spec.md` acceptance scenario and success criterion explicitly.

---

## Execution Order

1. Phase 1 (Foundational) — must complete first; T003/T004 can run parallel to each other.
2. Phase 2 (US1, 2-way) — after Phase 1.
3. Phase 3 (US3, HAVING verification) — after Phase 2.
4. Phase 4 (US2, 3/4-way) — after Phase 2; T014-T016 (3-way) and T017-T019 (4-way) can run in
   parallel to each other once T003/T004 are both done.
5. Phase 5 (torture-test closure) — after Phase 2 at minimum (only needs the 2-way case).
6. Phase 6 (Polish) — last.

**Parallel**: All `[P]` tasks within a phase can run concurrently.

---

**Commit**: After each task | **Test**: `dotnet test`
