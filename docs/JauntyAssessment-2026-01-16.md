# Jaunty Assessment - January 2026

## Executive Summary

Jaunty is a lightweight micro-ORM for .NET that has evolved into a capable data access library. While it started as a minimal alternative to Dapper, it now offers comparable functionality for most common scenarios. However, significant gaps remain before it can be considered production-ready for enterprise adoption.

---

## Current API Surface

### Read Operations (Complete)

| Feature                   | Sync | Async | Notes                                             |
| ------------------------- | ---- | ----- | ------------------------------------------------- |
| `Query<T>` | yes | yes | Strict mapping (all properties must match) |
| `QueryPartial<T>` | yes | yes | Lenient mapping (unmatched properties ignored) |
| `QueryFirst<T>` | yes | yes | Returns first row, throws if empty |
| `QueryFirstOrDefault<T>` | yes | yes | Returns first row or null |
| `QuerySingle<T>` | yes | yes | Returns single row, throws if not exactly one |
| `QuerySingleOrDefault<T>` | yes | yes | Returns single row or null |
| `QueryScalar<T>` | yes | yes | Returns scalar value |
| `QueryStream<T>` | yes | yes | Deferred execution (IEnumerable/IAsyncEnumerable) |
| `QueryMultiple` | yes | yes | Multiple result sets via GridReader |

### Write Operations (Complete)

| Feature                          | Sync | Async | Notes                  |
| -------------------------------- | ---- | ----- | ---------------------- |
| `Insert<T>` | yes | yes | Returns identity value |
| `Update<T>` | yes | yes | By primary key |
| `Delete<T>` | yes | yes | By entity or ID |
| `BulkInsert<T>` | yes | yes | Transaction-wrapped |
| `BulkUpdate<T>` | yes | yes | Transaction-wrapped |
| `BulkDelete<T>` | yes | yes | Transaction-wrapped |
| `BulkInsertIgnoreConstraints<T>` | yes | yes | Bypasses FK checks |
| `BulkUpdateIgnoreConstraints<T>` | yes | yes | Bypasses FK checks |
| `BulkDeleteIgnoreConstraints<T>` | yes | yes | Bypasses FK checks |

### Advanced Mapping (Complete)

| Feature                              | Status | Notes                           |
| ------------------------------------ | ------ | ------------------------------- |
| `Query<T1, T2>` | yes | Returns `List<(T1, T2)>` tuples |
| `Query<T1, T2, TResult>` | yes | Custom combiner function |
| `Query<Dictionary<string, object?>>` | yes | Dynamic dictionary mapping |
| `Query<Dictionary<string, TValue>>` | yes | Typed dictionary mapping |
| `Query<KeyValuePair<TKey, TValue>>` | yes | Single key-value pair |
| `Query<ValueTuple<...>>` | yes | Up to 8-element tuples |
| `Query<dynamic>` | yes | ExpandoObject support |
| Collection parameter expansion | yes | `WHERE id IN @ids` |

### Configuration

| Feature                   | Status | Notes                                |
| ------------------------- | ------ | ------------------------------------ |
| Custom naming conventions | yes | snake_case, PascalCase, etc. |
| Schema resolution | yes | Per-type schema configuration |
| Custom mappers | yes | Via `CommandOptions<T>.WithMapper()` |
| `IMapped<T>` interface | yes | Type-level custom mappers |

### Attributes

| Attribute             | Status | Notes                     |
| --------------------- | ------ | ------------------------- |
| `[Table]` | yes | Table name + schema |
| `[Column]` | yes | Column name mapping |
| `[Key]` | yes | Primary key designation |
| `[Ignore]` | yes | Exclude from mapping |
| `[DatabaseGenerated]` | yes | Identity/Computed columns |

### Database Support

| Database   | Status | Notes        |
| ---------- | ------ | ------------ |
| SQLite | yes | Full support |
| SQL Server | yes | Full support |
| MySQL | yes | Full support |
| PostgreSQL | yes | Full support |

---

## Comparison with Dapper

### Features Jaunty Has That Dapper Has

| Feature              | Dapper | Jaunty | Notes                                         |
| -------------------- | ------ | ------ | --------------------------------------------- |
| Basic CRUD | yes | yes | Parity |
| Async support | yes | yes | Parity |
| Multiple result sets | yes | yes | Parity |
| Custom type handlers | yes | yes | Via mappers |
| Parameter binding | yes | yes | Named + positional |
| Collection expansion | yes | yes | `WHERE IN @list` |
| Streaming | yes | yes | IEnumerable/IAsyncEnumerable |
| Multi-mapping | yes | yes | Different approach (property-name vs splitOn) |
| Dynamic queries | yes | yes | ExpandoObject |
| Dictionary mapping | yes | yes | Full support |
| Transaction support | yes | yes | Parity |
| Buffered/unbuffered | yes | yes | Via Stream methods |

### Features Dapper Has That Jaunty Lacks

| Feature                           | Priority | Difficulty | Notes                         |
| --------------------------------- | -------- | ---------- | ----------------------------- |
| **Table-Valued Parameters (TVP)** | High     | Medium     | SQL Server bulk operations    |
| **Stored procedure support**      | High     | Low        | `CommandType.StoredProcedure` |
| **Output parameters**             | Medium   | Low        | SP return values              |
| **Literal replacements**          | Medium   | Low        | `{=value}` syntax             |
| **Pseudo-positional params**      | Low      | Low        | `?param?` syntax              |
| **Type handlers registry**        | Medium   | Medium     | Global type conversion        |
| **Command recycling**             | Low      | Medium     | Performance optimization      |

### Jaunty Unique Features

| Feature                         | Notes                                        |
| ------------------------------- | -------------------------------------------- |
| **Strict mapping mode**         | Catches schema drift at dev time             |
| **Partial mapping mode**        | Explicit opt-in for lenient mapping          |
| **Property-name multi-mapping** | No `splitOn` needed                          |
| **FK constraint toggling**      | BulkIgnoreConstraints variants               |
| **Zero dependencies**           | Framework-only (except netstandard2.0 async) |
| **Static generic caching**      | Compile-once metadata                        |

---

## Strengths

### 1. **Developer Experience**

- Clear API design with consistent patterns
- Strict mapping catches bugs early
- Explicit partial mapping requires conscious choice
- Good XML documentation

### 2. **Performance Architecture**

- Expression tree compilation (one-time cost)
- Static generic caching per type
- FrozenDictionary on .NET 8+
- Minimal allocations during query execution

### 3. **Modern .NET Support**

- Multi-targeting (netstandard2.0 + net8.0)
- IAsyncEnumerable streaming
- ValueTask where appropriate
- Modern C# features

### 4. **Simplicity**

- Zero external dependencies
- Single NuGet package
- No configuration ceremony
- SQL-first approach

### 5. **Bulk Operations**

- Transaction-wrapped bulk operations
- FK constraint bypass for migrations
- Proper parameter reuse

---

## Weaknesses

### 1. **Missing Enterprise Features**

- No stored procedure support
- No Table-Valued Parameters
- No output parameter handling
- No retry/resilience policies
- No query logging/diagnostics

### 2. **Limited Ecosystem**

- No tooling (scaffolding, migrations)
- No IDE extensions
- No community plugins
- No benchmarking infrastructure

### 3. **Documentation Gaps**

- No public documentation site
- No migration guide from Dapper
- No performance comparison data
- No best practices guide

### 4. **Testing Concerns**

- 38 failing SQLite async tests (pre-existing)
- Limited database provider testing
- No integration test infrastructure for other DBs

### 5. **Production Readiness**

- No production usage track record
- No telemetry/observability
- No NuGet package published
- No semantic versioning established

---

## What Jaunty Needs to Be Taken Seriously

### Tier 1: Critical (Must Have)

1. **Fix SQLite Async Tests** - 38 failing tests undermine confidence
2. **Stored Procedure Support** - Enterprise requirement
3. **Public NuGet Package** - With proper versioning
4. **Basic Documentation** - README, getting started, API reference
5. **Performance Benchmarks** - BenchmarkDotNet comparisons vs Dapper

### Tier 2: Important (Should Have)

1. **Table-Valued Parameters** - SQL Server bulk scenarios
2. **Output Parameters** - Stored procedure return values
3. **Query Logging** - Diagnostics/debugging support
4. **Retry Policies** - Transient fault handling
5. **Global Type Handlers** - Custom type conversion registry

### Tier 3: Nice to Have

1. **CLI Tool** - Database scaffolding
2. **Source Generators** - Compile-time mapper generation
3. **Connection Pooling Integration** - Health checks
4. **OpenTelemetry Integration** - Distributed tracing
5. **Database-First Code Generation** - Entity scaffolding

---

## Competitive Position

### vs Dapper

- **Pros**: Stricter by default, cleaner multi-mapping, zero deps
- **Cons**: Less mature, smaller community, missing TVP/SP support
- **Verdict**: Viable alternative for simple scenarios, not enterprise-ready

### vs EF Core

- **Pros**: Faster, simpler, SQL-first, lighter
- **Cons**: No change tracking, no migrations, no LINQ
- **Verdict**: Different category - micro-ORM vs full ORM

### vs RepoDB

- **Pros**: Simpler API, strict mapping
- **Cons**: Less features, no fluent operations
- **Verdict**: Comparable but less feature-rich

### vs SqlKata

- **Pros**: SQL-first (no query builder), lighter
- **Cons**: No query building capability
- **Verdict**: Different philosophy - Jaunty is SQL-only

---

## Roadmap Recommendation

### Phase 1: Production Ready (4-6 weeks)

1. Fix all failing tests
2. Add stored procedure support
3. Publish to NuGet with v1.0.0
4. Create documentation site
5. Write migration guide from Dapper

### Phase 2: Enterprise Features (6-8 weeks)

1. Table-Valued Parameters
2. Output parameters
3. Query logging/diagnostics
4. Retry policies
5. Performance benchmarks

### Phase 3: Ecosystem (8-12 weeks)

1. CLI scaffolding tool
2. Source generators
3. OpenTelemetry integration
4. VS Code extension
5. Community engagement

---

## Conclusion

Jaunty has grown into a capable micro-ORM with a solid foundation. Its strict-by-default philosophy and clean API design differentiate it from Dapper. However, critical gaps in stored procedure support, failing tests, and lack of public presence prevent serious adoption.

**Current State**: Alpha/Beta quality - suitable for personal projects and evaluation
**Production Ready**: Not yet - needs SP support, test fixes, and documentation
**Enterprise Ready**: No - missing TVP, diagnostics, and proven track record

The library shows promise but requires focused effort on the Tier 1 items before it can compete seriously with established alternatives like Dapper.
