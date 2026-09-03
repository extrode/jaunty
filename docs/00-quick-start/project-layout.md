# Project Layout

Guide to the Jaunty repository structure and organization.

---

## Root Directory Structure

```
jaunty/
│
├── Configuration Files
│   ├── .editorconfig                    # Editor configuration (formatting, naming)
│   ├── .gitattributes                   # Git line endings, merge strategies
│   ├── .gitignore                       # Git ignore patterns
│   ├── Directory.Build.props            # Settings shared by every project
│   ├── coverage.runsettings             # Coverage collector settings, applied by scripts/coverage.ps1
│   ├── docker-compose.yml               # SQL Server, PostgreSQL, MySQL, MariaDB for the integration suites
│   ├── docker-compose.bulkinsert.yml    # The same engines for the bulk-insert benchmark
│   └── Jaunty.slnx                      # Solution file (opens all projects)
│
├── Documentation Files
│   ├── README.md                        # Project overview and quick start
│   ├── CHANGELOG.md                     # Release notes
│   ├── LICENSE.md                       # ISL-R 1.2
│   ├── LICENSE-DISTRIBUTION-EXCEPTION.md  # Permission to ship the packages inside your application
│   ├── CLA.md                           # Contributor license agreement
│   ├── CONTRIBUTING.md                  # How to contribute
│   ├── CONTRIBUTORS.md
│   └── SECURITY.md                      # How to report a vulnerability, and what is in scope
│
├── Folders
│   ├── .github/                         # Workflows: ci.yml, nightly.yml, release.yml
│   │
│   ├── src/                             # Source code packages
│   ├── tests/                           # Test projects
│   ├── docs/                            # Documentation (Markdown source)
│   │   └── specs/                       # Specifications, NNN-slug/NNN-spec.md
│   ├── work/                            # The work underway (tasklist, status, todo); untracked
│   ├── audit/                           # The audit record; untracked
│   │
│   ├── benchmarks/                      # Performance benchmarks
│   ├── samples/                         # Sample projects
│   ├── scripts/                         # Build and utility scripts
│   ├── data/                            # Test databases and seed scripts
│   └── dist/                            # Distribution artifacts (generated)
```

---

## Folder Details

### Core Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `src/` | Source code for the 9 packages plus the source generator that ships inside the core package | `Jaunty/`, `Jaunty.Fluent/`, `Jaunty.FlatFiles/`, `Jaunty.SourceGenerator/` |
| `tests/` | The 10 test projects, shared helpers, and the server seed scripts | `Jaunty.Tests/`, `Jaunty.UnitTests/`, `Helpers/`, `*-setup.sql` |
| `docs/` | Documentation source | Organized by topic (00-quick-start through 08-learn), plus `decisions/`, `plans/`, `specs/`, `lessons/`, `architecture/` |
| `docs/specs/` | Specifications | One `NNN-slug/` per feature, files `NNN-spec.md` etc. |
| `work/` | The work underway. **Untracked** — held in a private repository, so a clone will not contain it | `tasklist.md`, `status/`, `todo.md`, `milestones.md` |
| `audit/` | The 36-round audit record, and the summary of it. **Untracked**, same private repository | `roundNN/`, `findings-registry.md`, `coverage-ledger.md` |

### Development Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `benchmarks/` | BenchmarkDotNet projects and their reports | `Jaunty.Benchmarks/`, `Jaunty.FlatFiles.Benchmarks/`, `BENCHMARK-RESULTS.md` |
| `samples/` | Sample projects | Four `NativeAOT-*` samples; four `torture-test-*` ports (Conduit, eShopOnWeb, Sakila) |
| `scripts/` | Build and utility scripts | `build.ps1`, `build-aot.ps1`, `Verify-NativeAOT.ps1`, `coverage.ps1`, `reset-test-databases.ps1` |
| `data/` | Test databases and seed scripts | `sqlite/Northwind.db` (tracked; the source of the others), `postgres/`, `mysql/`, `sqlserver/` create scripts, `basic.csv` |

### Distribution Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `dist/` | Distribution artifacts | `docs-site/`, the generated HTML documentation, which is committed |

---

## Source Code Structure (src/)

```
src/
├── Jaunty/                        # Core micro-ORM (package Extrode.Jaunty)
│   ├── Attributes/                # [Table], [Column], [Key], [Ignore], [DatabaseGenerated], [EnumStorage]
│   ├── Configuration/             # JauntyConfig, BulkCopyConfiguration
│   ├── Core/                      # CommandOptions<T>, GridReader
│   ├── Diagnostics/               # AuditInterceptor, DiagnosticSource listener
│   ├── Dialects/                  # SQL Server, PostgreSQL, MySQL, SQLite; SqlIdentifierValidator
│   ├── Execute/                   # Execute (non-query) methods
│   ├── Import/                    # CSV import
│   ├── Interceptors/              # ICommandInterceptor, CommandContext, InterceptorPipeline
│   ├── Interfaces/                # IMapped<T>, IEntity<T>, generated-accessor contracts
│   ├── Multiple/                  # QueryMultiple
│   ├── Read/                      # Query* methods
│   ├── StoredProcedure/           # ExecuteStoredProcedure*, SpParameters
│   ├── Streaming/                 # QueryStream*, QueryPartialStream*
│   ├── TypeHandlers/              # Custom type handlers
│   ├── Write/                     # Insert, Update, Delete, Upsert, Bulk*
│   └── Internals/                 # Entity metadata, parameter parsing and binding, read and write cores
│
├── Jaunty.SourceGenerator/        # Roslyn generator, packed inside Extrode.Jaunty (not a package of its own)
├── Jaunty.Fluent/                 # Fluent query API
├── Jaunty.FlatFiles/              # Flat file support (interfaces)
├── Jaunty.FlatFiles.DuckDB/       # DuckDB implementation
├── Jaunty.Extensions.Reflection/  # Reflection-based mapping and native bulk copy providers
├── Jaunty.Extensions.Logging/     # ILogger + DI integration (keeps core dependency-free)
├── Jaunty.Extensions.Npgsql/      # PostgreSQL-specific parameter binding without boxing
├── Jaunty.Scaffolding/            # Database scaffolding
└── Jaunty.Scaffolding.Cli/        # dotnet tool; the NativeAOT publish target
```

`Jaunty.Extensions.Logging` exists so that `src/Jaunty` can declare **no package dependencies** on
`net8.0` and `net10.0`; it carries the `Microsoft.Extensions.*` references on core's behalf. See
[`../02-architecture/dependencies.md`](../02-architecture/dependencies.md).

---

## Test Structure (tests/)

```
tests/
├── Helpers/                       # Shared across projects
├── database-setup.sql             # SQL Server seed
├── postgres-setup.sql
├── mysql-setup.sql
├── mariadb-setup.sql
│
├── Jaunty.Tests/                  # Core integration suite, per engine
│   ├── Integration/               # Read/, Write/, Multiple/, Streaming/, StoredProcedure/, Dialects/ ...
│   ├── Unit/                      # Unit tests that need the core's internals
│   ├── Performance/
│   ├── Entities/                  # Test models
│   └── Helpers/                   # NorthwindDatabase, recording and throwing connections
│
├── Jaunty.UnitTests/              # Core unit tests, no database
├── Jaunty.SourceGenerator.Tests/  # Generator output and generated-mapper behaviour
├── Jaunty.Fluent.Tests/           # Fluent API tests
├── Jaunty.Fluent.ConfigTests/
├── Jaunty.Fluent.SourceGen.Tests/
├── Jaunty.FlatFiles.Tests/        # FlatFiles interface tests
├── Jaunty.FlatFiles.DuckDB.Tests/ # DuckDB implementation tests
├── Jaunty.Scaffolding.Tests/      # Scaffolding tests
└── Jaunty.Scaffolding.Cli.Tests/
```

**Test Organization Convention**: Test folders mirror `src/` structure

---

## Documentation Structure (docs/)

```
docs/
├── 00-quick-start/                # Getting started
├── 01-api-reference/              # API documentation
├── 02-architecture/               # Architecture docs
├── 03-development/                # Development guides
├── 04-extensions/                 # Extension docs
├── 05-quality/                    # Quality, testing, audit record, benchmark reports
├── 06-releases/                   # Release runbook, order templates
├── 07-design/                     # Docs-site design source
├── 08-learn/                      # Tutorials, exercises, error messages, migration guides
├── 99-archive/                    # Historical documents
├── _assets/                       # Media assets (logos, charts, diagrams)
├── decisions/                     # Dated decision records
├── plans/                         # Dated implementation plans
├── specs/                         # Numbered feature specifications
├── lessons/                       # Lessons for AI assistants and for humans
├── constitution.md                # The binding rules for the codebase
└── conventions.md                 # Naming, git and cleanup conventions
```

**Documentation Convention**: Numbered prefixes establish reading order

---

## File Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Documentation | `lowercase-with-hyphens.md` | `api-design-guidelines.md` |
| Folders | `lowercase-with-hyphens/` | `00-quick-start/` |
| Special | `_prefix/` sorts first | `_assets/` |
| Special | `99-prefix/` sorts last | `99-archive/` |

See [`03-development/file-naming-convention.md`](../03-development/file-naming-convention.md) for the complete standard.

---

## Navigation Tips

### Finding Source Code

| You Want | Go To |
|----------|-------|
| Query methods | `src/Jaunty/Read/Query.cs` |
| CRUD operations | `src/Jaunty/Write/` |
| Parameter parsing and binding | `src/Jaunty/Internals/Parameters/` |
| Dialects | `src/Jaunty/Dialects/` |
| Flat file support | `src/Jaunty.FlatFiles.DuckDB/` |
| Fluent API | `src/Jaunty.Fluent/` |
| `ILogger` / DI integration | `src/Jaunty.Extensions.Logging/` |
| The source generator | `src/Jaunty.SourceGenerator/` |

### Finding Tests

| You Want | Go To |
|----------|-------|
| Integration tests | `tests/Jaunty.Tests/Integration/` |
| Unit tests without a database | `tests/Jaunty.UnitTests/` |
| Generator tests | `tests/Jaunty.SourceGenerator.Tests/` |
| FlatFiles tests | `tests/Jaunty.FlatFiles.DuckDB.Tests/` |
| Performance tests | `tests/Jaunty.Tests/Performance/`, `tests/Jaunty.FlatFiles.DuckDB.Tests/Performance/` |

### Finding Documentation

| You Want | Go To |
|----------|-------|
| API reference | [`docs/01-api-reference/`](../01-api-reference/README.md) |
| Architecture | [`docs/02-architecture/`](../02-architecture/README.md) |
| Development guides | [`docs/03-development/`](../03-development/README.md) |
| Historical docs | [`docs/99-archive/`](../99-archive/) |

---

## Build and Scripts

### Build Scripts

| Script | Purpose |
|--------|---------|
| `scripts/build.ps1` | Standard build |
| `scripts/build-aot.ps1` | NativeAOT build |
| `scripts/Verify-NativeAOT.ps1` | NativeAOT verification: every reflection site carries a reviewed justification |
| `scripts/coverage.ps1` | Coverage run with `coverage.runsettings` |
| `scripts/build-docs.sh` | Generates `dist/docs-site/` from `docs/` |
| `scripts/reset-test-databases.ps1` / `.sh` | Drops and recreates the server test databases |
| `scripts/generate-northwind.py` | Renders the PostgreSQL and MySQL create scripts from `data/sqlite/Northwind.db` |

### Running Tests

```bash
# All tests
dotnet test Jaunty.slnx

# Specific test project
dotnet test tests/Jaunty.Tests

# With coverage
pwsh -NoProfile -File scripts/coverage.ps1
```

---

## Related Documents

- [`README.md`](../README.md) - Project overview
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) - Contribution guide
- [`00-quick-start/build-and-test.md`](build-and-test.md) - Build instructions

---

**Last Updated**: 2026-09-02
