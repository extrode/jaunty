# Jaunty Test Database Setup Guide

How to set up the databases the Jaunty integration tests run against.

## Quick Start

SQLite needs no setup — the fixture is committed:

```bash
dotnet test
```

That leaves the four server dialects skipped. To run everything, bring up the
stack the repo already defines, seed it, and point the tests at it:

```bash
docker compose up -d                                # four containers
pwsh scripts/reset-test-databases.ps1 --execute     # seeds all four from data/
```

Then export the four connection strings (see [Step 4](#step-4-configure-connection-strings))
and run `dotnet test` again.

**Reset the databases after every run.** The tests mutate what they run against —
write tests insert and update rows, `DialectFixture` creates its own tables, and the
SQLite fixture is edited in place. `scripts/reset-test-databases.ps1 --execute` drops
and recreates each database, then re-seeds. Re-seeding alone is not enough: the seed
scripts only recreate the 13 Northwind tables, so test-created ones (`bulk_*`,
`csv_import_test`, `execute_test`, `get_test`, `scaffold_test_*`) would survive.

---

## Database Requirements

Ports below are what `docker-compose.yml` publishes. **They are not the defaults** —
Postgres and MySQL are deliberately remapped so the stack cannot collide with a server
already installed on the machine. Using 5432 or 3306 silently reaches the wrong server.

| Dialect | Container | Host port | Database | User |
|---|---|---|---|---|
| SQLite | — | — | `data/sqlite/Northwind.db` | — |
| SQL Server | `torture-mssql` | **1433** | `Northwind` | `sa` |
| PostgreSQL | `torture-postgres` | **5433** | `northwind` | `postgres` |
| MySQL | `torture-mysql` | **3308** | `northwind` | `root` |
| MariaDB | `torture-mariadb` | **3307** | `northwind` | `root` |

The container password is `Torture_Test_Pwd1!` for all four, set in `docker-compose.yml`.

---

## Setup Instructions

### Step 1: Start the database servers

Use the repo's own stack rather than ad-hoc containers — it fixes the ports, passwords
and health checks that the rest of this guide assumes:

```bash
docker compose up -d
docker compose ps          # wait for all four to report healthy
```

A locally installed SQL Server works too, and is the only way to run the `BULK INSERT`
tests (see [Known environment limit](#known-environment-limit)).

### Step 2: Create the Northwind schema

The bootstrap scripts are vendored — do not download a third-party Northwind dump, as
their column naming does not match the entity contracts.

| Server | Script | Load with |
|---|---|---|
| SQL Server | `data/sqlserver/create-northwind.sql` | `sqlcmd -S <server> -U sa -P <pwd> -C -b -i create-northwind.sql` (creates the database itself) |
| PostgreSQL | `data/postgres/create-northwind.sql` | `createdb northwind` first, then `psql -d northwind -v ON_ERROR_STOP=1 -f create-northwind.sql` |
| MySQL / MariaDB | `data/mysql/create-northwind.sql` | `CREATE DATABASE northwind;` first, then `mysql northwind < create-northwind.sql` |

All three are generated from `data/sqlite/Northwind.db` so every dialect carries
identical data. Regenerate after changing that reference database:

```bash
python scripts/generate-sqlserver-northwind.py   # SQL Server
python scripts/generate-northwind.py             # PostgreSQL + MySQL/MariaDB
```

Each table carries both namings: snake_case columns as declared, plus PascalCase
generated columns bridging them, because the integration entities read PascalCase
field names out of the reader while their `[Column]` attributes name snake_case.

Pass `-b` to `sqlcmd` whenever you script this. Without it sqlcmd exits **0** even when
the statement failed, so a half-applied schema looks like a success.

### Step 3: Create the test stored procedures

| Server | Script |
|---|---|
| SQL Server | `data/sqlserver/create-stored-procedures.sql` |
| PostgreSQL | `data/postgres/create-stored-procedures.sql` |
| MySQL / MariaDB | `data/mysql/create-stored-procedures.sql` |

These create `GetAllProducts`, `GetProductsByCategory`, `GetProductById`,
`GetProductCount`, `GetProductCountByCategory`, `UpdateProductPrice`,
`GetProductCountWithOutput` and `GetNoResults`.

The PostgreSQL and MySQL versions alias their result columns to PascalCase
(`SELECT product_id AS ProductId, ...`, and quoted `RETURNS TABLE` column names on
Postgres). That is load-bearing: `Product.ReadEntity` looks up `ProductId`, not
`product_id`, so an unaliased procedure returns rows that cannot be mapped.

> `tests/database-setup.sql`, `tests/postgres-setup.sql` and `tests/mysql-setup.sql`
> are an older, divergent set of the same procedures. The `data/` scripts above are the
> ones `scripts/reset-test-databases.ps1` uses and the ones the green suite was verified
> against. Prefer them.

### Step 4: Configure connection strings

Environment variables take priority over `appsettings.json`:

```bash
export JAUNTY_TEST_SQLSERVER="Server=localhost,1433;Database=Northwind;User Id=sa;Password=Torture_Test_Pwd1!;TrustServerCertificate=true;"
export JAUNTY_TEST_POSTGRESQL="Host=localhost;Port=5433;Database=northwind;Username=postgres;Password=Torture_Test_Pwd1!"
export JAUNTY_TEST_MYSQL="Server=localhost;Port=3308;Database=northwind;Uid=root;Pwd=Torture_Test_Pwd1!;SslMode=Disabled;AllowLoadLocalInfile=true"
export JAUNTY_TEST_MARIADB="Server=localhost;Port=3307;Database=northwind;Uid=root;Pwd=Torture_Test_Pwd1!;SslMode=Disabled;AllowLoadLocalInfile=true"
```

Notes on the MySQL/MariaDB string:

- `SslMode=Disabled` — MySqlConnector's enum has no `None`; that value throws
  `Requested value 'None' was not found`.
- `AllowLoadLocalInfile=true` — required by the `LOAD DATA LOCAL INFILE` import tests.

Or use a settings file. `appsettings.json` is gitignored, so a fresh checkout has none —
copy the committed template:

```bash
cp tests/Jaunty.Tests/appsettings.example.json tests/Jaunty.Tests/appsettings.json
cp tests/Jaunty.Scaffolding.Tests/appsettings.example.json tests/Jaunty.Scaffolding.Tests/appsettings.json
```

**Fill in the passwords, or empty the string entirely.** An empty connection string makes
that dialect's tests **skip**; a non-empty one that cannot connect makes them **fail**.
The template ships with passwords blank, so copying it without editing turns 1,584
skips into failures. A checkout with no `appsettings.json` at all skips 4,791.

---

## Running Tests

```bash
dotnet test                                              # everything, both TFMs
dotnet test --filter "FullyQualifiedName~Sqlite"         # SQLite only
dotnet test --filter "FullyQualifiedName~SqlServer"      # SQL Server only
dotnet test --filter "FullyQualifiedName~Postgres"       # PostgreSQL only
dotnet test --filter "FullyQualifiedName~MySql"          # MySQL only
```

---

## Troubleshooting

### Tests are skipped

Connection strings are not configured. Check the environment variables are exported in
the shell running `dotnet test`, that `appsettings.json` reached the test output
directory, and that the containers are healthy (`docker compose ps`).

### "database northwind does not exist" — but it does

You are on the wrong port. `docker-compose.yml` publishes PostgreSQL on **5433** and
MySQL on **3308**, not 5432/3306. Those defaults reach whatever else is installed
locally, which will not have a `northwind`.

### "Could not find stored procedure"

Run the Step 3 script for that server.

### "Field not found in row: ProductId"

The stored procedures were created from an unaliased script. Re-run the `data/` version
from Step 3.

### "Unused parameter properties"

Parameter names in the anonymous object do not match the procedure's:

- SQL Server: `CategoryId`, `ProductId`, `NewPrice`
- PostgreSQL: `p_category_id`, `p_product_id`, `p_new_price`
- MySQL/MariaDB: `p_CategoryId`, `p_ProductId`, `p_NewPrice`

### Connection refused

Server not running, firewall, wrong port, or the container's port is not published.

---

## Test Coverage Summary

With all four configured against the `docker-compose.yml` stack and SQL Server pointed
at a **local** instance, the suite runs **12,207 passing / 0 failed / 0 skipped**:

| Assembly | net8.0 | net472 |
|---|---|---|
| Jaunty.Tests | 5,028 | 4,787 |
| Jaunty.Fluent.Tests | 1,157 | — |
| Jaunty.FlatFiles.DuckDB.Tests | 547 | — |
| Jaunty.Scaffolding.Tests | 436 | — |
| Jaunty.FlatFiles.Tests | 151 | — |
| Jaunty.SourceGenerator.Tests | 67 | — |
| Jaunty.Scaffolding.Cli.Tests | 34 | — |

Unconfigured — no `appsettings.json` and no environment variables — the same suite runs
**7,416 passing / 0 failed / 4,791 skipped**. The two totals agree: 7,416 + 4,791 = 12,207.

### Known environment limit

The `CsvImportTests.*_SqlServer_*` tests use `BULK INSERT`, which reads the CSV
**server-side**, so the file has to be reachable from wherever SQL Server runs:

| SQL Server | Result |
|---|---|
| Linux container (`docker-compose.yml`) | fail — the container cannot see the runner's filesystem at all |
| Local Windows instance | pass |

Run them against a local instance by pointing `JAUNTY_TEST_SQLSERVER` at it:

```bash
JAUNTY_TEST_SQLSERVER="Server=lpc:localhost;Database=NorthwindJaunty;Trusted_Connection=true;TrustServerCertificate=true;" \
  dotnet test tests/Jaunty.Tests/Jaunty.Tests.csproj -f net8.0 --filter "FullyQualifiedName~CsvImportTests"
```

The fixtures are staged under the test output directory (inside the repo), not the OS
temp dir, precisely so a container that bind-mounts the workspace can still see them.
