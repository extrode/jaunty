# Jaunty Documentation

Comprehensive documentation for Jaunty micro-ORM, extensions, and development guides.

---

## Quick Start

New to Jaunty? Start here:

- **[00-quick-start/](00-quick-start/README.md)** - Getting started guide
  - [Project Structure](00-quick-start/project-structure.md)
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

- **[04-extensions/](04-extensions/README.md)**
  - [Jaunty.FlatFiles](04-extensions/flatfiles/) - Flat file support
  - Jaunty.Extensions.Reflection - _Coming soon_

### 5. Quality & Testing

Quality assurance and testing documentation:

- **[05-quality/](05-quality/README.md)**
  - [Production Readiness Report](05-quality/reports/production-readiness-2026-03-03.md)
  - [Code Coverage](05-quality/code-coverage/) - _Coming soon_

### 6. Releases & Planning

Release documentation and task lists:

- **[06-releases/](06-releases/README.md)**
  - [Production Readiness Tasklist](06-releases/tasklists/production-readiness-tasklist.md)
  - [Commercial Tasklist](06-releases/tasklists/commercial-tasklist.md)
  - [Test Reorganization Plan](06-releases/tasklists/jaunty-reorganize-tests.md)

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

- **[archive/](archive/README.md)** - Historical and superseded documents
  - Early development notes (2026-01)
  - Code quality initiatives (2026-02)
  - NativeAOT migration (2026-02)
  - Old API reference
  - Miscellaneous artifacts (2026-03)

> **Note**: Only use `archive/` for context and history. Use documentation outside `archive/` as the source of truth.

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

1. Choose the appropriate section (01-06)
2. Follow naming conventions: `lowercase-with-hyphens.md`
3. Add to the section's README.md
4. Update this index if needed

### Updating Links

When moving documentation:
1. Use `git mv` for proper rename tracking
2. Update all internal links
3. Update this README.md
4. Test all links

---

**Last Updated**: March 2026  
**Maintained By**: Jaunty Contributors
