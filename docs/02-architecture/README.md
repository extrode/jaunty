# Architecture Documentation

Jaunty's architecture documentation covers design decisions, performance optimizations, and internal systems.

## Core Documents

| Document | Purpose |
|----------|---------|
| [`design-philosophy.md`](design-philosophy.md) | Core design decisions and philosophy |
| [`performance.md`](performance.md) | Performance optimizations and memory management |
| [`metadata-system.md`](metadata-system.md) | Entity metadata caching system |
| [`parameter-binding.md`](parameter-binding.md) | SQL parameter parsing and binding |
| [`connection-management.md`](connection-management.md) | Connection state handling |

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Public API Layer                            │
│  (Extension methods on IDbConnection - Query, Insert, etc.)     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Core Execution Layer                        │
│  (QueryCore, ExecuteReader, ExecuteQueryMultiple)               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Metadata Layer                              │
│  (MetadataCache<T>, MetadataBuilder, EntityMetadata)            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Parameter Layer                             │
│  (SqlParameterParser, ParameterBinder, ParameterCache)          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Mapper Layer                                │
│  (DrDispatcher, MappedCache, MultiEntityMapper)                 │
└─────────────────────────────────────────────────────────────────┘
```

## Key Design Principles

1. **Strict by Default**: Fail fast with clear error messages
2. **Zero Reflection at Runtime**: Compile expression trees once, cache forever
3. **Minimal Allocations**: Pre-size collections, use Span<T>, avoid LINQ in hot paths
4. **Connection State Respect**: Open if closed, leave open if already open
5. **Clear Error Messages**: Contextual information in all exceptions

## Performance Characteristics

| Operation | Complexity | Allocations |
|-----------|------------|-------------|
| First query for type | O(n) for metadata build | Metadata + compiled delegates |
| Subsequent queries | O(1) lookup | Entity instances only |
| Parameter binding | O(p) where p = parameters | Parameter objects |
| SQL parsing | O(s) where s = SQL length | Cached after first parse |

## Threading Model

- **Thread-safe**: All public APIs are thread-safe
- **Static caching**: Metadata cached in static generic classes
- **No locks**: Uses immutable cached data after initialization

## Multi-Targeting

| Feature | netstandard2.0 | net8.0 |
|---------|----------------|--------|
| Collections | `Dictionary<K,V>` | `FrozenDictionary<K,V>` |
| Async | `Task<T>` | `ValueTask<T>` optimization |
| Strings | Regular | `string.Create`, `Span<char>` |

---

## See Also

- [`../../00-quick-start/README.md`](../../00-quick-start/README.md) - Quick start
- [`../../01-api-reference/README.md`](../../01-api-reference/README.md) - API reference
- [`../../03-development/README.md`](../../03-development/README.md) - Development guides
