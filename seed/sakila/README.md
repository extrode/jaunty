# Sakila/Pagila Seed Data (Part 2 torture test)

Schema+data for Part 2 (`docs/jaunty-torture-test-handoff.md`) come from two external,
gitignored clones under `torture-test/` (not vendored into this repo — multi-MB data files):

- `torture-test/sakila/` — `jOOQ/sakila` (BSD-2-Clause), used for MySQL, MariaDB, SQL Server,
  and SQLite. Each has a `<dialect>-sakila-db/` folder with a `*-schema.sql` and
  `*-insert-data.sql`.
- `torture-test/pagila/` — `devrimgunduz/pagila`, used for Postgres specifically (has the
  views/triggers/`tsvector` full-text search that MySQL's Sakila doesn't, needed for the
  Postgres-boundary stress test called out in the handoff doc).

## Databases created

| Target | Container/file | Database name | Loaded from |
|---|---|---|---|
| SQL Server | `torture-mssql` (localhost:1433) | `sakila` | `sql-server-sakila-db/sql-server-sakila-{schema,insert-data}.sql` |
| Postgres | `torture-postgres` (localhost:5433) | `pagila` | `pagila-schema.sql`, `pagila-insert-data.sql` |
| MySQL | `torture-mysql` (localhost:3308) | `sakila` | `mysql-sakila-db/mysql-sakila-{schema,insert-data}.sql` |
| MariaDB | `torture-mariadb` (localhost:3307) | `sakila` | same MySQL-sakila files (MariaDB is MySQL-compatible) |
| SQLite | `data/sqlite/sakila.db` | n/a (file) | `sqlite-sakila-db/sqlite-sakila-{schema,insert-data}.sql` |

Row counts verified identical across all 5 (actor 200, film 1000, customer 599, rental 16044,
payment 16049) after load.

## Reproducing

```
git clone --depth 1 https://github.com/jOOQ/sakila.git torture-test/sakila
git clone --depth 1 https://github.com/devrimgunduz/pagila.git torture-test/pagila
```

Then run each dialect's `*-schema.sql` followed by `*-insert-data.sql` against a freshly
created database of the name in the table above, using each engine's own CLI
(`sqlcmd`/`mysql`/`psql`/`sqlite3`).
