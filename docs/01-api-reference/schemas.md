# Schemas

How each engine understands a schema, and what Jaunty emits.

## The one rule

**Jaunty emits a schema when you name one, and nothing when you do not.** It never substitutes a
default. The unqualified name is the engine's own indirection mechanism, and every engine has one.

```csharp
[Table("orders")]                    // SELECT ... FROM orders
[Table("orders", "sales")]           // SELECT ... FROM sales.orders
```

## Per engine

| Engine | Conventional default | What it means | What resolves a bare name |
|---|---|---|---|
| SQL Server | `dbo` | A namespace inside a database, owned by a principal | The **login's** `DEFAULT_SCHEMA`, then `dbo` |
| PostgreSQL | `public` | A namespace inside a database | `search_path`, per session |
| MySQL / MariaDB | none | Schema and database are the same thing | The database the connection selected |
| SQLite | none | An **attached database**, not a namespace | `main` |
| DuckDB | `main` | A namespace inside a database | `main` |

### SQL Server

`dbo` is the server's convention, not a guarantee. A login can be given another default:

```sql
CREATE USER app_user FOR LOGIN app_login WITH DEFAULT_SCHEMA = sales;
```

For that login, bare `orders` resolves to `sales.orders` first and falls back to `dbo.orders`.
If Jaunty emitted `dbo.orders` it would either fail on a table the user can see with hand-written
SQL, read a different table that happens to exist, or be denied by a schema-scoped `GRANT`.

### PostgreSQL

`search_path` is the documented mechanism for schema-per-tenant and per-role selection:

```sql
SET search_path = tenant_42, public;
```

Tenant tables are mapped **without** a schema on purpose, because the correct schema varies per
connection, not per type. Emitting `public.` would send every tenant to the same physical tables.

```csharp
[Table("events")]                      // resolved per session by search_path
[Table("tenants", "admin")]            // fixed, shared across tenants
```

### MySQL / MariaDB

There is no `dbo`/`public` equivalent: `CREATE SCHEMA` and `CREATE DATABASE` are synonyms, and
tables live directly in that one namespace. The connection string already selected it, so Jaunty
emits nothing. Naming a schema is how you reach **another database**:

```csharp
[Table("archive_orders", "reporting")]   // SELECT ... FROM reporting.archive_orders
```

### SQLite

SQLite's schema names are attached databases. The set is `main`, `temp`, and every `ATTACH`
alias — and the alias is whatever you choose:

```sql
ATTACH DATABASE 'archive.db' AS archive;
ATTACH DATABASE 'reports.db' AS master;    -- any name; nothing is reserved
```

```csharp
[Table("widgets", "archive")]
public partial class ArchivedWidget
{
    [Key][Column("id")] public long Id { get; set; }
    [Column("name")]    public string Name { get; set; } = string.Empty;
}
```

```sql
SELECT id, name FROM archive.widgets
INSERT INTO archive.widgets (id, name) VALUES (@id, @name)
UPDATE archive.widgets SET name = @name WHERE id = @id
DELETE FROM archive.widgets WHERE id = @id
```

Three facts that follow from schemas being attached databases:

**The schema set belongs to the connection, not the file.** Ask for a schema this connection has
not attached and SQLite says so, for example
`SQLite Error 1: 'no such table: archive.widgets'` from Microsoft.Data.Sqlite. Re-issue `ATTACH`
on every connection you open; pooling does not carry it.

**A bare name resolves to `main`, silently.** There is no error to catch. This is why Jaunty
qualifies when you name a schema, and why it does not add `main.` when you do not.

**A keyword schema is quoted.** `temp` is a SQLite keyword, so:

```csharp
[Table("scratch", "temp")]   // SELECT ... FROM "temp".scratch
```

Grep your logs for `"temp".scratch`, not `temp.scratch`.

> Before 2026-09-02 the SQLite dialect discarded the schema, on the belief that SQLite had no
> schemas. Nothing failed when it did: the statement went out unqualified, SQLite resolved it
> against `main`, and reads returned another table's rows while writes landed in another file. If
> you pinned an earlier version and mapped an entity to a schema, check which database it wrote to.

## Mixing default and named schemas

The ordinary case, and it works because Jaunty omits rather than guesses:

```csharp
[Table("orders")]                     // wherever the session resolves it
public partial class Order { }

[Table("audit_log", "audit")]         // pinned
public partial class AuditLog { }
```

```sql
SELECT ... FROM orders
SELECT ... FROM audit.audit_log
```

## `JauntyConfig.SchemaNameResolver`

The resolver is **global and dialect-blind**. It receives a `Type` and nothing else, so one
resolver serves every connection in the process:

```csharp
JauntyConfig.SchemaNameResolver = _ => "dbo";     // applies to SQLite too
```

That emits `dbo.products` on SQLite and PostgreSQL as well as SQL Server. Scope it by type, and
return `string.Empty` for entities that should stay unqualified:

```csharp
JauntyConfig.SchemaNameResolver = t =>
    t.Namespace?.StartsWith("App.Sqlite", StringComparison.Ordinal) == true
        ? string.Empty
        : "dbo";
```

It is consulted on the reflection mapping path only. Source-generated entities read the `[Table]`
attribute at compile time and ignore it. A `[Table]` schema wins over the resolver either way.

## `ISqlDialect.GetDefaultSchema()`

Informational. It reports the engine's conventional default (`dbo`, `public`, `main`, or empty)
and **Jaunty never emits it**. The effective default is session state — the login's default schema,
`search_path`, the connection's ATTACH set — so any constant substituted for it is wrong for some
session, silently so when a same-named table exists elsewhere.

## CSV import on SQLite

`ImportCsv` on a file-backed SQLite database shells out to the `sqlite3` CLI, which is a separate
process holding its own connection. It has attached nothing, so it can reach only the file it was
given: your `temp` tables and your ATTACHed databases do not exist for it.

Jaunty checks the target against your connection before choosing a path, and imports through
prepared statements instead whenever the CLI would reach a different table. The check covers the
unqualified case too, because a bare name is where the two disagree most quietly:

```csharp
connection.Execute("CREATE TEMP TABLE people (id INTEGER, name TEXT)");

// "people" is temp.people to this connection, and would be a new main.people to the CLI.
// Jaunty takes the prepared-statement path, and the rows land in temp.people.
connection.ImportCsv("people", "people.csv");
```

The routing rule, in full:

| Target | Path taken |
|---|---|
| Names a schema backed by the same file the CLI was given | CLI, with the alias rewritten to `main` |
| Names any other schema — `temp`, or an ATTACHed file | Prepared statements |
| Unqualified, and your connection resolves it to `main` | CLI |
| Unqualified, and your connection resolves it to `temp` or an attachment | Prepared statements |
| Unqualified, and nothing of that name exists anywhere | CLI, which creates the table from the CSV header |
| In-memory database | Prepared statements |

Both paths import the same rows; the prepared-statement path is slower on large files.

A closed connection is opened for the check, and the import runs inside that same open. Both
halves matter. A pooled `Close()` does not discard temp tables or the ATTACH set — a handle from
the pool comes back with them intact — so "closed" does not mean "nothing attached". And the pool
is FIFO, so checking on one open and importing on the next would take two different handles, with
the rows landing in whatever the second one called the table.

## See also

- [`attributes.md`](attributes.md) — `[Table]` and the rest of the mapping attributes
- [`configuration.md`](configuration.md) — `SchemaNameResolver` and the other resolvers
- `docs/decisions/2026-09-02-009-schema-qualification-policy.md` — why the policy is omission
