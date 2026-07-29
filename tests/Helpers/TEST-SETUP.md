# Jaunty Test Database Setup Guide

How to set up the databases the Jaunty integration tests run against.

> **On an Apple Silicon Mac, read [Apple Silicon](#apple-silicon-macos-arm64) first.** Three
> of the assumptions below do not hold there: SQLite needs a one-time build step, the SQL
> Server image has no arm64 build, and `dotnet test` without `-f net8.0` aborts the run.

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

## Apple Silicon (macOS arm64)

This guide was written from the Windows machine, where everything below is a non-issue.
Three things differ on an arm64 Mac. Measured 2026-07-29.

### 1. SQLite is not setup-free

`System.Data.SQLite.Core` ships no `osx-arm64` native, and its `osx-x64` one is plain
x86_64 Mach-O, so it cannot load into an arm64 process. Until this is built, **every test
touching `System.Data.SQLite` fails** — 1,099 of 1,099 failures in `Jaunty.Tests` and all
887 in `Jaunty.Fluent.Tests` had that single cause.

```bash
tools/native/sqlite-interop-osx-arm64/build.sh    # once per machine, ~1 min
```

See that directory's README for why it is more than a recompile. Nothing else on this page
changes, and CI is unaffected.

### 2. SQL Server has no arm64 image

Queried from the registries rather than assumed:

| Image | Platforms |
|---|---|
| `mcr.microsoft.com/mssql/server:2022-latest` | **linux/amd64 only** |
| `postgres:16` | linux/amd64, linux/arm64, +5 |
| `mysql:8` | linux/amd64, linux/arm64 |
| `mariadb:11` | linux/amd64, linux/arm64, +2 |

So `docker compose up -d` brings up three of the four natively. The `mssql` service needs
amd64 emulation — enable **Settings → General → Use Rosetta for x86_64/amd64 emulation on
Apple Silicon** in Docker Desktop, or point `JAUNTY_TEST_SQLSERVER` at a real instance
elsewhere.

Pointing it elsewhere is the better option anyway: the `CsvImportTests.*_SqlServer_*` tests
need a SQL Server that can see the runner's filesystem, which no container can — see
[Known environment limit](#known-environment-limit).

> Not yet verified: whether the emulated `mssql` container is *usable* here, only that it
> is the only one needing emulation. Docker Desktop is installed on this machine but its
> daemon was not running when this was written.

### 3. `dotnet test` at solution level aborts

`Jaunty.Tests` multi-targets `net8.0;net472`, and .NET Framework cannot host on macOS. The
net472 run does not fail — it **aborts the whole invocation**, so projects queued behind it
never execute and the summary looks short rather than broken.

```bash
dotnet test tests/Jaunty.Tests/Jaunty.Tests.csproj -f net8.0    # not `dotnet test`
```

That also means the Mac cannot cover net472 at all. The Windows machine is the only place
that TFM runs, which is worth remembering before concluding a change is verified.

### Measured baseline, unconfigured

With the SQLite native built and no connection strings set, on net8.0:

| | Passed | Failed | Skipped |
|---|---|---|---|
| `Jaunty.Tests` | 2,763 | 0 | 2,447 |
| `Jaunty.Fluent.Tests` | 1,182 | 0 | 0 |
| `Jaunty.Scaffolding.Tests` | 555 | 0 | 24 |
| `Jaunty.FlatFiles.DuckDB.Tests` | 578 | 0 | 0 |
| `Jaunty.FlatFiles.Tests` | 185 | 0 | 0 |
| `Jaunty.SourceGenerator.Tests` | 72 | 0 | 0 |
| `Jaunty.Fluent.SourceGen.Tests` | 9 | 0 | 8 |
| `Jaunty.Scaffolding.Cli.Tests` | 33 | **1** | 0 |

Of the 2,447 skips, 815 want `JAUNTY_TEST_POSTGRESQL` and 811 want `JAUNTY_TEST_SQLSERVER`.

The one failure is `ListTablesCommandTests.Invoke_ConnectionFailure_ReturnsErrorExitCodeAndMessage`
(expects exit 1, gets 0). It predates the SQLite work and is unrelated to it. Whether it is
macOS-specific has not been established — the coverage table below reports 34 passing for
that assembly on Windows, which suggests it may be, but that is one run on each platform and
not a conclusion.

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
at a **local** instance, the suite runs **12,426 passing / 0 failed / 0 skipped**:

| Assembly | net8.0 | net472 |
|---|---|---|
| Jaunty.Tests | 5,028 | 5,006 |
| Jaunty.Fluent.Tests | 1,157 | — |
| Jaunty.FlatFiles.DuckDB.Tests | 547 | — |
| Jaunty.Scaffolding.Tests | 436 | — |
| Jaunty.FlatFiles.Tests | 151 | — |
| Jaunty.SourceGenerator.Tests | 67 | — |
| Jaunty.Scaffolding.Cli.Tests | 34 | — |

Unconfigured — no environment variables, and `appsettings.json` either absent or blank — the
same suite runs **7,528 passing / 0 failed / 4,898 skipped**. The two totals agree:
7,528 + 4,898 = 12,426. The skips are 2,437 in each `Jaunty.Tests` TFM plus 24 in
`Jaunty.Scaffolding.Tests`.

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
