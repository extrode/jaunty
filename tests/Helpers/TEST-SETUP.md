# Jaunty Test Database Setup Guide

This guide explains how to set up the databases required for running Jaunty integration tests.

## Quick Start

For **SQLite tests** (default, no setup required):
```bash
dotnet test
```

For **all database tests**, you'll need to set up SQL Server, PostgreSQL, and MySQL with the Northwind database.

---

## Database Requirements

### 1. SQLite (Required)
- **Database:** Northwind.db
- **Location:** `data/sqlite/Northwind.db`
- **Setup:** Already included in the repository
- **Tests:** ~877 tests

### 2. SQL Server (Optional)
- **Server:** localhost
- **Database:** Northwind
- **Authentication:** Windows Authentication (Trusted Connection)
- **Tests:** ~100 additional tests

### 3. PostgreSQL (Optional)
- **Host:** localhost
- **Port:** 5432
- **Database:** Northwind
- **Username:** postgres
- **Tests:** ~100 additional tests

### 4. MySQL / MariaDB (Optional)
- **Host:** localhost
- **Port:** 3306
- **Database:** Northwind
- **Username:** root
- **Tests:** ~100 additional tests

---

## Setup Instructions

### Step 1: Install Database Servers

#### SQL Server
1. Install [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (free)
2. Or use [Azure SQL Edge](https://hub.docker.com/_/microsoft-azure-sql-edge) (Docker)

#### PostgreSQL
1. Install from [postgresql.org](https://www.postgresql.org/download/)
2. Or use Docker: `docker run -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:15`

#### MySQL
1. Install from [mysql.com](https://dev.mysql.com/downloads/mysql/)
2. Or use Docker: `docker run -e MYSQL_ROOT_PASSWORD= -p 3306:3306 mysql:8`

### Step 2: Create Northwind Database

The bootstrap scripts are vendored — do not download a third-party Northwind
dump, as their column naming does not match the entity contracts.

| Server | Script | Load with |
|---|---|---|
| SQL Server | `data/sqlserver/create-northwind.sql` | `sqlcmd -S <server> -U sa -P <pwd> -C -i create-northwind.sql` (creates the database itself) |
| PostgreSQL | `data/postgres/create-northwind.sql` | `createdb northwind` first, then `psql -d northwind -f create-northwind.sql` |
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

### Step 3: Create Test Stored Procedures

**For SQL Server:**
```bash
# In SQL Server Management Studio:
USE Northwind;
GO
-- Run: tests/database-setup.sql
```

**For PostgreSQL:**
```bash
# Using psql:
psql -U postgres -f tests/postgres-setup.sql

# Or in pgAdmin: Run tests/postgres-setup.sql
```

**For MySQL:**
```bash
# Using mysql client:
mysql -u root -p < tests/mysql-setup.sql

# Or in MySQL Workbench: Run tests/mysql-setup.sql
```

The scripts create these stored procedures/functions:
- `GetAllProducts`
- `GetProductsByCategory`
- `GetProductById`
- `GetProductCount`
- `GetProductCountByCategory`
- `UpdateProductPrice`
- `GetProductCountWithOutput`
- `GetNoResults`

### Step 4: Configure Connection Strings

Edit `tests/Jaunty.Tests/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=Northwind;Trusted_Connection=true;TrustServerCertificate=true;",
    "PostgreSql": "Host=localhost;Port=5432;Database=Northwind;Username=postgres;Password=your_password",
    "MariaDb": "Server=localhost;Port=3306;Database=Northwind;Uid=root;Pwd=your_password"
  }
}
```

**Or** use environment variables:

```bash
# Windows
setx JAUNTY_TEST_SQLSERVER "Server=localhost;Database=Northwind;Trusted_Connection=true;"
setx JAUNTY_TEST_POSTGRESQL "Host=localhost;Database=Northwind;Username=postgres;Password=xxx"
setx JAUNTY_TEST_MARIADB "Server=localhost;Database=Northwind;Uid=root;Pwd=xxx"

# Linux/Mac
export JAUNTY_TEST_SQLSERVER="Server=localhost;Database=Northwind;..."
export JAUNTY_TEST_POSTGRESQL="Host=localhost;Database=Northwind;..."
export JAUNTY_TEST_MARIADB="Server=localhost;Database=Northwind;..."
```

---

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run SQLite Tests Only
```bash
dotnet test --filter "FullyQualifiedName~Sqlite"
```

### Run SQL Server Tests Only
```bash
dotnet test --filter "FullyQualifiedName~SqlServer"
```

### Run PostgreSQL Tests Only
```bash
dotnet test --filter "FullyQualifiedName~Postgres"
```

### Run MySQL Tests Only
```bash
dotnet test --filter "FullyQualifiedName~MySql"
```

---

## Troubleshooting

### Tests Are Skipped

If tests are being skipped, it means the connection strings are not configured. Check:

1. `appsettings.json` exists in the test output directory
2. Environment variables are set correctly
3. Database servers are running

### "Could not find stored procedure" Error

Run the `database-setup.sql` script on your database server to create the required stored procedures.

### "Unused parameter properties" Error

The parameter names in your anonymous object don't match the stored procedure parameter names. Check:
- SQL Server: `CategoryId`, `ProductId`, `NewPrice`
- PostgreSQL: `p_category_id`, `p_product_id`, `p_new_price`
- MySQL/MariaDB: `p_CategoryId`, `p_ProductId`, `p_NewPrice`

### Connection Refused

1. Verify the database server is running
2. Check firewall settings
3. Verify connection string is correct
4. For Docker containers, ensure ports are exposed

---

## Test Coverage Summary

With all four configured against the `docker-compose.yml` stack, the suite runs
11,569 passing / 493 skipped. The 493 are all `MicrosoftSqlite` on net472
(Microsoft.Data.Sqlite is deliberately not exercised on .NET Framework), so
there is nothing further to configure.

Unconfigured, the same suite skips 2,092 tests.

### Known environment limit

The three `CsvImportTests.*_SqlServer_*` tests use `BULK INSERT`, which reads the
CSV **server-side**, so what passes depends on where SQL Server runs:

| SQL Server | Result |
|---|---|
| Linux container (`docker-compose.yml`) | all 3 fail — the container cannot see the runner's filesystem at all |
| Local Windows instance | 2 of 3 pass; `ImportCsv_SqlServer_CustomQuote_AppliedNatively` still fails |

The remaining one stages its fixture via `WriteTempCsv` into
`Path.GetTempPath()` (the *user's* profile temp directory), which the SQL Server
service account cannot read — it surfaces as `Cannot obtain the required
interface ("IID_IColumnsInfo") from OLE DB provider "BULK"`. The other two read
`data/basic.csv` from the repo, which is readable. Fixing it means staging that
CSV somewhere the service account can read, rather than the user's temp dir.

---

## Docker Setup (Recommended for CI/CD)

For consistent testing across environments, use Docker:

```yaml
# docker-compose.test.yml
version: '3.8'
services:
  sqlserver:
    image: mcr.microsoft.com/azure-sql-edge:latest
    environment:
      ACCEPT_EULA: "1"
      MSSQL_SA_PASSWORD: "YourStrong@Passw0rd"
    ports:
      - "1433:1433"
  
  postgres:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
  
  mysql:
    image: mysql:8
    environment:
      MYSQL_ROOT_PASSWORD: ""
    ports:
      - "3306:3306"
```

Start containers:
```bash
docker-compose -f docker-compose.test.yml up -d
```

Then run the database setup scripts and tests.
