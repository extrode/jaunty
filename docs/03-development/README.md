# Development Guides

Guides for developing and extending Jaunty.

## Core Guides

| Document | Purpose |
|----------|---------|
| [`coding-conventions.md`](coding-conventions.md) | C# coding standards and conventions |
| [`adding-new-methods.md`](adding-new-methods.md) | How to add new query methods |
| [`multi-targeting.md`](multi-targeting.md) | Supporting netstandard2.0 and net8.0 |
| [`performance-checklist.md`](performance-checklist.md) | Performance optimization checklist |

## Quick Reference

### Adding a New Query Method

1. Create file in `src/Jaunty/Read/` (e.g., `QueryFoo.cs`)
2. Add extension method in `partial class Jaunty`
3. Add `where T : new()` constraint if needed
4. Delegate to `QueryCore` or `QueryCoreAsync`
5. Create async variant in `QueryFooAsync.cs`
6. Add tests in `tests/Jaunty.Tests/Integration/Sqlite/Read/`

### Adding a New Attribute

1. Create in `src/Jaunty/Attributes/`
2. Use appropriate `AttributeTargets`
3. Update `MetadataBuilder.cs` to read the attribute
4. Add tests for attribute resolution
5. Document priority: `[Attribute]` > `JauntyConfig` > default

### Modifying Parameter Binding

1. Update `SqlParameterParser.cs` for parsing changes
2. Update `ParameterBinder.cs` for binding changes
3. Update caches if needed
4. Add unit tests for edge cases

## File Organization

### Public API Files

| Location | Naming | Example |
|----------|--------|---------|
| `Read/` | `MethodName.cs` | `Query.cs`, `QueryFirst.cs` |
| `Read/` | `MethodNameAsync.cs` | `QueryAsync.cs` |
| `Write/` | `Operation.cs` | `Insert.cs`, `BulkInsert.cs` |
| `Streaming/` | `MethodStream.cs` | `QueryStream.cs` |

### Internal Files

| Location | Purpose |
|----------|---------|
| `Internals/Entity/` | Metadata caching system |
| `Internals/Parameters/` | Parameter parsing and binding |
| `Internals/Dialects/` | SQL dialect implementations |
| `Internals/Write/` | Internal write helpers |

## Coding Standards

### C# Language Features

| Feature | Usage |
|---------|-------|
| File-scoped namespaces | Required |
| Primary constructors | Use for simple cases |
| Pattern matching | Encouraged |
| Records | For immutable data |
| `readonly struct` | For small value types |
| `Span<T>` | For zero-allocation slicing |
| LINQ | Avoid in hot paths |

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Namespaces | Hierarchical | `Jaunty.Internals.Entity` |
| Public classes | PascalCase | `GridReader`, `CommandOptions` |
| Internal classes | `internal sealed` | `internal sealed class MetadataCache<T>` |
| Methods | PascalCase, verb-based | `Query<T>()`, `CreateSetter()` |
| Properties | PascalCase, noun-based | `ColumnName`, `Mapper` |
| Parameters | camelCase | `sql`, `reader`, `columnName` |
| Private fields | _camelCase | `_cache`, `_columnNameResolver` |
| Generic type params | Single letter | `<T>`, `<TResult>` |

### Async Patterns

```csharp
// Pass-through async (no resource management)
public static Task<T> QueryAsync<T>(...)
    => QueryCoreAsync<T>(...);

// Resource management async
public static async Task<T> QueryAsync<T>(...)
{
    await using var resource = await GetResourceAsync();
    return await ProcessAsync(resource);
}

// Always use ConfigureAwait(false)
await something.ConfigureAwait(false);
```

## Performance Rules

### Memory

| Rule | Example |
|------|---------|
| Pre-size collections | `new List<T>(expectedCount)` |
| Use `Span<T>` | `ReadOnlySpan<char> sqlSpan = sql.AsSpan()` |
| Avoid boxing | Use generic constraints |
| Cache reflection | Compile once, reuse forever |

### Execution

| Rule | Example |
|------|---------|
| No LINQ in hot paths | Use `for` loops |
| Inline small methods | `[MethodImpl(MethodImplOptions.AggressiveInlining)]` |
| Use `FrozenDictionary` | On .NET 8+ for lookups |
| String comparison | `StringComparison.Ordinal` |

## Testing Requirements

| Requirement | Details |
|-------------|---------|
| Unit tests | For internal components (parser, binder) |
| Integration tests | For all public APIs |
| Error case tests | Test all exception paths |
| Coverage goal | 100% for public APIs |


## Git Workflow

### Commit Messages

```
type: short description

Optional longer description.

- Bullet points for details
- Explain why, not just what
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `test`: Tests
- `refactor`: Code restructuring
- `perf`: Performance improvements

### Branch Naming

- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation
- `refactor/description` - Refactoring

## See Also

- [`../../00-quick-start/build-and-test.md`](../../00-quick-start/build-and-test.md) - Build commands
- [`../02-architecture/README.md`](../02-architecture/README.md) - Architecture overview

