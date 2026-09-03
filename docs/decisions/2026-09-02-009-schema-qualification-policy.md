# 009 — Schema qualification policy

**Date:** 2026-09-02
**Status:** Accepted
**Supersedes:** the undocumented belief that SQLite has no schemas

## Decision

**Jaunty emits a schema when the user names one, and nothing when the user does not.** It never
substitutes `ISqlDialect.GetDefaultSchema()` for a missing schema, on any dialect.

`GetDefaultSchema()` stays on the interface as informational and gains no consumer.

## What prompted it

`SQLiteDialect.EscapeTableName` accepted a schema and discarded it. Measured with two attached
databases and an entity mapped `[Table("products","archive")]`:

```
Jaunty ToSql()       -> SELECT id, name FROM products
Jaunty returned      -> id=1 name=main-row
after Insert:
  main.products      -> main-row, written-by-jaunty
  archive.products   -> archive-row
```

No exception. Reads returned another table's rows; writes landed in another file. Fixed in
`19012846`, merged `35ca2f3f`. The wrong premise was stated in five places in `src/`, and the only
schema test passed `""`.

That raised the general question: when no schema is supplied, should Jaunty fill in the default?

## SQLite facts, measured

Schemas are `main`, `temp`, and every `ATTACH` alias. The alias is arbitrary:

```
sqlite> ATTACH DATABASE 's2.db' AS master;
sqlite> ATTACH DATABASE 'd1.db' AS dbo;
sqlite> CREATE TABLE dbo.x(i); INSERT INTO dbo.x VALUES(7); SELECT i FROM dbo.x;
7
sqlite> PRAGMA database_list;
0|main  |C:\...\s1.db
1|temp  |
2|master|C:\...\s2.db
3|dbo   |C:\...\d1.db
```

The same file can be attached under a second alias, and both names reach it:

```
sqlite> ATTACH DATABASE 's1.db' AS alias2;      -- s1.db is already main
sqlite> INSERT INTO alias2.t VALUES(99,'via-alias');
sqlite> SELECT count(*) FROM main.t;
1
```

Valid everywhere a table can appear, including as a three-part column reference:

```sql
SELECT archive.products.id FROM archive.products;
SELECT p.name FROM archive.products p;
INSERT INTO archive.products (id) VALUES (1);
SELECT * FROM "temp".widgets;          -- temp is a keyword, so it quotes
```

A bare name resolves to `main`, silently — which is what made the original bug invisible.

## Why omission, not the default

Reviewed independently by three models (`think`/fable, `glm-think`, `qwen-review`), each reading
the tree on its own. All three reached omission, on the same reasoning: **the default schema is
session state, and a dialect constant cannot stand in for session state.**

| Engine | If Jaunty hardcodes the default |
|---|---|
| SQL Server | A login with `DEFAULT_SCHEMA = sales` gets `dbo.orders`: "Invalid object name" on a table it can see with hand-written SQL, or a silent read of a leftover `dbo.orders`, or denial by a schema-scoped `GRANT` |
| PostgreSQL | `SET search_path = tenant_42, public` is the documented multi-tenant mechanism. Pinning `public.` sends **every tenant to the same physical tables** — silent cross-tenant data, not an exception |
| SQLite | `main.` is legal but changes semantics: a bare name resolves to a shadowing `temp` table first, `main.t` does not |
| MySQL | The connection already selected the database. Emitting it bakes the environment's database name into entity metadata, so dev to prod with a different name breaks |

The deciding case is mixed usage, which is ordinary rather than exotic:

```csharp
[Table("orders")]                     // login default schema resolves it -> sales.orders
[Table("audit_log", "audit")]         // named because it is not in the default
```

Under omission both work as the DBA intended. Under always-emit the first becomes `dbo.orders` and
the application is dead on arrival, or reads stale data. The PostgreSQL variant is worse: tenant
tables are mapped bare **because their schema varies per connection**, and metadata is built once
per `Type`, so there is no per-session seam an always-emit policy could hook into.

The house rule points the same way. Humans write `SELECT * FROM orders`.

## Considered and rejected

**Emit the default for determinism.** The strongest argument for it: `dbo.orders` names one object
regardless of who connects. Rejected because it overrides a server-side setting the DBA controls
with a constant the library guessed, and because it breaks the PostgreSQL tenant case outright.
Also, `PostgreSqlBulkCopyProvider.cs:306` already emits the table bare for COPY, so CRUD and bulk
would disagree.

**Delete `GetDefaultSchema()` since nothing calls it.** Rejected: `ISqlDialect` is public and
implemented outside this repo; in-tree there are the four `*DialectWithBulkCopy` wrappers plus test
dialects, and four test files pin the return values. A breaking removal for no functional gain.

**Give it a job in scaffolding** — emit the schema into `[Table]` only when it differs from the
default, so scaffolded SQL Server entities stop pinning `dbo`. Proposed by `glm-think`, and it
correctly identifies that scaffolding writes the default into every generated file. Rejected on
the argument `qwen-review` and fable reached independently: scaffolding records a physical fact
about where the table lives. Strip `dbo` and the generated entity resolves through the
*consumer's* default schema instead — the same silent wrong-table read, now baked into generated
source. Revisit only if scaffolded output portability becomes a real complaint.

**Make `SchemaNameResolver` dialect-aware.** `Func<Type,string>` is global and dialect-blind, so
`_ => "dbo"` already produced `dbo.products` on PostgreSQL before this change; SQLite joined the
club when it stopped discarding schemas. Rejected for now: metadata is built per type with no
connection in scope (`MetadataBuilder.cs:40`), so the dialect cannot be threaded in at resolution
time. The seam that has the dialect is later — `CrudSqlCache.cs:49`, `FluentMetadataCache.GetForDialect`
— and that is where a per-dialect override belongs if a real user ever needs one. Documented caveat
instead.

## Consequences

- No behaviour change: omission is what the code already did.
- SQLite honouring a supplied schema is a behaviour change, shipped in `35ca2f3f`. An application
  that set a global `SchemaNameResolver` for another engine and also opens SQLite now gets
  `no such table: dbo.products` where the schema used to be silently dropped. Loud, and documented
  in `docs/01-api-reference/schemas.md`.
- No cache is affected. `CrudSqlCache` keys on `(Type, connectionType)`, `MultiRowInsertCache` on
  `(entityType, connectionType, batchSize)`, `FluentMetadataCache` on `Type` and `(Type, dialectType)`;
  schema is a value inside the cached metadata and never part of a key. A grep for
  `SchemaName ==`/`.Equals` across `src/` returns nothing.
- `[Table("t")]` and `[Table("t","dbo")]` remain two entries producing two SQL strings against one
  physical table. Two CLR types were always two entries; the cost is a plan-cache entry on SQL Server.

## Open, not decided here

**`ImportCsv` on a file-backed SQLite database with a schema-qualified target.** The `sqlite3` CLI
is a separate process that has attached nothing, so it can reach only the file it was handed. A
`temp.`-qualified target exits 0 and imports zero rows while Jaunty reports a row count from the
CSV line count:

```
$ sqlite3 d2.db "CREATE TABLE people(id INTEGER, name TEXT);"
$ printf '.mode csv\n.import --skip 1 --schema temp "d1.csv" people\n' | sqlite3 d2.db
EXIT=0
$ sqlite3 d2.db "SELECT COUNT(*) FROM people;"
0
```

An unknown alias is loud (`unknown database "archive"`, exit 1). The candidate fix compares files
rather than names, because a second alias for the same file *is* reachable:

```csharp
int dot = tableName.IndexOf('.');
if (dot >= 0 && !CliCanReach(connection, tableName.Substring(0, dot), dbPath))
    return ImportViaPreparedStatements(connection, tableName, filePath, options);

return ImportViaSqliteCli(dbPath, tableName, filePath, options);
```

Tracked in the maintainer's tracker; awaiting an owner decision between routing to the prepared-statement
path, refusing the import, and replicating the caller's `ATTACH` into the CLI script.

## Follow-ups recorded, not done

- Four comments still assert SQLite has no schemas: `SQLiteSchemaReader.cs:128`,
  `Scaffolder.cs:208`, `EntityCodeGenerator.cs:133`, `TableSchema.cs:10`.
- Scaffolding reads `sqlite_master` only, so an attached database cannot be scaffolded.
- No test reaches the qualified-column-prefix branch in the Fluent join builders: alias inference
  supplies `a`/`m` or the `t1`/`t2` self-join fallback, so `archive.widgets.id` as a prefix is
  never emitted under test.
