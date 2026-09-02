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

The solution targets `net10.0`, so the .NET 10 SDK is required; CI installs 8.0, 9.0 and 10.0.

## Test Commands

### Run All Tests

```bash
# Every project, every framework
dotnet test Jaunty.slnx

# With output
dotnet test Jaunty.slnx -v n

# No build (use existing binaries)
dotnet test Jaunty.slnx --no-build
```

Run the framework legs of `Jaunty.Tests` separately rather than in one `dotnet test` of the
solution when SQL Server is reachable: the scaffolding reader tests drop and recreate fixed-name
tables in the one shared database, and two legs running at once fail one or two of them at random.

### Filter Tests

The suites carry one xUnit trait, `Category=AllocationBudget`; everything else is selected by name
or by project.

```bash
# By test class name
dotnet test tests/Jaunty.Tests -f net10.0 --filter "FullyQualifiedName~QueryTests"

# By project
dotnet test tests/Jaunty.Fluent.Tests

# The allocation budgets alone (they live in Jaunty.UnitTests)
dotnet test tests/Jaunty.UnitTests -f net10.0 --filter "Category=AllocationBudget"
```

### Test Projects

| Project | Purpose |
|---------|---------|
| `Jaunty.Tests` | Core integration suite against SQLite, SQL Server, PostgreSQL, MySQL and MariaDB; the server engines skip when unreachable |
| `Jaunty.UnitTests` | Core unit tests, no database |
| `Jaunty.SourceGenerator.Tests` | The bundled source generator: emitted source and generated-mapper behaviour |
| `Jaunty.Fluent.Tests` | Fluent query builder |
| `Jaunty.Fluent.ConfigTests` | Fluent builder configuration |
| `Jaunty.Fluent.SourceGen.Tests` | Fluent builder's generator |
| `Jaunty.FlatFiles.Tests` | Flat-file abstractions |
| `Jaunty.FlatFiles.DuckDB.Tests` | DuckDB-backed flat-file querying |
| `Jaunty.Scaffolding.Tests` | Schema readers and code generation |
| `Jaunty.Scaffolding.Cli.Tests` | The scaffolding command-line tool |

The whole solution on 2026-09-02, all frameworks: 10,429 passed, 37 skipped.

```bash
# Specific test project
dotnet test tests/Jaunty.Tests
dotnet test tests/Jaunty.Fluent.Tests
```

## Code Coverage

The repository has a configured coverage run that applies `coverage.runsettings` and lands
cobertura reports in `tmp/coverage/`:

```powershell
pwsh -NoProfile -File scripts/coverage.ps1
```

The ad-hoc equivalent, without the settings file, is `dotnet test --collect:"XPlat Code Coverage"`,
which writes `TestResults/*/coverage.cobertura.xml`. Prefer the script; the settings file exists
for a reason. See [`../05-quality/code-coverage.md`](../05-quality/code-coverage.md) for that
reason and for what the current baseline measures.

## Test Infrastructure

### Database Setup

The SQLite suites use the Northwind sample database at `data/sqlite/Northwind.db`. The file is
tracked in git; `tests/Jaunty.Tests/Helpers/NorthwindDatabase.cs` locates it by walking up from the
test output directory and fails the run with "Could not locate data/sqlite/Northwind.db" rather than
creating one. It is also the source that `scripts/generate-northwind.py` renders into the
PostgreSQL and MySQL `create-northwind.sql` scripts under `data/`.

The server engines are seeded from `tests/*-setup.sql` and `data/<engine>/`; `docker-compose.yml`
starts SQL Server, PostgreSQL, MySQL and MariaDB. Connection strings come from
`tests/Jaunty.Tests/appsettings.json`, which is gitignored; copy `appsettings.example.json` to start.
After a run against the servers, `scripts/reset-test-databases.ps1 -e` (or `.sh`) drops and
recreates them so the next run starts from the seeded baseline.

## CI/CD

### GitHub Actions

- `ci.yml` runs on every push and pull request to `dev` and `main`.
- `nightly.yml` runs on a schedule, 05:00 UTC on weekdays and Saturday, with the mutation tier on
  the Sunday run only.
- `release.yml` runs on a version tag.

### Local Pre-commit

```bash
# Build and test before commit
dotnet build && dotnet test Jaunty.slnx
```

## Troubleshooting

### Tests fail with "Could not locate data/sqlite/Northwind.db"

The file is tracked; restore it:
```bash
git checkout -- data/sqlite/Northwind.db
```

### Server suites all skip

No server was reachable at the connection string in `tests/Jaunty.Tests/appsettings.json`. Start
the containers with `docker compose up -d` and check the file exists; the skip reason names the
engine.

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

The published reports are under [`../05-quality/reports/`](../05-quality/README.md).

## See Also

- [`README.md`](README.md) - Quick start guide
- [`../03-development/api-design-guidelines.md`](../03-development/api-design-guidelines.md) - Coding and API standards
- [`../03-development/xml-documentation-style.md`](../03-development/xml-documentation-style.md) - Doc comment house style
- [`../05-quality/code-coverage.md`](../05-quality/code-coverage.md) - Coverage documentation
