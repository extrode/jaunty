# eShopOnWeb → Jaunty port (torture test artifact)

Reference snapshot of the files written/edited to port `dotnet-architecture/eShopOnWeb`'s
Catalog/Basket/Order data layer from EF Core onto Jaunty, per
`docs/jaunty-torture-test-handoff.md` (Part 1). See
`docs/jaunty-torture-test-gaps-log.md` for what this surfaced about Jaunty itself.

This is **not a buildable sample** — it's a snapshot of files that live inside a full
eShopOnWeb clone (gitignored from Jaunty's own history, since it's someone else's repo).
Paths mirror their location in that clone. Kept here so the actual port code survives
independently of the local, disposable vendor clone.

At last verification: builds clean, 12/12 `FunctionalTests` pass against a Jaunty-backed
repository on all 5 targets — SQLite (test default), SQL Server, PostgreSQL, MySQL, and
MariaDB (select via `JAUNTY_TORTURE_DB` env var: `sqlite`/`mssql`/`postgres`/`mysql`/
`mariadb`, with `JAUNTY_TORTURE_CONNSTR_*` for real-DB connection strings; `DatabaseProvider`
config key does the same for the app's own `Dependencies.cs`). Real SQL-pushdown query
translation throughout (`WHERE`/`ORDER BY`/`SKIP`/`TAKE` reach SQL via `JauntyRepository<T>`
+ `SpecRetargeter`; `Include`/`ThenInclude` hydrated with targeted `WhereIn` queries, never a
full-table scan). EF Core's own `EfRepository`/`CatalogContext`/
`Ardalis.Specification.EntityFrameworkCore` remain present alongside this in the actual
clone — `IntegrationTests` (out of scope for the port) depends on them directly, so they
weren't removed.

See `docs/jaunty-torture-test-gaps-log.md` for the 2 real Jaunty bugs/gaps this surfaced
(a `where T : new()` constraint that excludes DDD-style aggregates, and SQL Server's
`OFFSET`/`FETCH` requiring a preceding `ORDER BY` that `SqlServerDialect.GetPagingSql`
doesn't enforce or supply) and `docs/jaunty-torture-test-lessons-learned.md` for process
lessons (dos and don'ts for running a test like this).
