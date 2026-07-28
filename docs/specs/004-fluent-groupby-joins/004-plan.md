# Implementation Plan: GroupBy on Joined Queries

**Branch**: `004-fluent-groupby-joins`
**Date**: 2026-07-07
**Spec**: [spec.md](spec.md)
**Input**: `docs/specs/004-fluent-groupby-joins/004-spec.md`

---

## Summary

**Primary Requirement**: `GroupBy` + aggregate (`Count`/`Sum`/`Avg`/`Min`/`Max`) + `Having` +
`Select` must work on 2-way, 3-way, and 4-way joined queries, using the same multi-parameter
lambda convention already used for joined `WHERE` clauses.

**Technical Approach**: introduce a sibling family of interfaces/builders next to the existing
`IJoinedQuery{,3,4}` / `JoinedQueryBuilder{,3,4}` and `IGrouping<TKey,T>` / `GroupedQueryBuilder<T,TKey>`,
rather than widening the existing single-entity types in place. The new joined-group builders
consume the parent joined-query builder's already-accumulated `_joins`/`_conditions` state via
an internal seam (mirroring how `GroupedQueryBuilder<T,TKey>` already receives
`_whereConditions`/`_parameters` from `QueryBuilder<T>` today), so no `FROM`/`JOIN`/`WHERE`
SQL-assembly logic is duplicated or reimplemented — only `GROUP BY`/`HAVING`/aggregate-`SELECT`
fragments are new.

---

## Technical Context

| Field | Value |
|-------|-------|
| **Language** | C# (netstandard2.0, net8.0) |
| **Dependencies** | Zero (core) |
| **Storage** | N/A (query-shape feature) |
| **Testing** | xUnit; integration tests all 4 real dialects + SQLite |
| **Platform** | Cross-platform (.NET) |
| **Project Type** | Library |
| **Performance** | No new allocations beyond what joined queries + GroupBy already allocate individually; no double SQL-string-building pass |
| **Constraints** | Must not change existing single-entity `GroupBy` or existing join behavior (additive only) |

---

## Constitution Check

- [x] Performance-first upheld — reuses existing accumulated join/where state, no rebuild
- [x] NativeAOT-compatible — expression-tree translation only, same approach as existing
      `WhereExpressionVisitor`/`GroupByExpressionVisitor`, no reflection introduced
- [x] Zero dependencies (core)
- [x] Tests before implementation — see Phase 1/2 task ordering in `tasks.md`

---

## Project Structure

### Documentation (this feature)

```
docs/specs/004-fluent-groupby-joins/
├── spec.md
├── plan.md          # This file
└── tasks.md
```

### Source Code

```
src/Jaunty.Fluent/
├── Interfaces/
│   ├── IJoinedQuery.cs              # add GroupBy overloads to IJoinedQuery / 3 / 4
│   ├── IGroupedJoinedQuery.cs       # new: 2-way grouped-joined query surface
│   ├── IGroupedJoinedQuery3.cs      # new: 3-way
│   ├── IGroupedJoinedQuery4.cs      # new: 4-way
│   └── IGroupingJoined.cs           # new: sibling to IGrouping<TKey,T> for 2-entity aggregates
│   └── IGroupingJoined3.cs / 4.cs   # new: 3/4-entity aggregate selectors
├── Builders/
│   ├── Join/
│   │   ├── JoinedQueryBuilder.cs    # expose internal seam: accumulated _joins/_conditions
│   │   ├── JoinedQueryBuilder3.cs   # same, 3-way
│   │   └── JoinedQueryBuilder4.cs   # same, 4-way
│   └── Query/
│       ├── GroupedQueryBuilder.cs        # factor EvaluateExpression/FormatHavingLiteral into
│       │                                  a shared internal helper if not already reusable
│       ├── GroupedJoinedQueryBuilder.cs      # new: 2-way
│       ├── GroupedJoinedQueryBuilder3.cs     # new: 3-way
│       └── GroupedJoinedQueryBuilder4.cs     # new: 4-way
└── Internals/
    └── GroupByExpressionVisitor.cs   # generalize to accept multiple EntityMetadata (one per
                                        # joined entity) for multi-parameter key/aggregate selectors

tests/Jaunty.Fluent.Tests/Integration/
├── FluentGroupByJoinTests.cs         # new: 2-way join + GroupBy + aggregate + Having + Select
├── FluentGroupByJoin3Tests.cs        # new: 3-way
└── FluentGroupByJoin4Tests.cs        # new: 4-way

samples/torture-test-sakila-queries/
├── DialectSql.cs                     # remove Top5FilmsByRevenue, AverageRentalRateByCategory
│                                       # raw SQL (SC-2); keep MonthlyRentalCountPerStore only
│                                       # if plan decision below keeps it raw
└── Queries.cs                        # rewrite Q02, Q07 (and Q04 if in scope) as fluent
                                        # GroupBy-on-join calls
```

---

## Key Design Decisions

1. **Sibling interfaces, not widened existing ones.** `IGrouping<TKey,T>` stays exactly as-is;
   new `IGroupingJoined<TKey,TFrom,TJoin>` (+3/4-way) are separate interfaces with
   `Count()`/`Count<TResult>(Expression<Func<TFrom,TJoin,TResult>>)`/`Sum`/`Avg`/`Min`/`Max`
   taking the same multi-parameter-lambda shape already used by joined `WHERE`. Same reasoning
   as spec 003's `IMapped<T>` decision: avoids any breaking change to existing single-entity
   consumers, adoptable incrementally.

2. **State-reuse seam.** Add an `internal` accessor (property or internal constructor overload)
   on `JoinedQueryBuilder<TFrom,TJoin>` (and 3/4-way) exposing its accumulated `_joins` and
   `_conditions`/parameters to a same-assembly `GroupedJoinedQueryBuilder<TFrom,TJoin,TKey>`
   constructor — exact same pattern `GroupedQueryBuilder<T,TKey>` already uses today, just
   sourced from the joined builder instead of `QueryBuilder<T>`. No public API surface change
   needed for this seam; both builders live in `Jaunty.Fluent`.

3. **`GroupByExpressionVisitor` generalization.** Add an overload/variant taking an ordered
   array of `EntityMetadata` (one per joined entity, matching parameter order in the
   multi-parameter lambda) instead of a single `EntityMetadata`. The existing single-entity
   overload can delegate to the new one with a 1-element array to avoid duplicating translation
   logic — confirm during implementation whether this refactor is clean or whether keeping two
   independent code paths is actually simpler; either is acceptable, but the single-entity path
   must not regress (spec.md SC-3).

4. **`HAVING` closure-safety reuse (spec.md FR-005 / SC-4).** `GroupedQueryBuilder`'s existing
   `EvaluateExpression`/`FormatHavingLiteral` (fixed for gap #14) should be extracted to an
   internal static helper (e.g. `HavingExpressionHelpers`) usable by both the single-entity and
   new joined-group `TranslateHavingExpression` implementations, so the fix is shared by
   construction rather than copy-pasted and risking drift.

5. **`MonthlyRentalCountPerStore` / date-truncation scope (spec.md SC-2 note).** This query's
   `GROUP BY` key is a per-dialect date-truncation expression (`FORMAT`/`TO_CHAR`/`DATE_FORMAT`/
   `strftime`), not a plain column reference. Decision: **out of scope for this feature.**
   `GroupByExpressionVisitor` translates C# property/method-call expressions into SQL column
   references; teaching it dialect-specific date-truncation functions is a separate, unrelated
   capability (arguably its own future gap/spec, e.g. "portable date-truncation in expression
   trees"). This feature closes gap #13 for the 2 queries that are pure join+GROUP BY+aggregate
   (`Top5FilmsByRevenue`, `AverageRentalRateByCategory`); `MonthlyRentalCountPerStore` remains
   raw SQL, and `spec.md` SC-2 is scoped down to those 2 queries accordingly.

6. **3-way/4-way key-selector arity.** Mirrors the existing `.Where((t1,t2,t3) => ...)`
   convention already proven in Part 2's `Q01` — no key-selector-pair (`.On<TLeftKey,TRightKey>`)
   style needed here, since `GroupBy`'s key selector (unlike a join's `ON` condition) only ever
   needs to read from the already-joined rows, not correlate two sides.

---

## Implementation Phases

### Phase 1: Foundational (Blocks All User Stories)
- Extract `HavingExpressionHelpers` (closure-safe evaluation) from `GroupedQueryBuilder` for
  shared reuse (Key Design Decision 4) — do this first so every new `Having` implementation is
  correct from its first commit, satisfying SC-4 by construction rather than by later testing.
- Add the internal state-reuse seam to `JoinedQueryBuilder`/3/4 (Key Design Decision 2).
- Generalize or add the multi-entity overload of `GroupByExpressionVisitor` (Key Design
  Decision 3), with a regression test proving the existing single-entity path is byte-for-byte
  unchanged in generated SQL.

### Phase 2: User Story 1 (P1) MVP — 2-way join GroupBy
- `IGroupedJoinedQuery<TFrom,TJoin,TKey>` + `IGroupingJoined<TKey,TFrom,TJoin>` interfaces.
- `GroupedJoinedQueryBuilder<TFrom,TJoin,TKey>` implementation (GroupBy/Having/Select).
- `.GroupBy<TKey>(Expression<Func<TFrom,TJoin,TKey>>)` added to `IJoinedQuery<TFrom,TJoin>`.
- Integration tests: 2-way join + GroupBy + each aggregate kind + DTO `Select`, all 4 real
  dialects + SQLite.

### Phase 3: User Story 3 (P1) MVP — HAVING closure-safety (verifies Phase 1's extraction)
- Integration test: `.Having(g => g.Sum(...) > capturedVariable)` on the 2-way case, proving
  Phase 1's shared helper actually closes the loop (this is largely a verification phase if
  Phase 1 was done correctly, not new implementation).

### Phase 4: User Story 2 (P2) — 3-way and 4-way
- Same shape as Phase 2, extended to `IJoinedQuery3`/`IJoinedQuery4` and their builders.

### Phase 5: Torture-Test Closure (SC-2)
- Rewrite `samples/torture-test-sakila-queries/Queries.cs` `Q02` (`Top5FilmsByRevenue`) and
  `Q07` (`AverageRentalRateByCategory`) using the new fluent `GroupBy`-on-join API; remove their
  raw-SQL equivalents from `DialectSql.cs`. Re-run all 15 queries against all 5 dialects,
  confirm identical results to the Part 2 baseline (`docs/jaunty-torture-test-results.md`).
- Update `docs/jaunty-torture-test-gaps-log.md` gap #13 status to "fixed" (scoped per Key
  Design Decision 5 — note `MonthlyRentalCountPerStore` remains raw SQL for an unrelated
  reason), pointing at this branch/spec.

### Phase 6: Polish
- XML docs on all new public surface.
- `dotnet test` full solution, all dialects.
- Validate against every `spec.md` acceptance scenario and success criterion explicitly.

---

**Next**: `tasks.md` — full task breakdown.
