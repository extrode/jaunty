# Coverage Gap Inventory — 2026-07-04

Produced by the enterprise readiness pass (PROD-105). Collected with
`coverlet.collector` (now referenced by all 6 test projects) via
`dotnet test -c Release --framework net8.0 --collect:"XPlat Code Coverage"`,
**without local DB containers** — SqlServer/MySql/Postgres integration tests were
skipped (713 skips), which matters for interpreting the zeros below.

This is a handoff inventory: tests for these gaps are to be written by a follow-up
work, not part of this pass.

## Test suite status (all green)

4,369 passed / 0 failed / 714 skipped. Skips = DB-dialect integration tests
(SqlServer/MySql/Postgres) that require containers; the full core suite was last
verified against a SQL Server 2022 container on 2026-07-03 (3,587/3,589).

## Per-assembly line coverage (SQLite-only run)

| Assembly | Line coverage | Lines |
|---|---|---|
| Jaunty | 71.8% | 2433/3390 |
| Jaunty.Fluent | 77.4% | 2331/3011 |
| Jaunty.FlatFiles.DuckDB | 69.6% | 533/766 |
| Jaunty.Scaffolding | 62.7% | 423/675 |
| Jaunty.FlatFiles | 51.5% | 159/309 |
| Jaunty.Extensions.Reflection | 50.4% | 586/1163 |

## Genuinely untested (suites run fully locally; these are real gaps — write tests)

Priority order for the test-writing work:

1. `Jaunty.Fluent.JoinedQuery4Builder<T1..T4>` — 0/164 lines, and
   `Jaunty.Fluent.JoinClause4Builder<T1..T4>` — 0/43. The whole 4-entity join
   surface is untested (relates to PRD-007 3-way/4-way join parity).
2. `Jaunty.Fluent.Internals.QueryBuilderBase` — 0/72.
3. `Jaunty.JauntyLoggingExtensions` — 0/34 (public logging API, shipped in core).
4. `Jaunty.TypeHandlers.TypeHandlerRegistry` — 42% (15/36); Remove/replace paths
   untested.
5. `Jaunty.FlatFiles.FileSources.ExcelFileSource` (0/33),
   `DeltaLakeFileSource` (0/21), `IcebergFileSource` (0/24) — advertised file
   formats with zero tests (pure SQL-generation logic, no external deps needed).
6. `Jaunty.FlatFiles.Core.FlatFileOptions` — 38% (28/73).
7. `Jaunty.Scaffolding.Providers.MySql.MySqlTypeMapper` (0/53) and
   `PostgreSql.PostgreSqlTypeMapper` (0/63) — pure type-mapping logic, unit-testable
   without a database (SQLite/SqlServer mappers ARE tested; these two are not).
8. `Jaunty.FlatFiles.DuckDB.Internals.Import.ImportDialectResolver` — 30% (6/20).
9. `Jaunty.Extensions.Reflection.MetadataCache<T>` — 50% (77/155).
10. `Jaunty.Jaunty` (core static API surface) — 42% (419/1000): large #if-net472/
    ns2.0 sections and provider-conditional paths dilute this; needs member-level
    triage after container-backed collection (see below).

## 0% only because containers were absent (verify with container run before writing tests)

- `Jaunty.Extensions.Reflection.BulkCopy.{MySql,PostgreSql,SQLite,SqlServer}BulkCopyProvider`
  and the 4 `*DialectWithBulkCopy` classes. Note: SQLiteBulkCopyProvider at 0/84 is
  suspicious even for a SQLite-capable run — likely genuinely untested; check first.
- `Jaunty.FlatFiles.DuckDB.Internals.Import.{PostgreSql,SqlServer}ImportDialect`.
- `Jaunty.Scaffolding.Providers.{MySql,PostgreSql,SqlServer}SchemaReader`.

Action for CI (PRD-015 extension): run coverage collection in the CI job that has the
SQL Server container, and publish the cobertura artifact so the two categories above
can be separated definitively.

## Known failing/flaky (environmental, from prior reports)

- MySQL table-state isolation in `Jaunty.Tests` Release mode (47 failures when run
  against live MySQL; March 2026 audit finding, still open — test-fixture cleanup).
- 2 `CS8600` warnings in `TypeHandlerRoundTripTests.cs` (lines 157, 229).

## How to reproduce

```bash
dotnet test Jaunty.slnx -c Release --framework net8.0 \
  --collect:"XPlat Code Coverage" --results-directory ./coverage
```
