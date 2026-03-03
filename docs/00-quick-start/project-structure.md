# Project Structure

This document explains the Jaunty codebase organization.

## Repository Layout

```
Jaunty/
├── .editorconfig                  # Code style rules
├── .gitignore                     # Git ignore patterns
├── README.md                      # Project overview
├── Jaunty.slnx                    # Visual Studio solution
│
├── docs/                          # Documentation
│   ├── README.md                  # Documentation index
│   ├── 00-quick-start/            # Getting started
│   ├── 01-api-reference/          # API docs
│   ├── 02-architecture/           # Architecture
│   ├── 03-development/            # Development guides
│   ├── code-coverage/             # Test coverage
│   └── archive/                   # Historical docs
│
├── src/                           # Source code
│   ├── Jaunty/                    # Main library
│   │   ├── Attributes/            # Mapping attributes
│   │   ├── Configuration/         # JauntyConfig
│   │   ├── Core/                  # Core types
│   │   ├── Interfaces/            # IMapped<T>, IEntity<T>
│   │   ├── Read/                  # Query methods
│   │   ├── Streaming/             # Streaming methods
│   │   ├── Write/                 # CRUD operations
│   │   ├── Multiple/              # Multiple result sets
│   │   ├── StoredProcedure/       # Stored procedures
│   │   └── Internals/             # Implementation
│   │
│   ├── Jaunty.Fluent/             # Fluent API (optional)
│   ├── Jaunty.Scaffolding/        # Code scaffolding
│   └── Jaunty.Scaffolding.Cli/    # Scaffolding CLI
│
└── tests/                         # Test projects
    ├── Jaunty.Tests/              # Main test suite
    ├── Jaunty.Fluent.Tests/      # Fluent API tests
    └── Jaunty.Scaffolding.Tests/  # Scaffolding tests
```

## Main Library: src/Jaunty/

### Public API Directories

|-----------|---------|----------|
| `Attributes/` | `[Table]`, `[Column]`, `[Ignore]`, `[Key]`, `[DatabaseGenerated]` | yes |
| `Configuration/` | `JauntyConfig`, `NamingConvention` helpers | yes |
| `Core/` | `CommandOptions<T>`, `GridReader` | yes |
| `Interfaces/` | `IMapped<T>`, `IEntity<T>` | — |
| `Read/` | Query* methods | yes |
| `Streaming/` | QueryStream*, QueryPartialStream* | yes |
| `Write/` | Insert, Update, Delete, Bulk*, Upsert | yes |
| `Multiple/` | QueryMultiple, GridReader | yes |
| `StoredProcedure/` | ExecuteStoredProcedure* | — |

### Internal Implementation: Internals/

| Directory | Purpose |
|-----------|---------|
| `Dialects/` | ISqlDialect, SQLite/SqlServer/MySql/PostgreSql dialects |
| `Entity/` | EntityMetadata, MetadataCache<T>, MetadataBuilder |
| `Enums/` | MappingMode enum (Strict, Partial) |
| `Parameters/` | SqlParameterParser, ParameterBinder, ParameterCache |
| `Write/` | Internal write operation helpers |

| File | Purpose |
|------|---------|
| `QueryCore.cs` / `QueryCoreAsync.cs` | Core query execution |
| `ExecuteReader.cs` / `ExecuteReaderAsync.cs` | DataReader execution |
| `ExecuteQueryMultiple.cs` / `ExecuteQueryMultipleAsync.cs` | Multiple result sets |
| `DrDispatcher.cs` | Mapper resolution dispatcher |
| `MappedCache.cs` | Custom mapper cache |
| `CrudSqlCache.cs` / `CachedCrudSql.cs` | CRUD SQL generation cache |
| `MultiEntityMapper.cs` | Tuple mapping for Query<T1, T2> |

## Test Projects

### Jaunty.Tests

```
tests/Jaunty.Tests/
├── Entities/                      # Test models
│   ├── Product.cs
│   ├── Category.cs
│   ├── ProductSummary.cs
│   └── ...
│
├── Helpers/                       # Test infrastructure
│   ├── Database.cs                # SQLite connection helper
│   ├── SQLiteTestBase.cs          # Base class for integration tests
│   └── SkipSQLiteAsyncAttribute.cs
│
├── Integration/Sqlite/            # Database integration tests
│   ├── Configuration/             # JauntyConfig tests
│   ├── Infrastructure/            # Connection, dialect tests
│   ├── Multiple/                  # QueryMultiple, GridReader
│   ├── Read/                      # ~29 test files for Query*
│   ├── Streaming/                 # QueryStream* tests
│   └── Write/                     # Bulk*, Upsert tests
│
└── Unit/Read/                     # Unit tests
    ├── CommandOptionsTests.cs
    ├── SqlParameterParserTests.cs
    └── ParameterBinderTests.cs
```

### Test File Count

| Project | Files | Tests |
|---------|-------|-------|
| `Jaunty.Tests` | ~66 | ~600+ |
| `Jaunty.Fluent.Tests` | ~18 | ~100+ |
| `Jaunty.Scaffolding.Tests` | ~8 | ~50+ |

## Target Frameworks

| Framework | Purpose |
|-----------|---------|
| `netstandard2.0` | Broad compatibility (.NET Framework, .NET Core 2.0+) |
| `net8.0` | Modern .NET optimizations (FrozenDictionary, etc.) |

Conditional compilation:
```csharp
#if NET8_0_OR_GREATER
    // FrozenDictionary, modern APIs
#else
    // Dictionary fallback
#endif
```

## Key Dependencies

### Runtime

| Package | Framework | Purpose |
|---------|-----------|---------|
| `Microsoft.Bcl.AsyncInterfaces` | netstandard2.0 | Async interfaces |

### Test

| Package | Purpose |
|---------|---------|
| `xunit` | Test framework |
| `FluentAssertions` | Assertions |
| `System.Data.SQLite.Core` | SQLite provider |
| `Microsoft.NET.Test.Sdk` | Test platform |

## File Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Sync methods | `MethodName.cs` | `Query.cs`, `QueryFirst.cs` |
| Async methods | `MethodNameAsync.cs` | `QueryAsync.cs` |
| Tests | `ClassNameTests.cs` | `QueryTests.cs`, `ParameterBinderTests.cs` |
| Internals | `ComponentName.cs` | `MetadataCache.cs`, `SqlParameterParser.cs` |

## See Also

- [`README.md`](README.md) - Quick start
- [`../01-api-reference/README.md`](../01-api-reference/README.md) - API reference
- [`../02-architecture/README.md`](../02-architecture/README.md) - Architecture

