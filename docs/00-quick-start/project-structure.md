# Project Structure

This document explains how the core library and its test suite are organized. For the whole
repository tree see [`project-layout.md`](project-layout.md).

## Repository Layout

```
jaunty/
├── .editorconfig                  # Code style rules
├── .gitignore                     # Git ignore patterns
├── Directory.Build.props          # Settings shared by every project
├── README.md                      # Project overview
├── Jaunty.slnx                    # Solution file
│
├── docs/                          # Documentation (00-quick-start through 08-learn, 99-archive)
├── src/                           # The 9 packages plus the source generator
│   ├── Jaunty/                    # Core micro-ORM (package Extrode.Jaunty)
│   ├── Jaunty.SourceGenerator/    # Roslyn generator, packed inside Extrode.Jaunty
│   ├── Jaunty.Fluent/             # Fluent query API
│   ├── Jaunty.FlatFiles/          # Flat-file abstractions
│   ├── Jaunty.FlatFiles.DuckDB/   # DuckDB implementation
│   ├── Jaunty.Extensions.Reflection/  # Reflection mapper and native bulk copy
│   ├── Jaunty.Extensions.Logging/     # ILogger and DI integration
│   ├── Jaunty.Extensions.Npgsql/      # PostgreSQL parameter binding without boxing
│   ├── Jaunty.Scaffolding/        # Database scaffolding
│   └── Jaunty.Scaffolding.Cli/    # Scaffolding dotnet tool
│
└── tests/                         # The 10 test projects, shared helpers, server seed scripts
```

## Main Library: src/Jaunty/

### Public API Directories

| Directory | Contents | Public |
|-----------|----------|--------|
| `Attributes/` | `[Table]`, `[Column]`, `[Key]`, `[Ignore]`, `[DatabaseGenerated]`, `[EnumStorage]` | yes |
| `Configuration/` | `JauntyConfig` (resolver delegates, type handlers, interceptors), `BulkCopyConfiguration`, `IBulkCopyProvider`, `MappingMode` | yes |
| `Core/` | `CommandOptions<T>`, `MultiEntityCommandOptions`, `GridReader` | yes |
| `Diagnostics/` | `AuditInterceptor`, the `DiagnosticSource` listener | yes |
| `Dialects/` | `ISqlDialect`, the SQLite, SQL Server, PostgreSQL and MySQL dialects, `SqlDialectFactory`, `SqlIdentifierValidator` | yes |
| `Execute/` | `Execute*` non-query methods | yes |
| `Import/` | CSV import | yes |
| `Interceptors/` | `ICommandInterceptor`, `CommandContext`, the interceptor pipeline | yes |
| `Interfaces/` | `IMapped<T>`, `IEntity<T>`, `IGeneratedAccessors`, `IEntityMetadataSource` | yes |
| `Multiple/` | `QueryMultiple*` | yes |
| `Read/` | `Query*`, `Get*`, `QueryScalar*` | yes |
| `StoredProcedure/` | `ExecuteStoredProcedure*`, `SpParameters` | yes |
| `Streaming/` | `QueryStream*`, `QueryPartialStream*` | yes |
| `TypeHandlers/` | Custom type handlers | yes |
| `Write/` | `Insert`, `Update`, `Delete`, `Upsert`, `Bulk*` | yes |

### Internal Implementation: Internals/

| Directory | Purpose |
|-----------|---------|
| `BulkCopy/` | `BulkCopyOptions`, `EntityDataReader` |
| `Entity/` | `EntityMetadata`, `ColumnMetadata`, the source-generated metadata resolver |
| `Parameters/` | `SqlParameterParser` and its cache, `ParameterBinder`, `ParameterCache`, `ParameterCeiling` |
| `Read/` | `QueryCore`, `ExecuteReader`, `ExecuteQueryMultiple` (sync and async), `DrDispatcher`, `MappedCache`, `MultiEntityMapper`, scalar conversion |
| `Write/` | `InsertCore`, `UpdateCore`, `DeleteCore`, `CrudSqlCache` / `CachedCrudSql`, `MultiRowInsertCache`, bulk validators |

Loose files at the `Internals/` root cover the pieces shared by both paths: `BoundedCache`,
`CapacityHint`, `CommandTimeoutHint`, `CommandObservation`, `KeyGuard`,
`AsyncTransactionValidator`.

## Test Projects

### Jaunty.Tests

```
tests/Jaunty.Tests/
├── Entities/                      # Test models
├── Helpers/                       # NorthwindDatabase, TestConfiguration, recording and
│   │                              #   throwing connections, MockDbCommand
│   └── Dialects/                  # Per-engine data attributes; skip when the engine is unreachable
├── Integration/                   # Against a real database, SQLite by default
│   ├── Core/  Dialects/  Execute/  Get/  Import/  Infrastructure/  Multiple/  Read/
│   ├── StoredProcedure/  Streaming/  TransactionSafety/  TypeHandlers/  Write/
│   └── IDbConnectionFallback/
├── Performance/
└── Unit/                          # Needs the core's internals but no database
    ├── BulkCopy/  Configuration/  Core/  Dialects/  Extensions/  Import/  Interceptors/
    └── Internals/  Read/  StoredProcedures/  TypeHandlers/  Write/
```

`Jaunty.UnitTests/Unit/` holds the database-free suite, including the repository guard tests
(`DocumentedApiTests`, `LicenseFileTests`, `PackageIdentityTests`, `SolutionLayoutTests`,
`NightlyWorkflowCadenceTests`) and `AllocationBudgetTests`, the one suite with an xUnit trait.

### Test File Count

Files named `*Tests.cs`, counted 2026-09-02:

| Project | Files |
|---------|-------|
| `Jaunty.Tests` | 201 |
| `Jaunty.UnitTests` | 118 |
| `Jaunty.Fluent.Tests` | 108 |
| `Jaunty.FlatFiles.DuckDB.Tests` | 93 |
| `Jaunty.SourceGenerator.Tests` | 36 |
| `Jaunty.Scaffolding.Tests` | 32 |
| `Jaunty.FlatFiles.Tests` | 12 |
| `Jaunty.Fluent.SourceGen.Tests` | 5 |
| `Jaunty.Fluent.ConfigTests` | 3 |
| `Jaunty.Scaffolding.Cli.Tests` | 3 |

The whole solution, all frameworks, on the same day: 10,429 passed, 37 skipped.

## Target Frameworks

`src/Jaunty` targets:

| Framework | Purpose |
|-----------|---------|
| `netstandard2.0` | .NET Framework and anything else that cannot take `net8.0` |
| `net8.0` | Modern .NET: `FrozenDictionary`, span-based APIs |
| `net10.0` | Current .NET |

`Jaunty.Tests` runs on `net8.0`, `net10.0` and `net472`; the `net472` leg consumes the
`netstandard2.0` build.

Conditional compilation is almost entirely `NET8_0_OR_GREATER`:
```csharp
#if NET8_0_OR_GREATER
    // FrozenDictionary, modern APIs
#else
    // Dictionary fallback
#endif
```

## Key Dependencies

### Runtime (`src/Jaunty`)

| Package | Framework | Purpose |
|---------|-----------|---------|
| `Microsoft.Bcl.AsyncInterfaces` | `netstandard2.0` only | `IAsyncEnumerable<T>`, `IAsyncDisposable` |
| `System.Diagnostics.DiagnosticSource` | `netstandard2.0` only | `DiagnosticListener` for the diagnostics events |

On `net8.0` and `net10.0` the core has no package dependencies.

### Test (`tests/Jaunty.Tests`)

| Package | Purpose |
|---------|---------|
| `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Test framework and runner |
| `coverlet.collector` | Coverage collector |
| `CsCheck` | Property-based tests |
| `System.Data.SQLite.Core`, `Microsoft.Data.Sqlite` | The two SQLite providers |
| `Microsoft.Data.SqlClient`, `Npgsql`, `MySql.Data` | Server providers; version-pinned per framework |
| `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` | Logging extension tests |

## File Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Sync methods | `MethodName.cs` | `Query.cs`, `QueryFirst.cs` |
| Async methods | `MethodNameAsync.cs` | `QueryAsync.cs` |
| Tests | `ClassNameTests.cs` | `QueryTests.cs`, `ParameterBinderTests.cs` |
| Internals | `ComponentName.cs` | `SqlParameterParser.cs`, `CrudSqlCache.cs` |

## See Also

- [`README.md`](README.md) - Quick start
- [`project-layout.md`](project-layout.md) - The whole repository
- [`../01-api-reference/README.md`](../01-api-reference/README.md) - API reference
- [`../02-architecture/README.md`](../02-architecture/README.md) - Architecture
