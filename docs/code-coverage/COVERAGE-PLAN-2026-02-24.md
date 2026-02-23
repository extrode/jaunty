# Jaunty 100% Code Coverage Plan

**Target**: 100% Code Coverage  
**Created**: 2026-02-24  
**Current Coverage**: 75% (10816/43927 statements uncovered)

---

## Executive Summary

| Priority | Area | Coverage | Uncovered | Impact | Effort |
|----------|------|----------|-----------|--------|--------|
| **P0** | `Jaunty.Readers` | 0% | 20 stmts | High | Low |
| **P1** | `Jaunty.Fluent` | 33% | 6199 stmts | Medium | High |
| **P2** | `Jaunty.Scaffolding.Cli` | 0% | 154 stmts | Low | Low |
| **P3** | `Jaunty.Scaffolding` | 49% | 482 stmts | Low | Medium |
| **P4** | `Jaunty.SpParameters` | 60% | 29 stmts | Low | Low |
| **P5** | `Jaunty` (net8.0) | 76% | 1337 stmts | Medium | Medium |
| **P6** | `Jaunty` (netstandard2.0) | 69% | 1816 stmts | Medium | Medium |

---

## Phase 1: Quick Wins (Week 1)

### P0: Readers Namespace - 0% Coverage
**Impact**: High (core functionality)  
**Effort**: Low (20 statements)

| File | Statements | Tests Needed |
|------|-----------|--------------|
| `Readers/EntityReader.cs` | 20 | Test async streaming with `IAsyncEnumerable` |

**Action**: Add tests for `EntityReader.ReadEntitiesAsync<T>()` when `ASYNC_ENUMERABLE_SUPPORT` is defined.

---

### P2: Scaffolding CLI - 0% Coverage
**Impact**: Low (tooling, not library)  
**Effort**: Low (154 statements)

| File | Statements | Tests Needed |
|------|-----------|--------------|
| `Jaunty.Scaffolding.Cli/*` | 154 | CLI argument parsing tests |

**Action**: Either add CLI tests OR exclude from coverage (tooling).

---

### P4: SpParameters - 60% Coverage
**Impact**: Low (stored procedure support)  
**Effort**: Low (29 statements)

| Class | Coverage | Missing Tests |
|-------|----------|---------------|
| `SpParameter` | 100% | yes |
| `SpParameters` | 60% | Output parameter retrieval, null handling |

**Action**: Add tests for `SpParameters.Get<T>()`, `HasValue`, null output parameters.

---

## Phase 2: Core Library (Weeks 2-3)

### P5: Jaunty net8.0 - 76% Coverage (1337 statements)

#### By Namespace

| Namespace | Coverage | Uncovered | Priority |
|-----------|----------|-----------|----------|
| `Readers` | 0% | 20 | P0 |
| `SpParameters` | 60% | 29 | P4 |
| `Jaunty` (public API) | 71% | 1023 | High |
| `Internals` | 84% | 232 | Medium |
| `Core` | 90% | 31 | Low |
| `Configuration` | 97% | 1 | Done |
| `Attributes` | 100% | 0 | Done |

#### High-Impact Missing Tests

**1. GridReader Async Methods** (`Core/GridReader.cs`)
```csharp
// Missing: ReadAsync, ReadFirstAsync, ReadSingleAsync, etc.
// When GridReader is used with async QueryMultipleAsync
```

**2. Multi-Entity Mapper Edge Cases** (`Internals/MultiEntityMapper.cs`)
- Split reader scenarios
- Column name collision handling
- Empty result sets

**3. Parameter Binder - Collection Expansion** (`Internals/Parameters/ParameterBinder.cs`)
- `IN` clause expansion with large collections
- Multiple collection parameters in single query
- Empty collection handling

**4. Dialect-Specific SQL Generation** (`Internals/Dialects/`)
- PostgreSQL `RETURNING` clause
- SQL Server `OUTPUT INSERTED`
- MySQL/MariaDB `LAST_INSERT_ID()`

---

### P6: Jaunty netstandard2.0 - 69% Coverage (1816 statements)

Most uncovered code is **net8.0-specific** that has `#if NET8_0_OR_GREATER` guards. The actual netstandard2.0 coverage gap is smaller.

**Action**: Verify coverage report separates targets correctly.

---

## Phase 3: Fluent API (Weeks 4-6)

### P1: Jaunty.Fluent - 33% Coverage (6199 statements)

**Impact**: Medium (optional API surface)  
**Effort**: High (largest uncovered block)

#### Coverage by Feature

| Feature | Coverage | Tests Needed |
|---------|----------|--------------|
| Fluent query builder | ~40% | Chain method tests |
| Fluent configuration | ~30% | Configuration builder tests |
| Fluent entity mapping | ~25% | Mapping builder tests |

**Strategy**:
1. Start with most-used fluent methods
2. Test method chaining
3. Test configuration scenarios
4. Test error cases

---

## Phase 4: Scaffolding (Week 7)

### P3: Jaunty.Scaffolding - 49% Coverage (482 statements)

**Impact**: Low (development tooling)  
**Effort**: Medium

| Component | Coverage | Action |
|-----------|----------|--------|
| Database scaffolding | ~50% | Add integration tests |
| Entity generation | ~45% | Add generation tests |
| Template rendering | ~50% | Add template tests |

---

## Critical Rules

1. **DO NOT modify existing tests** - Only add new tests
2. **DO NOT change production code** to make tests pass
3. **Test net8.0 AND netstandard2.0** - Both targets matter
4. **Multi-database tests** - Use `[SqlServer]`, `[Postgres]`, `[MariaDB]`, `[MicrosoftSqlite]`, `[SystemSqlite]` attributes
5. **No database? Skip gracefully** - Use `[Fact(Skip = "No database")]` or theory data

---

## Test Patterns to Follow

### 1. Read Existing Tests First
```csharp
// See: tests/Jaunty.Tests/Integration/Read/QueryTests.cs
// Pattern: IClassFixture<DialectFixture> + [Theory] + dialect attributes
```

### 2. Use Dialect Attributes
```csharp
[Theory]
[SqlServer]
[Postgres]
[MariaDB]
[MicrosoftSqlite]
[SystemSqlite]
public void MyTest(DialectInfo dialect) { }
```

### 3. Test Both Sync and Async
```csharp
// For every sync method test, add async variant
public void Query_ReturnsResults() { }
public async Task QueryAsync_ReturnsResults() { }
```

### 4. Test Edge Cases
- Empty result sets
- Null values
- Large collections
- Transaction rollback
- Connection state (open/closed)

---

## Tracking Progress

Update this table as tests are added:

| Week | Goal | Coverage Gain | Status |
|------|------|---------------|--------|
| 1 | P0, P2, P4 | +3% | todo |
| 2-3 | P5 (net8.0 core) | +15% | todo |
| 4-6 | P1 (Fluent) | +10% | todo |
| 7 | P3 (Scaffolding) | +2% | todo |
| **Total** | | **+30%** | |

---

## Tools

- **dotCover** - Coverage analysis (see `docs/screenshots/dotCover-2026-02-23.png`)
- **Coverage checklist** - `docs/code-coverage/coverage-checklist.md`
- **Test guide** - `docs/code-coverage/test-implementation-guide.md`

---

## Next Actions

1. Review this plan
2. Start with P0 (Readers - 20 statements)
3. Then P2 (CLI - 154 statements)
4. Then P4 (SpParameters - 29 statements)
5. Move to P5 (Core library gaps)

---

**Last Updated**: 2026-02-24  
**Maintainer**: Development Team
