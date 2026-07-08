# Jaunty Torture Test — Results Matrix

Part 1 (eShopOnWeb port) and Part 2 (Sakila/Pagila dialect-translation stress test) complete.
Optional Part 3 (Conduit/SQLite smoke test) not attempted — see
`docs/jaunty-torture-test-handoff.md`.

| Test | SQL Server | Postgres | MySQL | MariaDB | SQLite |
|---|---|---|---|---|---|
| eShopOnWeb FunctionalTests (12 tests) | 12/12 pass | 12/12 pass | 12/12 pass | 12/12 pass | 12/12 pass |
| Sakila query 1..15 | pass (see notes) | pass (see notes) | pass | pass | pass (baseline) |

## Part 2 — Sakila/Pagila query results

All 15 queries (`samples/torture-test-sakila-queries/`) ran successfully against all 5 targets
with no crashes and no unexplained result differences. Per-query comparison against the SQLite
baseline:

| # | Query | SQL Server | Postgres | MySQL | MariaDB |
|---|---|---|---|---|---|
| 1 | Rental history for a customer (3-way join) | exact | same rows, shifted dates* | exact | exact |
| 2 | Top 5 films by revenue (raw SQL — join+GROUP BY) | exact | exact | exact | exact |
| 3 | Actor filmography (2-way join) | exact | exact | exact | exact |
| 4 | Monthly rental count per store (raw SQL, per-dialect date-trunc) | exact | same rows, shifted dates* | exact | exact |
| 5 | Customers with outstanding rentals (2-way join) | same rows, tie-order differs† | same rows, shifted dates* | same rows, tie-order differs† | same rows, tie-order differs† |
| 6 | Film count per category (GroupBy+Count) | exact | exact | exact | exact |
| 7 | Average rental rate by category (raw SQL — join+GROUP BY, portable SQL) | exact | exact | exact | exact |
| 8 | Films with no category (WhereNotExists) | exact (empty set) | exact (empty set) | exact (empty set) | exact (empty set) |
| 9 | Top 10 customers by spend (GroupBy+Sum, client-sorted) | same rows, tie-order differs† | same rows, tie-order differs† | exact | exact |
| 10 | Actors in >25 films (GroupBy+Having, closure threshold) | exact | exact | exact | exact |
| 11 | Distinct stores stocking a film (Distinct) | exact | exact | exact | exact |
| 12 | Rental count per staff member (GroupBy+Count) | exact | exact | exact | exact |
| 13 | Customers who never paid (WhereNotExists) | exact (empty set) | exact (empty set) | exact (empty set) | exact (empty set) |
| 14 | Films above average rental rate (2-step: raw scalar + fluent filter) | same rows, tie-order differs† | same rows, tie-order differs† | exact | same rows, tie-order differs† |
| 15 | Paginated film list, page 2 (Skip/Take + OrderBy) | exact | exact | exact | exact |

`*` **Not a Jaunty issue.** Pagila's `rental`/`payment` timestamp data is systematically shifted
(~17 years forward, ~1 hour time-of-day) relative to jOOQ/sakila's static 2005–2006 snapshot used
by the other 4 targets — a data-provenance difference between the two independently-maintained
upstream sample-database projects, confirmed by checking the raw rows directly with each engine's
own CLI (`mysql`, `psql`). Row counts, row identities, and relative patterns (which customer
rented which film, which rentals are still outstanding) match exactly; only the literal date
values differ for the 3 queries that surface raw timestamps (Q1, Q4, Q5). Query correctness is
unaffected — this would reproduce identically even if Jaunty were never involved.

`†` **Not a Jaunty issue.** These 3 queries order or select "top N" by a column with many exact
ties (`rental_date` shared by dozens of rentals seeded at the same instant; `total spend`/
`rental_rate` shared by many customers/films) without a secondary tiebreaker in the query design
itself — SQL doesn't guarantee tie order unless one is specified, so which of several tied rows
sorts first is legitimately implementation-defined per engine. Row *sets* match exactly (verified
by sorting before diffing); only the order of tied rows differs. A production query needing
deterministic tie order would add an explicit secondary `OrderBy` (e.g. by id) — intentionally
not done here since the point was to prove the aggregate/filter logic, not tie-breaking.

**Net result: zero unexplained result differences across all 5 dialects for all 15 queries.**
Every observed difference traces to one of the two documented, non-Jaunty causes above.

## Part 2 — Jaunty gaps found

Four real findings while writing and running these queries, all in `docs/jaunty-torture-test-gaps-log.md`
(entries #11–14):
- **#11 (fixed 2026-07-08):** `Jaunty.Fluent`'s query builder had no NativeAOT-safe metadata
  path — it required `Jaunty.Extensions.Reflection` for every single fluent query, contradicting
  the constitution's "no runtime reflection in core" principle for its primary API. Closed via
  `docs/specs/003-fluent-nativeaot-metadata/`; this sample's `Jaunty.Extensions.Reflection`
  reference has since been removed entirely (see updated repro steps below) and all 15 queries
  still pass against all 5 targets.
- **#12 (fixed):** source generator produced uncompilable code for any `DateTime?`/`Guid?`
  property — a missing fully-qualified case in an existing type-name switch.
- **#13 (fixed 2026-07-08):** `GroupBy` cannot be combined with joins in the fluent API at all —
  forced 3 of the 15 queries to raw SQL passthrough, 2 of which needed no dialect-specific SQL.
  Closed by spec 004 (`IGroupingJoined`/`IGroupedJoinedQuery` for 2/3/4-way); `Q02`/`Q07`
  rewritten on the new API, byte-identical output across all 5 targets. `Q04` remains raw SQL
  (date-truncation, an unrelated, explicitly out-of-scope capability).
- **#14 (fixed):** `HAVING` rejected any closure-captured variable or method parameter as a
  comparison value — only literals worked, an easy-to-hit gap in ordinary parameterized code.

Also confirmed via the codegen-diff step (entries #9–10, not fixed, low priority): Postgres
enum/array columns and one SQLite compound-type-declaration edge case both fall back to `object`
in generated entities instead of a precise type.

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

For Part 2 (Sakila/Pagila):

- Same `docker compose up -d`, plus `data/sqlite/sakila.db` for the SQLite target (see
  `seed/sakila/README.md` for how it and the 4 real-DB databases were loaded).
- `cd samples/torture-test-sakila-queries && dotnet run -- <sqlite|sqlserver|postgres|mysql|mariadb>`.
  SQLite needs no env vars; the other 4 need the same `JAUNTY_TORTURE_CONNSTR_*` env vars as
  Part 1 (not committed, same reasoning as above), pointed at the `sakila`/`pagila` databases
  instead of the eShopOnWeb ones.
- Codegen-diff output (`dotnet-jaunty scaffold` run against each target) is preserved at
  `samples/torture-test-sakila-codegen/`.
