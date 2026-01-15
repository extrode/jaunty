# Database-to-Entity Code Generation: Complete Analysis

## What This Is

A CLI/tool that:
1. Connects to an existing database
2. Reads schema metadata (tables, views, columns, keys, constraints)
3. Generates C# entity classes with proper attributes
4. Handles naming conventions (snake_case → PascalCase)
5. Generates `IMapped<T>` implementations with optimized `ReadEntity()` methods

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Jaunty.Scaffold CLI                          │
├─────────────────────────────────────────────────────────────────┤
│  jaunty scaffold --connection "..." --output ./Entities          │
│                  --namespace MyApp.Entities                      │
│                  --dialect sqlserver|postgres|sqlite|mysql       │
└───────────────┬─────────────────────────────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────────┐
│      Schema Reader (per dialect)                               │
│  ├─ SqlServerSchemaReader                                      │
│  ├─ PostgresSchemaReader                                       │
│  ├─ SqliteSchemaReader                                         │
│  └─ MySqlSchemaReader                                          │
└───────────────┬───────────────────────────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────────┐
│         Schema Model                                           │
│  ├─ DatabaseSchema                                             │
│  │   ├─ Tables[]                                               │
│  │   └─ Views[]                                                │
│  ├─ TableSchema                                                │
│  │   ├─ Name, Schema                                           │
│  │   ├─ Columns[]                                              │
│  │   └─ PrimaryKeys[]                                          │
│  └─ ColumnSchema                                               │
│      ├─ Name, DataType, IsNullable                             │
│      ├─ IsIdentity, IsComputed                                 │
│      └─ MaxLength, Precision, Scale                            │
└───────────────┬───────────────────────────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────────┐
│      Type Mapper (per dialect)                                 │
│  SQL Type → C# Type                                            │
│  ├─ int/integer → int                                          │
│  ├─ varchar/text → string                                      │
│  ├─ decimal/numeric → decimal                                  │
│  ├─ timestamp → DateTime                                       │
│  └─ ... dialect-specific mappings                              │
└───────────────┬───────────────────────────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────────┐
│      Naming Convention Handler                                 │
│  ├─ snake_case → PascalCase                                    │
│  ├─ SCREAMING_CASE → PascalCase                                │
│  ├─ camelCase → PascalCase                                     │
│  └─ Pluralization handling                                     │
└───────────────┬───────────────────────────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────────┐
│        Code Generator                                          │
│  ├─ Entity class with properties                               │
│  ├─ [Table], [Column], [Key] attrs                             │
│  ├─ IMapped<T>.ReadEntity() method                             │
│  ├─ IEntity<TKey> implementation                               │
│  └─ OrdinalCache helper struct                                 │
└───────────────┬───────────────────────────────────────────────┘
                │
                ▼
        Generated .cs files
```

---

## Component Details

### 1. Schema Readers (Per Dialect)

Each database has different system catalogs:

**SQL Server:**
```sql
SELECT
    c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE,
    c.IS_NULLABLE, c.CHARACTER_MAXIMUM_LENGTH,
    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
-- Plus sys.indexes, sys.key_constraints for PKs
```

**PostgreSQL:**
```sql
SELECT
    table_schema, table_name, column_name, udt_name,
    is_nullable, character_maximum_length,
    is_identity, identity_generation
FROM information_schema.columns
-- Plus pg_constraint for PKs
```

**SQLite:**
```sql
PRAGMA table_info('table_name');
-- Returns: cid, name, type, notnull, dflt_value, pk
```

**MySQL:**
```sql
SELECT
    TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, EXTRA
FROM INFORMATION_SCHEMA.COLUMNS
WHERE EXTRA LIKE '%auto_increment%' -- for identity
```

---

### 2. Type Mapping

SQL types → C# types with nullability:

| SQL Server | PostgreSQL | SQLite | MySQL | C# Type |
|------------|------------|--------|-------|---------|
| int | integer | INTEGER | INT | int |
| bigint | bigint | INTEGER | BIGINT | long |
| smallint | smallint | INTEGER | SMALLINT | short |
| tinyint | - | INTEGER | TINYINT | byte |
| bit | boolean | INTEGER | TINYINT(1) | bool |
| decimal(p,s) | numeric(p,s) | REAL | DECIMAL(p,s) | decimal |
| float | double precision | REAL | DOUBLE | double |
| real | real | REAL | FLOAT | float |
| varchar(n) | varchar(n) | TEXT | VARCHAR(n) | string |
| nvarchar(n) | text | TEXT | TEXT | string |
| char(n) | char(n) | TEXT | CHAR(n) | string |
| datetime | timestamp | TEXT | DATETIME | DateTime |
| datetime2 | timestamptz | TEXT | TIMESTAMP | DateTime |
| date | date | TEXT | DATE | DateOnly (.NET 6+) |
| time | time | TEXT | TIME | TimeSpan |
| uniqueidentifier | uuid | TEXT | CHAR(36) | Guid |
| varbinary | bytea | BLOB | BLOB | byte[] |
| money | money | REAL | DECIMAL | decimal |

---

### 3. Naming Convention Handler

Detecting and converting naming patterns:

```csharp
public static class NamingConventionDetector
{
    public static NamingStyle Detect(IEnumerable<string> columnNames)
    {
        // Analyze patterns:
        // - Contains underscores? → snake_case or SCREAMING_SNAKE
        // - All uppercase? → SCREAMING
        // - Starts lowercase? → camelCase
        // - Starts uppercase no underscores? → PascalCase
    }
}

public static class NameConverter
{
    public static string ToPascalCase(string name, NamingStyle sourceStyle)
    {
        return sourceStyle switch
        {
            NamingStyle.SnakeCase => string.Concat(
                name.Split('_')
                    .Select(s => char.ToUpper(s[0]) + s[1..].ToLower())),
            NamingStyle.ScreamingSnake => string.Concat(
                name.Split('_')
                    .Select(s => char.ToUpper(s[0]) + s[1..].ToLower())),
            // ...
        };
    }
}
```

**Edge cases**:
- `product_id` → `ProductId` (easy)
- `productID` → `ProductId` or `ProductID`? (ambiguous)
- `XMLParser` → preserve acronyms?
- `__double_underscore__` → handle gracefully

---

### 4. Generated Output Example

For a `products` table with snake_case columns:

```csharp
using System.Data;
using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace MyApp.Entities;

[Table("products")]
public class Product : IEntity<int>, IMapped<Product>
{
    [Ignore]
    public int Id
    {
        get => ProductId;
        set => ProductId = value;
    }

    [Key]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    [Column("supplier_id")]
    public int? SupplierId { get; set; }

    [Column("category_id")]
    public short? CategoryId { get; set; }

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }

    public static Product ReadEntity(IDataReader reader)
    {
        var ordinal = new OrdinalCache(reader);
        return new()
        {
            ProductId = reader.GetInt32(ordinal["product_id"]),
            ProductName = reader.GetString(ordinal["product_name"]),
            SupplierId = reader.IsDBNull(ordinal["supplier_id"])
                ? null
                : reader.GetInt32(ordinal["supplier_id"]),
            CategoryId = reader.IsDBNull(ordinal["category_id"])
                ? null
                : reader.GetInt16(ordinal["category_id"]),
            UnitPrice = reader.IsDBNull(ordinal["unit_price"])
                ? null
                : reader.GetDecimal(ordinal["unit_price"]),
        };
    }

    private readonly struct OrdinalCache(IDataReader reader)
    {
        private readonly Dictionary<string, int> _cache = [];
        public int this[string columnName] =>
            _cache.TryGetValue(columnName, out var ordinal)
                ? ordinal
                : _cache[columnName] = reader.GetOrdinal(columnName);
    }
}
```

---

## CLI Interface Design

```bash
# Basic usage
jaunty scaffold \
  --connection "Server=localhost;Database=Northwind;..." \
  --output ./Entities \
  --namespace MyApp.Entities

# With options
jaunty scaffold \
  --connection "Host=localhost;Database=northwind;..." \
  --dialect postgres \
  --output ./Entities \
  --namespace MyApp.Entities \
  --tables products,categories,orders \    # Specific tables only
  --exclude-views \                         # Skip views
  --naming pascal \                         # Force PascalCase conversion
  --nullable enable \                       # Use nullable reference types
  --generate-mapped \                       # Include IMapped<T> implementation
  --generate-entity \                       # Include IEntity<T> implementation

# From config file
jaunty scaffold --config ./jaunty.scaffold.json
```

**Config file (`jaunty.scaffold.json`):**
```json
{
  "connection": "Server=localhost;Database=Northwind;Trusted_Connection=true",
  "dialect": "sqlserver",
  "output": "./Entities",
  "namespace": "MyApp.Entities",
  "options": {
    "generateMapped": true,
    "generateEntity": true,
    "nullableReferenceTypes": true,
    "namingConvention": "auto"
  },
  "tables": {
    "include": ["*"],
    "exclude": ["__EFMigrationsHistory", "sysdiagrams"]
  },
  "typeOverrides": {
    "products.unit_price": "decimal",
    "*.created_at": "DateTimeOffset"
  }
}
```

---

## Pros

### 1. Zero-Friction Onboarding
- Database exists → Run one command → Start coding
- No manual POCO creation for 50+ table databases
- Matches EF Core's `Scaffold-DbContext` experience developers expect

### 2. Correct by Construction
- Types match database exactly (no guessing)
- Nullability reflects column constraints
- Primary keys automatically detected
- Foreign keys could inform navigation properties (future)

### 3. Optimized ReadEntity() Included
- Users get `IMapped<T>` performance without writing boilerplate
- OrdinalCache pattern built-in
- Correct `GetInt32`/`GetString`/etc. methods per type

### 4. Dialect-Aware
- Respects each database's type system
- Generates appropriate attributes for column name mapping
- Handles dialect-specific types gracefully

### 5. Regeneration-Friendly
- Schema changes → Re-run scaffold → Updated entities
- Could support partial classes for custom logic preservation
- Timestamp/hash to detect manual modifications

### 6. Competitive Feature
- Dapper has no official scaffolder
- EF Core has `Scaffold-DbContext`
- This fills a gap for micro-ORM users

---

## Cons

### 1. Significant Development Effort

| Component | Estimated Effort |
|-----------|------------------|
| Schema readers (4 dialects) | High |
| Type mapping (all edge cases) | Medium |
| Naming convention detection | Medium |
| Code generator | Medium |
| CLI infrastructure | Medium |
| Testing (many DB versions) | High |
| Documentation | Medium |

### 2. Maintenance Burden

- Database versions change (PostgreSQL 15 vs 16, etc.)
- New C# features (required members, primary constructors)
- Type mapping edge cases reported by users
- Each dialect is essentially its own product

### 3. Partial Regeneration Problem

User scenario:
1. Generate entities
2. Add custom methods/logic to `Product.cs`
3. Database schema changes
4. Regenerate... **custom code lost**

**Solutions** (all have tradeoffs):
- Partial classes (`.g.cs` + user `.cs`)
- Merge detection (complex)
- Manual conflict resolution
- "Don't regenerate, just diff" mode

### 4. Connection String in CLI

Security concern: connection strings in command history, scripts, CI logs.

**Mitigations:**
- Environment variable support: `--connection $DB_CONNECTION`
- Config file (gitignored)
- Interactive prompt for password

### 5. Opinionated Decisions

Scaffolding forces choices:
- `int` vs `Int32`?
- `string?` or `string = null!`?
- Separate file per entity or single file?
- `record` vs `class`?
- Include XML docs from column descriptions?

### 6. Database Permissions

Reading schema requires:
- SQL Server: `VIEW DEFINITION` permission
- PostgreSQL: Access to `information_schema`
- Some production DBs restrict this

### 7. Complex Schemas

Edge cases:
- Composite primary keys → `IEntity<(int, int)>`?
- Views without clear PK → How to handle?
- Computed columns → ReadOnly properties?
- Temporal tables → Include history columns?
- JSON columns → `string` or `JsonDocument`?
- Geography/Geometry types → What type?

---

## Complexity Assessment

| Area | Complexity | Notes |
|------|------------|-------|
| **Core scaffolding** | Medium | Straightforward once schema is read |
| **SQL Server schema reader** | Medium | Well-documented, INFORMATION_SCHEMA |
| **PostgreSQL schema reader** | Medium | Some quirks with array types, enums |
| **SQLite schema reader** | Low-Medium | Simple but limited type info |
| **MySQL schema reader** | Medium | Version differences (5.7 vs 8.0) |
| **Type mapping** | Medium-High | Many edge cases across dialects |
| **Naming detection** | Medium | Heuristics, user overrides |
| **Code generation** | Medium | String building, formatting |
| **CLI infrastructure** | Low | Use existing CLI framework |
| **Testing** | High | Need real databases, many versions |
| **Partial class support** | Medium-High | Merge/detection logic |

**Overall: Medium-High complexity project**

---

## Implementation Phases

### Phase 1: MVP
- SQLite schema reader only (simplest)
- Basic type mapping
- Simple code generation (no IMapped<T>)
- CLI with minimal options

### Phase 2: Full Dialect Support
- SQL Server, PostgreSQL, MySQL readers
- Complete type mapping tables
- Dialect-specific edge cases

### Phase 3: Advanced Features
- IMapped<T> generation with OrdinalCache
- IEntity<T> with Id property handling
- Naming convention auto-detection
- Partial class support

### Phase 4: Polish
- Config file support
- Table/view filtering
- Type overrides
- Documentation

---

## Recommendation

**This is worth building** - it's a differentiating feature that makes Jaunty immediately useful for database-first development. The `IMapped<T>` generation is particularly valuable as it gives users optimal performance without manual effort.

**Suggested approach:**
1. Start with SQLite (existing Northwind tests available)
2. Build the core infrastructure
3. Add SQL Server next (most common production DB)
4. PostgreSQL third (growing popularity)
5. MySQL last (if demand exists)

**Key decisions to make:**
1. Separate package (`Jaunty.Scaffold`) or built into main?
2. .NET tool (`dotnet tool install jaunty-scaffold`) or standalone exe?
3. Partial class strategy for regeneration?

---

## Related Documents
- [HighPerformanceMapperImplementation.md](./HighPerformanceMapperImplementation.md) - Roslyn source generator approach
- [SourceGenerationAnalysis.md](./SourceGenerationAnalysis.md) - Analysis of compile-time source generation
