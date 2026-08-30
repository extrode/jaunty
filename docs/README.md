# Jaunty Documentation

Comprehensive documentation for Jaunty micro-ORM, extensions, and development guides.

---

## Quick Start

New to Jaunty? Start here:

- **[00-quick-start/](00-quick-start/README.md)** - Getting started guide
  - [Project Layout](00-quick-start/project-layout.md) - Repository structure
  - [Build and Test](00-quick-start/build-and-test.md)

---

## Documentation Sections

### 1. API Reference

Complete API documentation for Jaunty core:

- **[01-api-reference/](01-api-reference/README.md)**
  - [Query Methods](01-api-reference/query-methods.md)
  - [Single Result Methods](01-api-reference/single-result-methods.md)
  - [Scalar Methods](01-api-reference/scalar-methods.md)
  - [Streaming Methods](01-api-reference/streaming-methods.md)
  - [CRUD Operations](01-api-reference/crud-operations.md)
  - [Bulk Copy Methods](01-api-reference/bulk-copy-methods.md)
  - [Multiple Result Sets](01-api-reference/multiple-result-sets.md)
  - [Stored Procedures](01-api-reference/stored-procedures.md)
  - [Fluent API](01-api-reference/fluent-api.md)
  - [Attributes](01-api-reference/attributes.md)
  - [Configuration](01-api-reference/configuration.md)

### 2. Architecture

Architecture documentation and design specifications:

- **[02-architecture/](02-architecture/README.md)**
  - [Design Philosophy](02-architecture/design-philosophy.md)
  - [Architecture Specification](02-architecture/architecture-specification.md)
  - [Architecture Visuals](02-architecture/architecture-visuals.md)
  - [Bulk Copy Architecture](02-architecture/bulk-copy-architecture.md)
  - [Metadata System Spec](02-architecture/metadata-system-spec.md)
  - [Parameter Binding Spec](02-architecture/parameter-binding-spec.md)
  - [Performance Spec](02-architecture/performance-spec.md)

### 3. Development Guides

Development guides and best practices:

- **[03-development/](03-development/README.md)**
  - [API Design Guidelines](03-development/api-design-guidelines.md)
  - [Code Review Checklist](03-development/code-review-checklist.md)
  - [XML Documentation Style](03-development/xml-documentation-style.md)
  - [Known Limitations](03-development/limitations.md)
  - [Optimizations](03-development/optimizations.md)

### 4. Extensions

Documentation for Jaunty extension packages:

Nine packages ship. `Extrode.Jaunty` is the core and has no dependencies of its own; everything
that would add one lives in a separate package, which is the whole reason the list is this long.

| Package | What it adds | Docs |
|---|---|---|
| `Extrode.Jaunty` | The core: query, CRUD, dialects, parameter binding | [01-api-reference/](01-api-reference/README.md) |
| `Extrode.Jaunty.Fluent` | The fluent query builder | [Fluent API](01-api-reference/fluent-api.md) |
| `Extrode.Jaunty.FlatFiles` | CSV and flat-file sources | [flatfiles/](04-extensions/flatfiles/README.md) |
| `Extrode.Jaunty.FlatFiles.DuckDB` | DuckDB-backed flat-file querying | [flatfiles/](04-extensions/flatfiles/README.md) |
| `Extrode.Jaunty.Extensions.Logging` | `ILogger` and DI integration, `LoggingInterceptor`, sensitive-parameter masking | [Configuration](01-api-reference/configuration.md) |
| `Extrode.Jaunty.Extensions.Reflection` | Reflection-based mapping and bulk copy — **shipping, not planned** | [Bulk Copy Methods](01-api-reference/bulk-copy-methods.md) |
| `Extrode.Jaunty.Extensions.Npgsql` | PostgreSQL-specific binding without boxing | [Dependencies](02-architecture/dependencies.md) |
| `Extrode.Jaunty.Scaffolding` | Model generation from a live schema | [Dependencies](02-architecture/dependencies.md) |
| `Extrode.Jaunty.Scaffolding.Cli` | The scaffolding command-line tool | [Dependencies](02-architecture/dependencies.md) |

- **[04-extensions/](04-extensions/README.md)** - the extension section index
- **[Dependencies](02-architecture/dependencies.md)** - what every package pulls in, per target framework

### 5. Quality & Testing

Quality assurance and testing documentation:

- **[05-quality/](05-quality/README.md)**
  - [Audit Record](05-quality/audit-record.md) - 36 rounds, 725 findings, and where each of the
    eight security families is closed in `src/`
  - [Code Coverage](05-quality/code-coverage.md)
  - [Production Readiness Report](05-quality/reports/PRODUCTION-READINESS-2026-07-02.md)
  - [Coverage Gap Inventory](05-quality/reports/COVERAGE-GAPS-2026-07-04.md)
  - [Benchmarks](05-quality/reports/benchmarks-2026-07-29.md)

### 6. Releases & Planning

Release documentation and task lists:

- **[06-releases/](06-releases/README.md)**
  - [Release Runbook](06-releases/RELEASE-RUNBOOK.md) - Step-by-step release process
  - [Pricing](06-releases/pricing.md) - The commercial model
  - [Production Readiness Tasklist](06-releases/tasklists/PRODUCTION-READINESS-TASKLIST.md)
  - [Commercial Tasklist](06-releases/tasklists/COMMERCIAL-TASKLIST.md)
  - [Test Reorganization Plan](06-releases/tasklists/jaunty-reorganize-tests.md)

### 7. Design

Visual design source for the Jaunty docs site:

- **[07-design/](07-design/README.md)**
  - [`Jaunty Docs App.dc.html`](07-design/Jaunty%20Docs%20App.dc.html) - Primary design (authoritative layout target)
  - [`Jaunty Docs.dc.html`](07-design/Jaunty%20Docs.dc.html) - Secondary design reference
  - [`support.js`](07-design/support.js) - Design-tool runtime (view `.dc.html` files in browser)

### 8. Learn

Hands-on guides for people writing Jaunty code for the first time, or porting to it:

- **[08-learn/](08-learn/README.md)** - Your first hour with Jaunty, a runnable walkthrough
  - [Exercises](08-learn/exercises.md)
  - [Migrating to Jaunty](08-learn/migrating/README.md) - from [Dapper](08-learn/migrating/from-dapper.md) or [EF Core](08-learn/migrating/from-ef-core.md), and the [strict-mapping rule](08-learn/migrating/strict-mapping.md) to read first

### Decisions

Dated architecture and commercial decision records: **[decisions/](decisions/)**

---

## Building the Docs Site

`scripts/build-docs.sh` runs the shared `Docs` tool (sibling repo `../docs`), which converts this markdown tree into a self-contained static HTML site matching the design in `07-design/`. Page layout, CSS, and nav JS are generated by the tool itself, not read from this repo.

```bash
# From repo root — generate into dist/docs-site/
./scripts/build-docs.sh

# Open dist/docs-site/index.html in a browser to review
```

**Output** is written to `dist/docs-site/`, which is **committed** as the published doc site —
`.gitignore` has `/dist/*` then `!/dist/docs-site/`. Regenerating and not committing the result
leaves a published site that disagrees with the markdown it came from, so the two move together.  
**Design tokens** and implementation details are documented in [`07-design/README.md`](07-design/README.md).

---

## For Contributors

### Essential Reading

- [CONTRIBUTING.md](../CONTRIBUTING.md) - How to contribute
- [03-development/code-review-checklist.md](03-development/code-review-checklist.md) - Code review standards

### Documentation Guidelines

- [03-development/xml-documentation-style.md](03-development/xml-documentation-style.md) - XML documentation standards
- [03-development/api-design-guidelines.md](03-development/api-design-guidelines.md) - API design patterns

---

## Historical Documents

- **[99-archive/](99-archive/README.md)** - Historical and superseded documents
  - Early development notes (2026-01)
  - Code quality initiatives (2026-02)
  - NativeAOT migration (2026-02)
  - Miscellaneous artifacts (2026-03+)

> **Note**: Only use `99-archive/` for context and history. Use documentation outside `99-archive/` as the source of truth.

---

## Media Assets

- **[_assets/](_assets/README.md)** - Screenshots, diagrams, and other media
  - Screenshots for documentation examples
  - Architecture diagrams
  - Flow charts and sequence diagrams

---

## Search Tips

### Finding API Documentation

- Query methods → `01-api-reference/query-methods.md`
- CRUD operations → `01-api-reference/crud-operations.md`
- Attributes → `01-api-reference/attributes.md`

### Finding Architecture Docs

- System overview → `02-architecture/architecture-specification.md`
- Visual diagrams → `02-architecture/architecture-visuals.md`
- Performance → `02-architecture/performance-spec.md`

### Finding Development Guides

- API design → `03-development/api-design-guidelines.md`
- Code review → `03-development/code-review-checklist.md`
- Limitations → `03-development/limitations.md`

---

## Documentation Maintenance

### Adding New Documentation

1. Choose the appropriate section (01-09)
2. Follow naming conventions: `lowercase-with-hyphens.md`
3. Add to the section's README.md
4. Update this index if needed

**Naming Convention**: See [`03-development/file-naming-convention.md`](03-development/file-naming-convention.md) for the complete standard.

### Updating Links

When moving documentation:
1. Use `git mv` for proper rename tracking
2. Update all internal links
3. Update this README.md
4. Test all links

---

**Last updated**: 2026-08-31
