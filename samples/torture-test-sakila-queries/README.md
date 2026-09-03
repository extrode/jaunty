# Sakila/Pagila query runner (Part 2 torture test)

Fifteen non-trivial read queries (`Q01`..`Q15` in `Queries.cs`) run against the same logical
Sakila/Pagila schema on **five** targets, so the output of one dialect can be diffed against
another. This is the query half of Part 2 of
the private torture-test handoff; the
scaffolding half is [`torture-test-sakila-codegen`](../torture-test-sakila-codegen).

Results and what the diffs showed are recorded in
the private torture-test results; gaps found
are in the private torture-test gaps log.

## What it exercises

Multi-table joins, `GROUP BY` with aggregates pushed down to SQL, `LEFT JOIN ... IS NULL`
anti-joins, correlated subqueries, `DISTINCT`, paging, and per-dialect date truncation (`Q04`
takes a `Dialect` argument for exactly that reason). Between them the queries cover the
fluent read surface that the single-table samples do not reach.

Like [`NativeAOT-FluentQuery`](../NativeAOT-FluentQuery), this project **deliberately does not
reference `Jaunty.Extensions.Reflection`**. All 15 queries resolve source-generated entity
metadata reflection-free, with no `UseReflectionMapping()` call — that is spec 003 (gap #11)
closed, and the absence of that project reference is the assertion.

## Running it

```
dotnet run --project samples/torture-test-sakila-queries -- <sqlserver|postgres|mysql|mariadb|sqlite>
```

The four server dialects read their connection string from an environment variable and exit
with an error if it is unset:

| Argument | Variable |
|---|---|
| `sqlserver` | `JAUNTY_TORTURE_CONNSTR_MSSQL` |
| `postgres` | `JAUNTY_TORTURE_CONNSTR_POSTGRES` |
| `mysql` | `JAUNTY_TORTURE_CONNSTR_MYSQL` |
| `mariadb` | `JAUNTY_TORTURE_CONNSTR_MARIADB` |

`sqlite` takes no variable — it opens `data/sqlite/sakila.db` relative to the build output.
That file is gitignored (`/data/**/*.db`), like every other database fixture in the repo.

**None of the five works from a bare clone.** The schemas and data come from two external
gitignored clones and are multi-MB, so they are not vendored here.
[`seed/sakila/README.md`](../../seed/sakila/README.md) is the setup: which upstream repo feeds
which target, the container names and ports, and the load order.

## Reading the output

Each query prints a `== QNN Name ==` header followed by pipe-delimited rows, formatted through
the invariant-culture helpers at the bottom of `Program.cs` so that dates and decimals are
byte-comparable across dialects. Diff two runs directly:

```
dotnet run --project samples/torture-test-sakila-queries -- mysql   > /tmp/mysql.txt
dotnet run --project samples/torture-test-sakila-queries -- mariadb > /tmp/mariadb.txt
diff /tmp/mysql.txt /tmp/mariadb.txt
```

Differences that survive are either a genuine schema difference between the upstream ports —
Pagila is not a straight translation of MySQL's Sakila — or a Jaunty finding. The results doc
says which were which.
