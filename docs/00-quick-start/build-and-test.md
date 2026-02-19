# Build and Test Guide

## Build Commands

```bash
# Debug build (all targets)
dotnet build

# Release build
dotnet build -c Release

# Specific framework
dotnet build -f net8.0
dotnet build -f netstandard2.0

# No restore (faster)
dotnet build --no-restore

# With warnings as errors
dotnet build -warnaserror
```

## Test Commands

### Run All Tests

```bash
# All tests
dotnet test

# With output
dotnet test -v n

# No build (use existing binaries)
dotnet test --no-build
```

### Filter Tests

```bash
# By category
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Unit"

# By test class name
dotnet test --filter "FullyQualifiedName~QueryTests"
dotnet test --filter "ClassName=ParameterBinderTests"

# By trait
dotnet test --filter "Priority=High"

# Combine filters
dotnet test --filter "Category=Integration&FullyQualifiedName~Read"
```

### Test Projects

| Project | Purpose |
|---------|---------|
| `Jaunty.Tests` | Main test suite (~600+ tests) |
| `Jaunty.Fluent.Tests` | Fluent API tests |
| `Jaunty.Scaffolding.Tests` | Scaffolding tests |

```bash
# Specific test project
dotnet test tests/Jaunty.Tests
dotnet test tests/Jaunty.Fluent.Tests
```

## Code Coverage

```bash
# Collect coverage
dotnet test --collect:"XPlat Code Coverage"

# Coverage with filters
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute="GeneratedCodeAttribute"
```

Coverage reports are generated in:
- `TestResults/*/coverage.cobertura.xml`


## Test Infrastructure

### Database Setup

Tests use SQLite with the Northwind sample database:
- Location: `data/sqlite/Northwind.db`
- Helper: `tests/Jaunty.Tests/Helpers/Database.cs`

### Test Helpers

```csharp
// Database connection helper
public class Database : IDisposable
{
    public IDbConnection Connection { get; }
}

// Base class for integration tests
public class SQLiteTestBase : IDisposable
{
    protected readonly Database _db;
}
```

## CI/CD

### GitHub Actions

Tests run automatically on:
- Pull requests
- Push to main branches
- Scheduled runs

### Local Pre-commit

```bash
# Build and test before commit
dotnet build && dotnet test
```

## Troubleshooting

### Tests Fail with "Database not found"

Ensure the SQLite database exists:
```bash
ls data/sqlite/Northwind.db
```

If missing, the test helper creates it from `data/sqlite/northwind.sql`.

### Tests Hang on SQLite Async

SQLite async has limitations. Some tests use `[SkipSQLiteAsync]` attribute.

### Build Fails with "Reference not found"

Restore packages:
```bash
dotnet restore
dotnet build --no-incremental
```

## Performance Testing

### BenchmarkDotNet

Benchmarks are in a separate project. Run with:

```bash
dotnet run -c Release --project benchmarks/Jaunty.Benchmarks
```

## See Also

- [`README.md`](README.md) - Quick start guide
- [`../03-development/coding-conventions.md`](../03-development/coding-conventions.md) - Coding standards
- [`../code-coverage/`](../code-coverage/) - Coverage documentation
