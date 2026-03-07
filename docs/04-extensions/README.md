# Jaunty Extensions

Documentation for Jaunty extension packages that build on top of Jaunty core.

---

## Available Extensions

### Jaunty.FlatFiles

Flat file support for Jaunty, enabling SQL queries on CSV, TSV, Parquet, JSON, and other file formats using DuckDB as the embedded query engine.

**Documentation**:
- [`flatfiles/README.md`](flatfiles/README.md) - FlatFiles overview
- [`flatfiles/architecture.md`](flatfiles/architecture.md) - Architecture and design
- [`flatfiles/code-analysis.md`](flatfiles/code-analysis.md) - Code analysis report

**Key Features**:
- Query flat files using SQL
- Support for CSV, TSV, Parquet, JSON, Excel, Delta Lake, Iceberg
- DuckDB embedded query engine
- Import/export to external databases
- Full CRUD operations on file data

### Jaunty.Extensions.Reflection

Reflection-based entity mapping for scenarios where source generation is not available.

**Documentation**: _Coming soon_

**Key Features**:
- Runtime reflection mapping
- Compatible with NativeAOT (with manual initialization)
- Fallback for dynamic scenarios

---

## Extension Architecture

Extensions follow the same design principles as Jaunty core:

1. **Zero dependencies** - Extensions only depend on Jaunty core
2. **Consistent API** - Match Jaunty core patterns and naming
3. **Performance-first** - Optimized for minimal allocations
4. **NativeAOT compatible** - Support for AOT compilation scenarios

---

## For Extension Developers

### Design Guidelines

When creating new extensions for Jaunty:

1. **Match core patterns** - Use the same folder structure and naming conventions
2. **Document thoroughly** - Follow the documentation structure in this folder
3. **Test extensively** - Maintain high test coverage like core
4. **Consider AOT** - Design for NativeAOT compatibility from the start

### Documentation Structure

```
04-extensions/
├── README.md                      # This file - extension overview
├── {extension-name}/
│   ├── README.md                  # Extension overview
│   ├── architecture.md            # Architecture documentation
│   ├── api-reference.md           # API documentation
│   └── code-analysis.md           # Code analysis (if applicable)
```

---

## See Also

| Document | Purpose |
|----------|---------|
| [`../01-api-reference/README.md`](../01-api-reference/README.md) | Jaunty core API reference |
| [`../02-architecture/README.md`](../02-architecture/README.md) | Jaunty core architecture |
| [`../03-development/README.md`](../03-development/README.md) | Development guides |
