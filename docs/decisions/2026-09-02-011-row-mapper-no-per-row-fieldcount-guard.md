# 011 — The generated row mapper has no per-row `FieldCount` guard

**Date:** 2026-09-02
**Status:** Accepted
**Revises:** the `CreateRowMapper` shape introduced under PRD-001 (2026-07-02), which compared
`reader.FieldCount` on every row and fell back to `ReadEntity` when a stale delegate met a
changed result-set shape.

## Decision

**The closure `CreateRowMapper` returns is valid for the result set it was created against and
nothing else.** It reads columns through the ordinals resolved once by `OrdinalMap.Resolve` and
performs no per-row check of any kind. A caller that keeps the delegate across `NextResult()`
gets the first result set's ordinals applied to the second, which is a misuse, not a case the
mapper rescues.

The per-row `if (r.FieldCount != fieldCount) return ReadEntity(r);` is gone from both closures
(the `DbDataReader` fast path and the plain `IDataReader` path), and **a future audit must not
put it back**. The generator comment at the emission site and this file both say so.

## Why

On Microsoft.Data.Sqlite `FieldCount` is `sqlite3_column_count` through P/Invoke, one native
call per row, paid on every row of every query to cover a reuse no caller in this repository
commits:

- `DrDispatcher.Resolve` calls the factory per result set and hands the closure to one read loop.
- `GridReader.ReadCore` and `ReadAsyncCore` resolve lazily inside the loop for each result set,
  so a second `Read<T>()` after `NextResult()` gets a fresh closure.
- `QueryCore` resolves once per command, which has one result set.

## Measured

SQLite, 10,000 rows, warm job, 15 iterations, the same harness as
[benchmarks-2026-09-02.md](../05-quality/reports/benchmarks-2026-09-02.md), run alone before
and after the change on the same machine the same evening:

| Case | With the guard | Without | Change |
|---|---|---|---|
| ADO.NET (hand-coded) | 4.08 ms | 4.22 ms | run-to-run noise |
| Jaunty `Query<T>` | 5.42 ms | 4.95 ms | -0.47 ms |
| Jaunty `Query<T>` (`WithExpectedRowCount`) | 5.18 ms | 5.14 ms | inside its 0.70 ms SD |
| Jaunty (custom mapper, `GetDouble`, `WithExpectedRowCount`) | 4.04 ms | 4.16 ms | unaffected, as expected |
| RepoDb | 5.77 ms | 5.48 ms | run-to-run noise |
| Dapper | 7.45 ms | 7.51 ms | run-to-run noise |

The custom-mapper cases never had the guard, so they are the control: the harness moved by
about 0.1 ms between runs and the generated mapper moved by 0.47 ms. The plain `Query<T>` case
is now 1.17x the hand-coded loop on SQLite, from 1.33x. The hinted case's standard deviation
in the second run was too wide to read a change from; its mean did not move against it.

The remaining gap to the hand loop is the `IsDBNull` on the nullable `product_name` column,
one native call per row, which the hand loop does not make and the mapper must: NULL is a
legitimate value there, so the catch-based diagnosis of decision 010 does not apply.

## What the removal depends on

Every library path that hands a row mapper to a loop resolves it against the reader it will run
over. That is a property of `DrDispatcher` and `GridReader`, pinned by the tests below. A new
caller that caches the closure beyond one result set reintroduces the hazard, and the fix is in
that caller, not a guard in every generated mapper.

`OrdinalMap.CacheEntry.Matches` still compares `FieldCount`, once per `Resolve`, where a
result-set-level check belongs.

## What did not change

- Shape validation itself: `OrdinalMap.Resolve` still throws on a missing column and still
  verifies every ordinal by name before the closure is built.
- `ReadEntity`, the fully validating per-row path, is still emitted and still what `Mapper`
  (as opposed to `MapperFactory`) resolves to.
- Nullable columns keep `if (!IsDBNull) set`; non-nullable value types keep the try/catch
  diagnosis of [decision 010](2026-09-02-010-null-guard-by-catch-not-precheck.md).

## Pinned by

- `tests/Jaunty.SourceGenerator.Tests/SqliteGeneratedMapperShapeTests.cs`,
  `CreateRowMapper_ResolvedPerResultSet_MapsAReorderedSecondSet`: a fresh closure per result
  set maps a reordered, wider second set; reusing the first closure across `NextResult()` fails
  it, verified by making that change and watching it go red.
- The generator's emitted-source tests, which contain no `FieldCount` inside either closure.

## Where the code is

`src/Jaunty.SourceGenerator/JauntyGenerator.cs`, the `CreateRowMapper` emission (search for
"1b. CreateRowMapper"). The history of the read path, including the step that introduced the
factory, is in [How Jaunty got fast](../08-learn/how-jaunty-got-fast.md).
