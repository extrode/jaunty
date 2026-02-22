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
- **Database:** northwind
- **Username:** postgres
- **Tests:** ~100 additional tests

### 4. MySQL (Optional)
- **Host:** localhost
- **Port:** 3306
- **Database:** northwind
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

Download and run the Northwind database script for your database server:

- **SQL Server:** [Northwind for SQL Server](https://github.com/microsoft/sql-server-samples/tree/master/samples/databases/northwind)
- **PostgreSQL:** [Northwind for PostgreSQL](https://github.com/pthom/northwind_psql)
- **MySQL:** [Northwind for MySQL](https://github.com/jpwhite3/northwind-MySQL)

### Step 3: Create Test Stored Procedures

Run the setup script to create required stored procedures:

```bash
# SQL Server (using sqlcmd)
sqlcmd -S localhost -d Northwind -i tests/database-setup.sql

# Or in SQL Server Management Studio:
# Open tests/database-setup.sql and execute
```

The script creates these stored procedures:
- `GetAllProducts()` - Returns all products
- `GetProductsByCategory(@CategoryId)` - Returns products by category
- `GetProductCount()` - Returns product count
- `GetCategoryWithProductCount(@CategoryId, @ProductCount OUTPUT)` - Returns category with output parameter

### Step 4: Configure Connection Strings

Edit `tests/Jaunty.Tests/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=Northwind;Trusted_Connection=true;TrustServerCertificate=true;",
    "PostgreSql": "Host=localhost;Port=5432;Database=northwind;Username=postgres;Password=your_password",
    "MySql": "Server=localhost;Port=3306;Database=northwind;Uid=root;Pwd=your_password"
  }
}
```

**Or** use environment variables:

```bash
# Windows
setx JAUNTY_TEST_SQLSERVER "Server=localhost;Database=Northwind;Trusted_Connection=true;"
setx JAUNTY_TEST_POSTGRESQL "Host=localhost;Database=northwind;Username=postgres;Password=xxx"
setx JAUNTY_TEST_MYSQL "Server=localhost;Database=northwind;Uid=root;Pwd=xxx"

# Linux/Mac
export JAUNTY_TEST_SQLSERVER="Server=localhost;Database=Northwind;..."
export JAUNTY_TEST_POSTGRESQL="Host=localhost;Database=northwind;..."
export JAUNTY_TEST_MYSQL="Server=localhost;Database=northwind;..."
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
- SQL Server: Parameters are `@CategoryId`, `@ProductName`, etc.
- PostgreSQL: Parameters are `p_category_id`, `p_product_name`, etc.
- MySQL: Parameters are `p_CategoryId`, `p_ProductName`, etc.

### Connection Refused

1. Verify the database server is running
2. Check firewall settings
3. Verify connection string is correct
4. For Docker containers, ensure ports are exposed

---

## Test Coverage Summary

| Database | Tests | Status |
|----------|-------|--------|
| SQLite | ~877 | Always runs |
| SQL Server | ~100 | Requires setup |
| PostgreSQL | ~100 | Requires setup |
| MySQL | ~100 | Requires setup |
| **Total** | **~1177** | |

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
