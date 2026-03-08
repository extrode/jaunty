# Test Reorganization Plan — Dialect Attribute Architecture

## Goal

Reorganize `Jaunty.Tests` so that cross-dialect tests mirror `src/Jaunty/`'s directory structure, each test method carries dialect attributes, and each dialect shows as a **separate entry** in Test Explorer.

## Architecture

### Pattern: `[Theory]` + Dialect `DataAttribute` Subclasses

```csharp
[Theory]
[SqlServer]
[MicrosoftSqlite]
[SystemSqlite]
[MariaDB]
[Postgres]
public void Query_ReturnsResults(DialectInfo dialect)
{
    using var connection = _fixture.GetConnection(dialect);
    var results = connection.Query<Product>("...");
    Assert.NotEmpty(results);
}
```

- Each dialect attribute is a `DataAttribute` subclass that yields `new object[] { new DialectInfo(...) }`
- If the DB is unavailable, the attribute sets `Skip` → test shows as skipped in Test Explorer
- `DialectInfo.ToString()` → `"SqlServer"` / `"SystemSqlite"` etc. for Test Explorer display names
- For async tests without SQLite support: simply omit `[MicrosoftSqlite]` and `[SystemSqlite]`

### Connection Setup: `IClassFixture<DialectFixture>`

```csharp
public class QueryTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;
    public QueryTests(DialectFixture fixture) => _fixture = fixture;
}
```

`DialectFixture` opens connections once per class, caches them, disposes on teardown.

### Write Test Isolation: `IClassFixture<WriteDialectFixture>`

- **SystemSqlite / MicrosoftSqlite**: In-memory DB with schema + seed data (fresh per class)
- **SqlServer / Postgres / MariaDB**: `CommandOptions.WithTransaction()` + `Rollback()` in `Dispose()`

---

## Target Directory Structure

```
tests/Jaunty.Tests/
├── Helpers/
│   ├── Dialects/
│   │   ├── DialectInfo.cs              # Wrapper: Name, Provider enum, ToString()
│   │   ├── DialectFixture.cs           # IClassFixture — read-only connections
│   │   ├── WriteDialectFixture.cs      # IClassFixture — write isolation
│   │   ├── SqlServerAttribute.cs       # DataAttribute → DialectInfo
│   │   ├── PostgresAttribute.cs        # DataAttribute → DialectInfo
│   │   ├── MariaDBAttribute.cs         # DataAttribute → DialectInfo
│   │   ├── MicrosoftSqliteAttribute.cs # DataAttribute → DialectInfo (net8.0 only)
│   │   └── SystemSqliteAttribute.cs    # DataAttribute → DialectInfo
│   ├── Database.cs                     # (keep for backward compat during migration)
│   ├── TestConfiguration.cs            # (add MariaDB/MySQL support)
│   └── ...existing helpers...
├── Integration/
│   ├── Read/                           # Mirrors src/Jaunty/Read/
│   │   ├── QueryTests.cs              # [Theory] + dialect attrs
│   │   ├── QueryAsyncTests.cs         # omit SQLite attrs
│   │   ├── QueryFirstTests.cs
│   │   ├── QueryFirstAsyncTests.cs
│   │   ├── QuerySingleTests.cs
│   │   ├── QuerySingleAsyncTests.cs
│   │   ├── QueryScalarTests.cs
│   │   ├── QueryScalarAsyncTests.cs
│   │   ├── ExecuteScalarTests.cs
│   │   ├── ExecuteScalarAsyncTests.cs
│   │   └── ...all other Read/ tests
│   ├── Write/                          # Mirrors src/Jaunty/Write/
│   │   ├── InsertTests.cs
│   │   ├── InsertAsyncTests.cs
│   │   ├── UpdateTests.cs
│   │   ├── DeleteTests.cs
│   │   ├── UpsertTests.cs
│   │   ├── BulkInsertTests.cs
│   │   └── ...all other Write/ tests
│   ├── Multiple/                       # Mirrors src/Jaunty/Multiple/
│   │   ├── GridReaderTests.cs
│   │   ├── GridReaderAsyncTests.cs
│   │   ├── QueryMultipleTests.cs
│   │   └── QueryMultipleAsyncTests.cs
│   ├── Streaming/                      # Mirrors src/Jaunty/Streaming/
│   │   ├── QueryPartialStreamTests.cs
│   │   ├── QueryPartialStreamAsyncTests.cs
│   │   ├── QueryPartialUnbufferedTests.cs
│   │   └── QueryPartialUnbufferedAsyncTests.cs
│   ├── Configuration/                  # Cross-dialect config tests
│   │   └── ConfigurationTests.cs
│   ├── StoredProcedure/                # Stays dialect-specific (SQL differences too large)
│   │   ├── StoredProcedureTestBase.cs
│   │   └── StoredProcedureAsyncTestBase.cs
│   ├── SqlServer/                      # Dialect-specific tests only
│   │   └── StoredProcedure/
│   ├── Postgres/                       # Dialect-specific tests only
│   │   └── StoredProcedure/
│   ├── MySql/                          # Dialect-specific tests only
│   │   └── StoredProcedure/
│   └── Sqlite/                         # Dialect-specific tests only (if any remain)
│       └── Configuration/
```

---

## Implementation Phases

### Phase 1: Infrastructure (new files)

**Step 1.1** — Create `Helpers/Dialects/` directory with 8 new files:

1. **`DialectInfo.cs`** — Enum `DialectProvider { SystemSqlite, MicrosoftSqlite, SqlServer, Postgres, MariaDB }` + wrapper class with `Name`, `Provider`, `ConnectionString`, `ToString()`.

2. **`DialectFixture.cs`** — `IDisposable` class. Lazy-opens connections per `DialectProvider`. `GetConnection(DialectInfo)` returns an open `IDbConnection`. Creates:
   - `System.Data.SQLite.SQLiteConnection` for SystemSqlite (Northwind.db path)
   - `Microsoft.Data.Sqlite.SqliteConnection` for MicrosoftSqlite (Northwind.db path) — `#if NET8_0_OR_GREATER`
   - `SqlConnection` for SqlServer
   - `NpgsqlConnection` for Postgres
   - `MySqlConnection` for MariaDB

3. **`WriteDialectFixture.cs`** — Like `DialectFixture` but for write tests:
   - SQLite variants: in-memory DB with schema + seed data
   - Server DBs: wraps connection in transaction, rollback on dispose

4. **`SqlServerAttribute.cs`** — `DataAttribute` subclass:
   ```csharp
   public class SqlServerAttribute : DataAttribute
   {
       public override IEnumerable<object[]> GetData(MethodInfo method)
       {
           if (!TestConfiguration.HasSqlServer)
               yield break; // or set Skip
           yield return new object[] { DialectInfo.SqlServer };
       }
       public override string Skip => TestConfiguration.HasSqlServer ? null : "SQL Server not available";
   }
   ```

5. **`PostgresAttribute.cs`** — Same pattern for Postgres
6. **`MariaDBAttribute.cs`** — Same pattern for MariaDB/MySQL
7. **`MicrosoftSqliteAttribute.cs`** — Always available on net8.0, skipped on netstandard2.0
8. **`SystemSqliteAttribute.cs`** — Always available (System.Data.SQLite)

**Step 1.2** — Update `TestConfiguration.cs`:
- Add `MariaDbConnectionString`, `HasMariaDb`, `JAUNTY_TEST_MARIADB` env var (if not already present)

### Phase 2: File Moves via PowerShell Script

**Script**: `scripts/Reorganize-Tests.ps1`

The script will:
1. Create target directories (`Integration/Read/`, `Integration/Write/`, `Integration/Multiple/`, `Integration/Streaming/`)
2. `git mv` files from `Integration/Sqlite/Read/*.cs` → `Integration/Read/`
3. `git mv` files from `Integration/Sqlite/Write/*.cs` → `Integration/Write/`
4. `git mv` files from `Integration/Sqlite/Multiple/*.cs` → `Integration/Multiple/`
5. `git mv` files from `Integration/Sqlite/Streaming/*.cs` → `Integration/Streaming/`
6. Update namespaces in moved files (replace `Integration.Sqlite.Read` → `Integration.Read`, etc.)
7. Remove empty `Integration/Sqlite/Read/`, `Integration/Sqlite/Write/`, etc. directories
8. Keep `Integration/Sqlite/Configuration/` and `Integration/Sqlite/StoredProcedure/` in place (dialect-specific)

**Files moved (~75 files)**:
- `Integration/Sqlite/Read/` → `Integration/Read/` (26+ files)
- `Integration/Sqlite/Write/` → `Integration/Write/` (15+ files)
- `Integration/Sqlite/Multiple/` → `Integration/Multiple/` (8+ files)
- `Integration/Sqlite/Streaming/` → `Integration/Streaming/` (6+ files)

### Phase 3: Rewrite Test Classes

For each moved test class, transform from:
```csharp
// OLD
public class QueryTests : IDisposable
{
    private readonly Database _db = new();

    [Fact]
    public void Query_ReturnsResults()
    {
        var results = _db.Connection.Query<Product>(...);
        Assert.NotEmpty(results);
    }

    public void Dispose() => _db.Dispose();
}
```

To:
```csharp
// NEW
public class QueryTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;
    public QueryTests(DialectFixture fixture) => _fixture = fixture;

    [Theory]
    [SqlServer]
    [MicrosoftSqlite]
    [SystemSqlite]
    [MariaDB]
    [Postgres]
    public void Query_ReturnsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<Product>(...);
        Assert.NotEmpty(results);
    }
}
```

**Dialect-specific SQL differences** handled via helper methods on `DialectInfo`:
- `dialect.SelectTopN(5, "products")` → `SELECT TOP 5 ...` (SqlServer) vs `SELECT ... LIMIT 5` (others)
- `dialect.CountReturnType` → `typeof(int)` (SqlServer) vs `typeof(long)` (others)
- `dialect.ParameterPrefix` → `@` (SqlServer/SQLite) vs empty/named (Postgres)

**Write tests** use `WriteDialectFixture` instead of `DialectFixture`:
```csharp
public class InsertTests : IClassFixture<WriteDialectFixture>
{
    [Theory]
    [SqlServer]
    [SystemSqlite]
    [MariaDB]
    [Postgres]
    public void Insert_SingleEntity_Succeeds(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        // ctx.Connection + ctx.CommandOptions (with transaction)
        var result = ctx.Connection.Insert(entity, ctx.CommandOptions);
        Assert.True(result > 0);
        // Transaction rolled back on ctx.Dispose()
    }
}
```

### Phase 4: Absorb Provider-Specific Core Tests

Current provider-specific tests under `Integration/SqlServer/`, `Integration/Postgres/`, `Integration/MySql/` that duplicate SQLite test logic get **deleted** — their coverage is now handled by dialect attributes on the shared tests.

**Keep dialect-specific**:
- `StoredProcedure/` tests (SQL syntax varies too much between providers)
- `Sqlite/Configuration/` tests (SQLite-specific config behavior)

### Phase 5: Cleanup & Documentation

1. Remove empty directories left after moves
2. Remove obsolete helper classes:
   - `SkipIfNoSqlServerAttribute.cs` → replaced by `SqlServerAttribute`
   - `SkipIfNoPostgresAttribute.cs` → replaced by `PostgresAttribute`
   - `SkipIfNoMySqlAttribute.cs` → replaced by `MariaDBAttribute`
   - `SkipSQLiteAsyncAttribute.cs` → no longer needed (omit SQLite attrs)
   - `SQLiteTestBase.cs` → replaced by `DialectFixture`
3. Remove `Database.cs` if no remaining references
4. Update the coding standards with new architecture

---

## SQL Dialect Differences Reference

| Feature | SqlServer | Postgres | MariaDB | SystemSqlite | MicrosoftSqlite |
|---------|-----------|----------|---------|--------------|-----------------|
| Top N | `SELECT TOP N ...` | `LIMIT N` | `LIMIT N` | `LIMIT N` | `LIMIT N` |
| COUNT type | `int` | `long` | `long` | `long` | `long` |
| Identity insert | `SCOPE_IDENTITY()` | `RETURNING id` | `LAST_INSERT_ID()` | `last_insert_rowid()` | `last_insert_rowid()` |
| Param prefix | `@` | `@` | `@` | `@` | `@` |
| Async DataReader | Yes | Yes | Yes | **No** | **No** |
| Stored Procedures | Yes | Yes (functions) | Yes | No | No |

---

## Data Isolation Rules

| Dialect | Read Tests | Write Tests |
|---------|-----------|-------------|
| SystemSqlite | Northwind.db file | In-memory DB (`:memory:`) |
| MicrosoftSqlite | Northwind.db file | In-memory DB (`:memory:`) |
| SqlServer | Direct connection | Transaction + Rollback |
| Postgres | Direct connection | Transaction + Rollback |
| MariaDB | Direct connection | Transaction + Rollback |

---

## File Manifest

### New Files (10)
| File | Purpose |
|------|---------|
| `Helpers/Dialects/DialectInfo.cs` | Enum + wrapper with dialect metadata |
| `Helpers/Dialects/DialectFixture.cs` | Read-only connection fixture |
| `Helpers/Dialects/WriteDialectFixture.cs` | Write-isolated connection fixture |
| `Helpers/Dialects/SqlServerAttribute.cs` | DataAttribute for SQL Server |
| `Helpers/Dialects/PostgresAttribute.cs` | DataAttribute for PostgreSQL |
| `Helpers/Dialects/MariaDBAttribute.cs` | DataAttribute for MariaDB |
| `Helpers/Dialects/MicrosoftSqliteAttribute.cs` | DataAttribute for Microsoft.Data.Sqlite |
| `Helpers/Dialects/SystemSqliteAttribute.cs` | DataAttribute for System.Data.SQLite |
| `scripts/Reorganize-Tests.ps1` | PowerShell script for git mv + namespace updates |
| `data/mysql/create-stored-procedures.sql` | MySQL SP creation script |

### Moved Files (~75 files via PowerShell script)
| From | To |
|------|----|
| `Integration/Sqlite/Read/*.cs` (26 files) | `Integration/Read/` |
| `Integration/Sqlite/Write/*.cs` (15 files) | `Integration/Write/` |
| `Integration/Sqlite/Multiple/*.cs` (8 files) | `Integration/Multiple/` |
| `Integration/Sqlite/Streaming/*.cs` (6 files) | `Integration/Streaming/` |

### Modified Files (all moved files + these)
| File | Changes |
|------|---------|
| `Helpers/TestConfiguration.cs` | Add MariaDB connection string support |
| All moved test files | Theory + dialect attrs + DialectFixture |

### Deleted Files (after migration)
| File | Reason |
|------|--------|
| `Helpers/SkipIfNoSqlServerAttribute.cs` | Replaced by `SqlServerAttribute` |
| `Helpers/SkipIfNoPostgresAttribute.cs` | Replaced by `PostgresAttribute` |
| `Helpers/SkipIfNoMySqlAttribute.cs` | Replaced by `MariaDBAttribute` |
| `Helpers/SkipSQLiteAsyncAttribute.cs` | No longer needed |
| `Helpers/SQLiteTestBase.cs` | Replaced by `DialectFixture` |
| `Helpers/Database.cs` | Replaced by `DialectFixture` |
| Provider-specific duplicates in `Integration/SqlServer/`, `Integration/Postgres/` | Covered by dialect attrs |

---

## Verification

After each phase:
1. `dotnet build` — 0 errors
2. `dotnet test --filter "FullyQualifiedName~Integration.Read"` — verify moved tests pass
3. `dotnet test` — full suite, 0 new failures
4. Visual Studio Test Explorer: verify separate dialect entries per test method
5. `git diff data/sqlite/Northwind.db` — no changes (Northwind.db untouched)

---

## Execution Order

1. **Phase 1** — Create infrastructure files (DialectInfo, fixtures, attributes)
2. **Phase 1 verify** — `dotnet build` passes
3. **Phase 2** — Run PowerShell script to move files + update namespaces
4. **Phase 2 verify** — `dotnet build` passes (namespaces correct)
5. **Phase 3** — Rewrite test classes (batch: Read/ first, then Write/, Multiple/, Streaming/)
6. **Phase 3 verify** — `dotnet test` per category
7. **Phase 4** — Delete provider-specific duplicates
8. **Phase 5** — Cleanup helpers, update docs
9. **Final verify** — Full `dotnet test`, Test Explorer screenshots

