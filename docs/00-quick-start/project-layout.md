# Project Layout

Guide to the Jaunty repository structure and organization.

---

## Root Directory Structure

```
C:\src\jaunty\
│
├── Configuration Files
│   ├── .editorconfig              # Editor configuration (formatting, naming)
│   ├── .gitattributes             # Git line endings, merge strategies
│   ├── .gitignore                 # Git ignore patterns
│   └── Jaunty.slnx                # Solution file (opens all projects)
│
├── Documentation Files
│   ├── README.md                  # Project overview and quick start
│   ├── LICENSE.md                 # Project license
│   ├── CONTRIBUTING.md            # How to contribute
│
├── Folders
│   ├── .github/                   # GitHub configuration (workflows)
│   ├── .idea/                     # JetBrains IDE settings
│   ├── .vs/                       # Visual Studio settings
│   │
│   ├── src/                       # Source code packages
│   ├── tests/                     # Test projects
│   ├── docs/                      # Documentation (Markdown source)
│   │   └── specs/                 # Specifications, NNN-slug/NNN-spec.md
│   ├── work/                      # The work underway (tasklist, status, todo)
│   │
│   ├── benchmarks/                # Performance benchmarks
│   ├── samples/                   # Sample projects
│   ├── scripts/                   # Build and utility scripts
│   ├── data/                      # Test data files
│   └── dist/                      # Distribution artifacts (generated)
```

---

## Folder Details

### Core Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `src/` | Source code for all packages | `Jaunty/`, `Jaunty.FlatFiles/`, `Jaunty.Fluent/` |
| `tests/` | Test projects | `Jaunty.Tests/`, `Jaunty.FlatFiles.Tests/` |
| `docs/` | Documentation source | Organized by topic (00-quick-start through 06-releases) |
| `docs/specs/` | Specifications | One `NNN-slug/` per feature, files `NNN-spec.md` etc. |
| `work/` | The work underway | `tasklist.md`, `status/`, `todo.md`, `milestones.md` |

### Development Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `benchmarks/` | Performance benchmarks | `Jaunty.Benchmarks/`, `Jaunty.FlatFiles.Benchmarks/` |
| `samples/` | Sample projects | NativeAOT examples, basic usage samples |
| `scripts/` | Build and utility scripts | `build.ps1`, `build-aot.ps1`, `Verify-NativeAOT.ps1` |
| `data/` | Test data files | CSV, JSON, Parquet test files |

### Configuration Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `.github/` | GitHub configuration | Workflows |
| `.idea/` | JetBrains IDE settings | Rider, ReSharper settings |
| `.vs/` | Visual Studio settings | VS-specific configuration |

### Distribution Folders

| Folder | Purpose | Key Contents |
|--------|---------|--------------|
| `dist/` | Distribution artifacts | Generated HTML docs, publish outputs |

---

## Source Code Structure (src/)

```
src/
├── Jaunty/                        # Core micro-ORM
│   ├── Read/                      # Query operations
│   ├── Write/                     # CRUD operations
│   ├── Internals/                 # Internal helpers
│
├── Jaunty.Fluent/                 # Fluent query API
├── Jaunty.FlatFiles/              # Flat file support (interfaces)
├── Jaunty.FlatFiles.DuckDB/       # DuckDB implementation
├── Jaunty.Extensions.Reflection/  # Reflection-based mapping
└── Jaunty.Scaffolding/            # Database scaffolding
```

---

## Test Structure (tests/)

```
tests/
├── Jaunty.Tests/                  # Core tests
│   ├── Integration/               # Integration tests
│   ├── Unit/                      # Unit tests
│   └── Helpers/                   # Test helpers
│
├── Jaunty.Fluent.Tests/           # Fluent API tests
├── Jaunty.FlatFiles.Tests/        # FlatFiles interface tests
├── Jaunty.FlatFiles.DuckDB.Tests/ # DuckDB implementation tests
└── Jaunty.Scaffolding.Tests/      # Scaffolding tests
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
├── 05-quality/                    # Quality & testing
├── 06-releases/                   # Release docs
├── 99-archive/                    # Historical documents
└── _assets/                       # Media assets (screenshots, diagrams)
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

See [`03-development/file-naming-convention.md`](03-development/file-naming-convention.md) for the complete standard.

---

## Navigation Tips

### Finding Source Code

| You Want | Go To |
|----------|-------|
| Query methods | `src/Jaunty/Read/Query.cs` |
| CRUD operations | `src/Jaunty/Write/` |
| Flat file support | `src/Jaunty.FlatFiles.DuckDB/` |
| Fluent API | `src/Jaunty.Fluent/` |

### Finding Tests

| You Want | Go To |
|----------|-------|
| Integration tests | `tests/Jaunty.Tests/Integration/` |
| FlatFiles tests | `tests/Jaunty.FlatFiles.DuckDB.Tests/` |
| Performance tests | `tests/.../Performance/` folders |

### Finding Documentation

| You Want | Go To |
|----------|-------|
| API reference | [`docs/01-api-reference/`](01-api-reference/README.md) |
| Architecture | [`docs/02-architecture/`](02-architecture/README.md) |
| Development guides | [`docs/03-development/`](03-development/README.md) |
| Historical docs | [`docs/99-archive/`](99-archive/README.md) |

---

## Build and Scripts

### Build Scripts

| Script | Purpose |
|--------|---------|
| `scripts/build.ps1` | Standard build |
| `scripts/build-aot.ps1` | NativeAOT build |
| `scripts/Verify-NativeAOT.ps1` | NativeAOT verification |

### Running Tests

```bash
# All tests
dotnet test

# Specific test project
dotnet test tests/Jaunty.Tests

# With coverage
dotnet test /p:CollectCoverage=true
```

---

## Related Documents

- [`README.md`](../README.md) - Project overview
- [`CONTRIBUTING.md`](../CONTRIBUTING.md) - Contribution guide
- [`00-quick-start/build-and-test.md`](00-quick-start/build-and-test.md) - Build instructions

---

**Last Updated**: March 2026  
**Maintained By**: Jaunty Contributors
