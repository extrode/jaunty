# Feature candidates

Features Jaunty may add later. Nothing here is planned or promised. An entry records what the
feature would need and why it is not built yet, so the reasoning is not lost and a request can be
weighed against it.

To ask for one of these, or for something not listed, open an issue describing what you are
trying to do.

| Feature | Status | Asked for by |
|---|---|---|
| [Paged DELETE and UPDATE](#paged-delete-and-update) | Not planned | Nobody yet |
| [Per-connection naming](#per-connection-naming) | Not planned | Nobody yet |

---

## Paged DELETE and UPDATE

**What it would do:** make `From<Order>().OrderBy(o => o.CreatedAt).Take(5).Delete()` delete
those five rows, instead of refusing it.

**Today:** refused at compile time (CS0619), and at runtime for the one route the compiler cannot
see. The workaround, selecting keys and then deleting by key in one transaction, is in
[Writes after Take or Skip](../01-api-reference/fluent-api.md#writes-after-take-or-skip).

**What it would need:**
- A required `OrderBy`. Without one, "the first five rows" is whatever order the database happens
  to return, and the chain should not compile.
- A primary key on the entity, to write `WHERE key IN (SELECT key ... ORDER BY ... LIMIT n)`.
- Per-database SQL:
  - SQL Server: a CTE or a key subquery with `ORDER BY ... OFFSET ... FETCH`, since `DELETE TOP`
    takes no `ORDER BY`.
  - PostgreSQL and DuckDB: a key subquery.
  - SQLite: a key subquery, since `DELETE ... LIMIT` is a compile-time option most builds leave off.
  - MySQL and MariaDB: `DELETE ... ORDER BY ... LIMIT n` for `Take`. `Skip` has no form, because
    there is no offset on DELETE and MySQL rejects `LIMIT` inside an `IN (...)` subquery; a derived
    table works around that but changes the locking.
- Composite keys, which need a row-value `IN` or an `EXISTS` join and differ again per database.
- Tests for each database, both orders (`Take` then `Where`, `Where` then `Take`), and `Distinct`.

**Why not now:** it is new surface on every database for something nobody has asked for, and the
workaround is short and portable. Blocking it costs nothing that works today.

---

## Per-connection naming

**What it would do:** let two connections in one process map the same entity to different table
or column names, for example one schema per tenant.

**Today:** `JauntyConfig.TableNameResolver`, `SchemaNameResolver` and `ColumnNameResolver` are
process-wide. Changing one affects every connection, and the caches rebuild for everyone.

**What it would need:** a naming scope carried with the connection or the call (for example in
`CommandOptions`), cache keys that include it, and the same on the source-generated path, whose
name cache is keyed only by the configuration generation.

**Why not now:** nobody has asked for it, and a schema per tenant is usually handled by the
connection string or the database user's default schema, which needs nothing from Jaunty.
