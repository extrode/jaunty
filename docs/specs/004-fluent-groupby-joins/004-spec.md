# Feature Specification: GroupBy on Joined Queries

**Branch**: `004-fluent-groupby-joins`
**Created**: `2026-07-07`
**Status**: Draft

---

## Context / Problem Statement

Torture-test gap #13 (`docs/jaunty-torture-test-gaps-log.md`): `GroupBy<TKey>` exists only on
the single-entity `QueryBuilder<T>` (`src/Jaunty.Fluent/Builders/Query/GroupedQueryBuilder.cs`).
None of the joined-query interfaces — `IJoinedQuery<TFrom,TJoin>`, `IJoinedQuery3<T1,T2,T3>`,
`IJoinedQuery4<T1,T2,T3,T4>` (`src/Jaunty.Fluent/Interfaces/IJoinedQuery.cs`) — expose a
`GroupBy` member at all. This forced 3 of the 15 Part 2 torture-test queries to raw SQL
passthrough (`connection.Query<T>(sql, ...)`), 2 of which needed no dialect-specific SQL at
all — purely worked around this gap, not a genuine translation-difficulty problem.

The two pipelines are structurally separate today: `GroupedQueryBuilder<T,TKey>` wraps a single
entity type `T` and receives pre-built `WhereCondition`s/parameters from `QueryBuilder<T>` at
construction, assembling `SELECT ... FROM ... WHERE ... GROUP BY ... HAVING ...` itself.
`JoinedQueryBuilder<TFrom,TJoin>` (and its 3/4-way siblings) independently track `_joins: List<JoinInfo>`
plus their own `_conditions`/`_orderByColumns`, assembling `SELECT ... FROM ... JOIN ... WHERE ...
ORDER BY ...` in their own `BuildSelectSql()`. Neither pipeline currently has a seam for the
other to plug into, and `IGrouping<TKey,T>` (`src/Jaunty.Fluent/Interfaces/IGrouping.cs`) is
hardcoded to a single entity type `T` for its `Count`/`Sum`/`Avg`/`Min`/`Max` selectors.

**Goal**: `GroupBy` works on 2-way, 3-way, and 4-way joined queries, producing correct
`GROUP BY`/`HAVING`/aggregate SQL across all 4 real dialects, using the same multi-parameter
lambda convention Jaunty.Fluent already uses for joined `WHERE` clauses (e.g.
`.Where((r, i, f) => r.CustomerId == customerId)`, proven in Part 2's `Q01`).

---

## 1. User Scenarios & Testing

### User Story 1: GroupBy + aggregate on a 2-way join

**Priority**: `P1` (MVP)

**Description**: A developer groups a 2-way joined query by a key drawn from either joined
entity and projects an aggregate (`Count`/`Sum`/`Avg`/`Min`/`Max`) into a DTO — the same shape
already proven for single-entity GroupBy in Part 2's `Q10` (`Actors in >25 films`), but with a
second joined table in scope.

**Why This Priority**: This is the direct, concrete case behind gap #13 — 2 of the 3
raw-SQL-forced queries in Part 2 are exactly this shape (join + `GROUP BY` + aggregate, no
other dialect-specific SQL needed).

**Independent Test**: Rewrite `samples/torture-test-sakila-queries/DialectSql.cs`'s
`Top5FilmsByRevenue` and `AverageRentalRateByCategory` raw-SQL queries as fluent
`.InnerJoin<T>().GroupBy(...)` calls; confirm identical results against all 5 dialects.

**Acceptance Scenarios**:
```gherkin
Scenario: 2-way join, GroupBy, Sum aggregate, DTO projection
Given db.From<Inventory>().InnerJoin<Film>().OnFromSecond(...)
When .GroupBy((i, f) => f.FilmId).Select(g => new Dto { FilmId = g.Key, Revenue = g.Sum((i, f) => f.RentalRate) })
Then correct GROUP BY SQL is generated and executed against all 4 real dialects + SQLite,
  results matching the equivalent raw SQL query

Scenario: GroupBy key drawn from the joined (not base) entity
Given a 2-way join
When .GroupBy((t1, t2) => t2.SomeColumn) is used
Then GROUP BY references the joined table's column correctly, with dialect-correct
  table/column escaping
```

### User Story 2: GroupBy + aggregate on 3-way and 4-way joins

**Priority**: `P2`

**Description**: Same capability extended to `IJoinedQuery3`/`IJoinedQuery4`, using a 3-arg or
4-arg key-selector lambda, consistent with the existing 3/4-way `WHERE` convention.

**Acceptance Scenarios**:
```gherkin
Scenario: 3-way join GroupBy
Given a 3-way joined query (rental/inventory/film shape, as in Part 2's Q01)
When .GroupBy((t1, t2, t3) => t3.SomeKey) is used
Then correct SQL is generated and executed against all 4 real dialects
```

### User Story 3: HAVING on grouped-joined queries, closure-safe from day one

**Priority**: `P1` (MVP)

**Description**: `.Having(...)` on the new grouped-joined builder must support
closure-captured variables and method parameters immediately — Part 2 found and fixed exactly
this bug (gap #14) for the single-entity case (`GroupedQueryBuilder.TranslateHavingExpression`
gained an `EvaluateExpression` compile-and-invoke fallback). The new joined-group code path
must reuse that fix, not reintroduce the bug independently.

**Acceptance Scenarios**:
```gherkin
Scenario: HAVING with a captured threshold variable, joined-group case
Given var minRevenue = 100m; and a 2-way joined GroupBy query
When .Having(g => g.Sum((i, f) => f.RentalRate) > minRevenue) is called
Then the query succeeds (does not throw NotSupportedException), matching the fix already
  applied to the single-entity case
```

### Edge Cases

- Column name collisions between joined entities (e.g. both entities have a column literally
  named `Id`) — the multi-parameter lambda convention disambiguates by construction (caller
  always names which entity parameter a column comes from), so this should require no special
  handling, but must be covered by a test.
- `GroupBy` key selector spanning columns from more than one joined entity in a single key
  (e.g. a composite key `(t1.A, t2.B)`) — confirm whether the existing single-entity
  `GroupByExpressionVisitor` already supports multi-column anonymous-type keys, and if so
  extend that support to the multi-entity case rather than treating it as new scope.

---

## 2. Requirements

### Functional Requirements

- `FR-001`: `.GroupBy<TKey>(Expression<Func<TFrom, TJoin, TKey>>)` added to
  `IJoinedQuery<TFrom, TJoin>`, returning a new `IGroupedJoinedQuery<TFrom, TJoin, TKey>`
  (exact name decided in `plan.md`).
- `FR-002`: Equivalent 3-arg and 4-arg `GroupBy` overloads added to `IJoinedQuery3`/
  `IJoinedQuery4`.
- `FR-003`: New builder(s) (e.g. `GroupedJoinedQueryBuilder<TFrom,TJoin,TKey>` + 3/4-way
  variants) reuse the existing `_joins`/`_conditions` state already accumulated by the parent
  joined-query builder — no re-parsing of `ON` conditions — appending `GROUP BY`/`HAVING`/
  aggregate-`SELECT` SQL fragments analogous to the existing single-entity
  `GroupedQueryBuilder`.
- `FR-004`: The grouping-result interface for the joined case exposes `Count`/`Sum`/`Avg`/
  `Min`/`Max` with selectors typed `Expression<Func<TFrom, TJoin, TResult>>` (multi-parameter,
  matching the existing joined-`WHERE` convention) — not a materialized tuple type.
- `FR-005`: `.Having(...)` on the new grouped-joined builder(s) reuses the closure-evaluation
  fallback already fixed in `GroupedQueryBuilder.EvaluateExpression` (factor into a shared
  internal helper if it isn't already reusable as-is).
- `FR-006`: `.Select(...)` projection supports `g.Key`, `g.Count()`, `g.Sum(...)`, etc. mapped
  to a caller-supplied DTO, consistent with the existing single-entity GroupBy `.Select(g => new
  Dto {...})` pattern already proven in Part 2's `Q10`.
- `FR-007`: All 4 real dialects (SQL Server, Postgres, MySQL, MariaDB) plus SQLite generate
  correct `GROUP BY`/`HAVING` SQL for the joined case, reusing the existing `ISqlDialect`
  escaping/quoting — no new dialect-specific logic introduced by this feature.

### Key Entities

| Entity | Description | Key Attributes |
|--------|-------------|-----------------|
| `IGroupedJoinedQuery<TFrom,TJoin,TKey>` (name TBD) | New interface, 2-way grouped-joined query | GroupBy result, `.Having()`, `.Select()` |
| `IGroupingJoined<TKey,TFrom,TJoin>` (name TBD) | New interface generalizing `IGrouping<TKey,T>` for the 2-entity case | `Key`, `Count()`, `Sum/Avg/Min/Max((t1,t2) => ...)` |
| `GroupedJoinedQueryBuilder<TFrom,TJoin,TKey>` | New internal builder implementing the above | Reuses parent `_joins`/`_conditions` |

---

## 3. Success Criteria

- `SC-1`: A 2-way joined `GroupBy` + aggregate query compiles and executes correctly against
  all 4 real dialects + SQLite.
- `SC-2`: The 3 `samples/torture-test-sakila-queries/` queries currently forced to raw SQL
  passthrough specifically because of this gap (`Top5FilmsByRevenue`,
  `MonthlyRentalCountPerStore`, `AverageRentalRateByCategory` — see `DialectSql.cs`) are
  rewritten using the new fluent `GroupBy`-on-join API and still produce identical results —
  the concrete closure test for gap #13. (`MonthlyRentalCountPerStore` needs per-dialect date
  truncation in the key selector; confirm in `plan.md` whether that's in scope for this
  feature or remains a separate raw-SQL case for date-function reasons unrelated to gap #13.)
- `SC-3`: Existing single-entity `GroupBy` behavior (`QueryBuilder<T>.GroupBy`) is completely
  unchanged — no regression, full existing suite green.
- `SC-4`: `HAVING` on a joined-grouped query supports closure-captured variables from the first
  commit — does not reintroduce gap #14.

---

## 4. Non-Goals / Out of Scope

- `OrderBy`/`Skip`/`Take` on grouped queries (joined or single-entity) — a related but separate
  gap; client-side sort/paginate after `.Select()` remains the documented workaround (see
  `docs/jaunty-torture-test-results.md` Part 2 notes). Not addressed here.
- `GroupBy` combined with set operations (`UNION`, etc.).
- Joins beyond the existing 4-way ceiling.
- Per-dialect date-truncation functions in `GROUP BY` key selectors (e.g. `MonthlyRentalCountPerStore`'s
  `FORMAT`/`TO_CHAR`/`DATE_FORMAT`/`strftime` differences) — only in scope if `plan.md`
  determines the existing expression-translation layer already has a portable answer; otherwise
  that specific query remains raw SQL for date-function reasons independent of this gap.

---

## 5. Design Notes (Planning Phase)

1. **Naming**: `IGroupedJoinedQuery<...>`/`IGroupingJoined<...>` above are placeholders —
   `plan.md` should pick names consistent with existing conventions (`IJoinedQuery`,
   `IGrouping`) before implementation.
2. **`IGrouping<TKey,T>` generalization risk**: widening the existing single-entity interface
   in place risks breaking existing consumers; a sibling interface for the joined case (mirrors
   the same reasoning in spec 003 for `IMapped<T>`) is the safer default — confirm in `plan.md`.
3. **State reuse seam**: `GroupedQueryBuilder` already receives `_whereConditions`/`_parameters`
   from `QueryBuilder<T>` at construction — the joined case needs the equivalent internal
   factory/constructor seam exposing `JoinedQueryBuilder`'s accumulated `_joins`/`_conditions`
   to the new grouped-joined builder, not a SQL-from-scratch reimplementation.
4. **`GroupByExpressionVisitor` generalization**: currently resolves against a single entity's
   `EntityMetadata`. Needs either an overload accepting an array/tuple of `EntityMetadata` (one
   per joined entity) for translating multi-parameter key/aggregate selectors, or a comparable
   mechanism — decide the exact shape in `plan.md`.

---

## Section Summary

| Section | Required | Purpose |
|---------|----------|---------|
| User Scenarios | Yes | User stories + acceptance |
| Requirements | Yes | Functional capabilities |
| Success Criteria | Yes | Measurable outcomes |

---

**Next**: `plan.md` — implementation plan resolving naming, the `IGrouping` generalization
question, and the `GroupByExpressionVisitor` multi-entity shape.
