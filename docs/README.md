# Jaunty Documentation

Welcome to the Jaunty micro-ORM documentation. This directory contains all technical documentation, guides, and reference materials.

## Quick Navigation

### Getting Started
| Document | Purpose |
|----------|---------|
| [`00-quick-start/README.md`](00-quick-start/README.md) | New developer onboarding guide |
| [`00-quick-start/build-and-test.md`](00-quick-start/build-and-test.md) | Build and test commands |
| [`00-quick-start/project-structure.md`](00-quick-start/project-structure.md) | Codebase organization |

### API Reference
| Document | Purpose |
|----------|---------|
| [`01-api-reference/README.md`](01-api-reference/README.md) | API documentation index |
| [`01-api-reference/query-methods.md`](01-api-reference/query-methods.md) | Query, QueryFirst, QuerySingle, etc. |
| [`01-api-reference/query-partial-methods.md`](01-api-reference/query-partial-methods.md) | QueryPartial* methods |
| [`01-api-reference/streaming-methods.md`](01-api-reference/streaming-methods.md) | QueryStream, QueryPartialStream |
| [`01-api-reference/write-methods.md`](01-api-reference/write-methods.md) | Insert, Update, Delete, Bulk* |
| [`01-api-reference/bulk-copy-methods.md`](01-api-reference/bulk-copy-methods.md) | **NEW** Bulk copy operations |
| [`01-api-reference/multiple-result-sets.md`](01-api-reference/multiple-result-sets.md) | QueryMultiple, GridReader |
| [`01-api-reference/stored-procedures.md`](01-api-reference/stored-procedures.md) | ExecuteStoredProcedure* |
| [`01-api-reference/attributes.md`](01-api-reference/attributes.md) | [Table], [Column], [Ignore], [Key] |
| [`01-api-reference/configuration.md`](01-api-reference/configuration.md) | JauntyConfig, naming conventions |

### Architecture
| Document | Purpose |
|----------|---------|
| [`02-architecture/README.md`](02-architecture/README.md) | Architecture overview |
| [`02-architecture/design-philosophy.md`](02-architecture/design-philosophy.md) | Core design decisions |
| [`02-architecture/performance.md`](02-architecture/performance.md) | Performance optimizations |
| [`02-architecture/metadata-system.md`](02-architecture/metadata-system.md) | Entity metadata caching |
| [`02-architecture/parameter-binding.md`](02-architecture/parameter-binding.md) | SQL parameter parsing and binding |
| [`02-architecture/bulk-copy-architecture.md`](02-architecture/bulk-copy-architecture.md) | **NEW** Bulk copy architecture |

### Development
| Document | Purpose |
|----------|---------|
| [`03-development/README.md`](03-development/README.md) | Development guide index |
| [`03-development/coding-conventions.md`](03-development/coding-conventions.md) | C# coding standards |
| [`03-development/adding-new-methods.md`](03-development/adding-new-methods.md) | How to add new query methods |
| [`03-development/multi-targeting.md`](03-development/multi-targeting.md) | netstandard2.0 vs net8.0 |

### Testing
| Document | Purpose |
|----------|---------|
| [`code-coverage/coverage-checklist.md`](code-coverage/coverage-checklist.md) | Method-by-method coverage status |
| [`code-coverage/test-implementation-guide.md`](code-coverage/test-implementation-guide.md) | Testing patterns and examples |
| [`../tests/Helpers/TEST-SETUP.md`](../tests/Helpers/TEST-SETUP.md) | Database test setup guide |

### Project Information
| Document | Purpose |
|----------|---------|
| [`LIMITATIONS.md`](LIMITATIONS.md) | Known limitations and constraints |
| [`API-DESIGN.md`](API-DESIGN.md) | API design guidelines |
| [`ARCHITECTURE-DECISIONS.md`](ARCHITECTURE-DECISIONS.md) | Architecture decision records (ADRs) |
| [`CODE-REVIEW.md`](CODE-REVIEW.md) | Code review checklist |
| [`XML-DOCUMENTATION-STYLE-GUIDE.md`](XML-DOCUMENTATION-STYLE-GUIDE.md) | XML documentation standards |
| [`../README.md`](../README.md) | Project overview (root) |
| [`../CONTRIBUTING.md`](../CONTRIBUTING.md) | Contributing guide (root) |

---

## Documentation Structure

```
docs/
├── README.md                      # You are here - documentation index
├── 00-quick-start/                # Getting started guides
├── 01-api-reference/              # API documentation
├── 02-architecture/               # Architecture & design
├── 03-development/                # Development guides
├── code-coverage/                 # Test coverage documentation
└── archive/                       # Historical/archived documents
    ├── 2026-01-assessments/       # January 2026 project assessments
    ├── 2026-01-brainstorm/        # Brainstorming sessions
    ├── 2026-01-refactors/         # Refactoring notes
    └── 2026-01-api-analysis/      # API analysis documents
```

---

## For New Developers

1. **Start here**: Read `00-quick-start/README.md`
2. **Understand the API**: Browse `01-api-reference/README.md`
3. **Learn the architecture**: Read `02-architecture/design-philosophy.md`
4. **Start coding**: Follow `03-development/coding-conventions.md`

## For Contributors

2. **Check limitations**: Review [`LIMITATIONS.md`](LIMITATIONS.md)
4. **Update docs**: Keep relevant documentation current

## Archive

Historical documents from early project development are preserved in `archive/`:

| Folder | Contents |
|--------|----------|
| [`2026-01-early-development/`](archive/2026-01-early-development/) | Initial project assessments, brainstorming, and API analysis |
| [`2026-02-code-quality/`](archive/2026-02-code-quality/) | Code improvement opportunities and inconsistency tracking |
| [`2026-02-nativeaot-migration/`](archive/2026-02-nativeaot-migration/) | NativeAOT migration planning and status |

These documents are kept for reference but should not be used as primary guidance.

---

**Last Updated**: 2026-02-19  
**Documentation Maintainer**: Project Team
