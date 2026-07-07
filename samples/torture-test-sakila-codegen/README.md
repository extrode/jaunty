# Sakila/Pagila codegen diff (Part 2 torture test)

Output of `dotnet-jaunty scaffold` (`src/Jaunty.Scaffolding.Cli`) run against the same
logical Sakila/Pagila schema on all 5 targets, per `docs/jaunty-torture-test-handoff.md`
Part 2 step 2 ("diff generated models across DBs for the same logical schema").

Generated with:

```
dotnet-jaunty scaffold --connection <conn> --provider <p> --output <dir> --namespace Sakila.Entities --force
```

against the databases documented in `seed/sakila/README.md`.

## Table counts

16 tables on SQL Server, MySQL, MariaDB, SQLite. 22 on Postgres — Pagila partitions the
`payment` table by month (`payment_p2022_01` .. `_07`), which MySQL Sakila's `payment`
does not; scaffolding correctly picked up each partition as its own table.

## Findings: structurally identical modulo type-mapping (as expected)

Compared `Actor.cs`, `FilmActor.cs` (composite PK), `Film.cs`, and `Customer.cs` across all
5 outputs. Every property-shape and type difference found traces back to a genuine
difference in the underlying DDL of the 4 independently-authored schema ports themselves
(jOOQ/sakila's 4 dialect variants + pagila), not to Jaunty mistranslating the same type
differently per dialect:

- `film.release_year`: `VARCHAR(4)` in SQL Server/SQLite -> `string?`, `YEAR` in MySQL ->
  `short?`, a `public.year` domain in Pagila -> `int?`. Three different column types
  because the four schema authors chose three different underlying types, not a Jaunty gap.
- `customer.active`: `CHAR(1)` in SQL Server/SQLite -> `string`, `SMALLINT`/`TINYINT(1)` in
  MySQL/MariaDB -> `sbyte`. Again a genuine per-port schema difference.
- Pagila's `customer` table carries both a legacy `active integer` column and the "real"
  `activebool boolean NOT NULL DEFAULT true` — a documented pagila quirk (kept for
  historical compatibility with the original Sakila port), not something Jaunty introduced.
- `[Table]` attribute presence: emitted (schema-qualified) for SQL Server/Postgres, omitted
  for MySQL/MariaDB/SQLite. This is `EntityCodeGenerator`'s existing "only emit `[Table]`
  when the table name differs from the class name or a schema is present" optimization
  (`src/Jaunty.Scaffolding/CodeGeneration/EntityCodeGenerator.cs`) working as designed —
  MySQL/SQLite have no schema concept, so the attribute is correctly skipped.
- `actor_id` etc.: `int` on SQL Server/Postgres/MySQL/MariaDB, `long` on SQLite. SQLite has
  no fixed-width integer storage (`INTEGER` affinity is always 64-bit), so `long` is the
  correct, intentional mapping.
- Composite primary keys (`film_actor.actor_id` + `film_actor.film_id`) are correctly
  marked `[Key]` on **both** columns on **all 5 targets, including SQL Server** — this
  exercised the exact non-adjacent-composite-key code path that crashed before the fix in
  `docs/jaunty-torture-test-gaps-log.md` (SQL Server `PK` marking bug, fixed on
  `fix/scaffolding-sqlserver-pk-mutation`, merged to `dev` before this run).

## Findings: genuine (minor) type-mapper gaps

Two real gaps surfaced, both logged in `docs/jaunty-torture-test-gaps-log.md` rather than
fixed in this pass (narrow/edge-case, not blocking, unlike the SQL Server PK-marking crash
which was fixed directly):

1. **Postgres enum and array types fall back to `object`.** `film.rating` is a Postgres
   enum (`mpaa_rating`); `film.special_features` is `text[]`. Both scaffold as
   `public object? Rating`/`SpecialFeatures` instead of a proper enum/array mapping.
2. **SQLite type mapper does exact-keyword matching, not SQLite's real type-affinity
   algorithm.** `film.description` is declared `BLOB SUB_TYPE TEXT` (a legacy
   Firebird-style annotation present in jOOQ's SQLite Sakila port). SQLite's own
   documented affinity rules would give this column TEXT affinity (its declared type
   contains "TEXT" as a substring), but `SQLiteTypeMapper.MapToCSharpType` does an exact
   `switch` on the whole base-type string, doesn't match `"BLOB SUB_TYPE TEXT"` against any
   case, and falls through to the `object` default.

## Boundary case confirmed as predicted

Pagila's `film` table has a `fulltext tsvector` full-text-search column. It scaffolds as
`public object Fulltext { get; set; } = null!;` — exactly the "raw-SQL-passthrough, not an
expression-tree target" boundary the handoff doc predicted in advance. Not a bug.
