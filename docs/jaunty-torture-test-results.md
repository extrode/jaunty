# Jaunty Torture Test — Results Matrix

Part 1 only (eShopOnWeb port). Part 2 (Sakila/Pagila) and optional Part 3 (Conduit/SQLite
smoke test) not attempted in this pass — see `docs/jaunty-torture-test-handoff.md`.

| Test | SQL Server | Postgres | MySQL | MariaDB | SQLite |
|---|---|---|---|---|---|
| eShopOnWeb FunctionalTests (12 tests) | 12/12 pass | 12/12 pass | 12/12 pass | 12/12 pass | 12/12 pass |
| Sakila query 1..15 | not attempted | not attempted | not attempted | not attempted | not attempted |

All 5 targets run the same `JauntyRepository<T>` code path with real SQL-level query
translation (not in-memory filtering) — `WHERE`/`ORDER BY`/`SKIP`/`TAKE` are pushed to SQL
via Jaunty's fluent builder (`connection.From<TRow>()...`), and `Include`/`ThenInclude`
child collections are hydrated with one targeted `WhereIn(fk, parentIds)` query per include
path, never a full child-table scan.

## Dialect-specific workarounds required in Jaunty itself

One, found and worked around at the port level (not fixed in Jaunty — see
`docs/jaunty-torture-test-gaps-log.md` #7 for the full writeup and recommendation):

- **SQL Server `OFFSET`/`FETCH` requires a preceding `ORDER BY`.**
  `src/Jaunty/Dialects/SqlServerDialect.cs`'s `GetPagingSql` unconditionally appends
  `OFFSET...FETCH` without checking for or supplying an `ORDER BY`. `Skip`/`Take` without an
  explicit order — a completely ordinary query shape — works fine on Postgres/MySQL/SQLite
  and fails only on SQL Server, only at query-execution time (not at build time). Worked
  around in the port (`JauntyRepository<T>.QueryRootRows`) by defaulting to `ORDER BY <Id>`
  whenever paging is requested with no explicit spec-level order.

No other dialect-specific SQL failures were observed. Other gaps found during this test
(the `where T : new()` constraint excluding DDD-style aggregates, EF-InMemory test fixtures
needing a real-DB substitute) are dialect-independent — see the gaps log for the full list
and verdicts (core candidate / extension candidate / out of scope).

## Non-dialect issues found (in the port, not Jaunty)

Two genuine bugs found and fixed during this test, neither a Jaunty gap:

1. **Identity write-back after Insert.** `JauntyRepository<T>`'s `Persist`/`PersistBasket`/
   `PersistOrder` originally discarded the DB-generated identity returned by
   `connection.Insert(...)` instead of writing it back onto the in-memory entity's `Id`. Any
   insert-then-immediately-update flow (e.g. adding the first item to a brand-new basket)
   silently operated against `Id == 0` afterward — no exception, just data that "disappeared"
   on the next read. Fixed by writing the generated ID back via the existing
   `AggregateMappers.SetBaseId` reflection helper after every insert.
2. **Native Windows DB services intercepting docker-forwarded ports.** This machine had
   native `postgres.exe` (port 5432) and `mysqld.exe` (port 3306) Windows services already
   bound to the same ports docker-compose was forwarding to the torture-test containers,
   producing confusing dialect-specific-looking errors ("database does not exist" on
   Postgres; an auth-plugin/access-denied error on MySQL) that were actually just a
   different server answering the connection. Fixed by remapping the containers to
   5433/3308 respectively (`docker-compose.yml`) rather than touching the unrelated system
   services.

## Environment / how to reproduce

- `docker compose up -d` (repo root) starts all 4 real DBs.
- SQLite is the default target for `dotnet test tests/FunctionalTests/FunctionalTests.csproj`
  (no env var needed).
- For a real DB: set `JAUNTY_TORTURE_DB` to `mssql`/`postgres`/`mysql`/`mariadb`, and the
  matching `JAUNTY_TORTURE_CONNSTR_{MSSQL,POSTGRES,MYSQL,MARIADB}` to a full connection
  string (see `tests/FunctionalTests/TortureDatabase.cs` in the port — connection strings
  used for this run are recorded in `docs/jaunty-torture-test-gaps-log.md` history, not
  committed to source, since they contain the dev-only docker password).
- The eShopOnWeb clone itself lives at `torture-test/eshoponweb/` (gitignored — a disposable
  vendor clone). The actual port code that was written/edited is preserved at
  `samples/torture-test-eshoponweb-port/` in this repo.
