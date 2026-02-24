# Jaunty 100% Code Coverage Plan

**Target**: 100% Code Coverage for both net8.0 and netstandard2.0  
**Created**: 2026-02-24  
**Coverage Report**: dotCover-2026-02-24.png  
**Last Updated**: 2026-02-24 (Phase 2 started - GridReader tests added)

---

## Current Coverage Status

| Target | Coverage | Uncovered | Total | Status |
|--------|----------|-----------|-------|--------|
| **net8.0** | 76% → 77% | ~1300 | 5503 | Improving |
| **netstandard2.0** | 70% | 1800 | 5907 | Critical |
| **Overall** | 75% → 76% | ~10700 | 42835 | Improving |

---

## Progress Log

### 2026-02-24: Test Quality Fix - SQLite Coverage
**Issue**: GridReader tests missing `[MicrosoftSqlite][SystemSqlite]` attributes  
**Impact**: 44 additional tests now running (68 → 112 tests)  
**Action**: Added SQLite dialect to all GridReader tests  
**Result**: All 112 tests passing

**Lesson**: ALL non-async, non-stored-procedure tests MUST include all 5 dialects:
- `[SqlServer]`
- `[Postgres]`  
- `[MariaDB]`
- `[MicrosoftSqlite]`
- `[SystemSqlite]`

**Exceptions** (SQLite doesn't support):
- Async stored procedure tests
- `QueryMultipleAsync` with stored procedures

### 2026-02-24: Phase 1 - Quick Wins COMPLETE
**Added**: 11 new tests

**SpParameter Tests** (3 tests):
- `SpParameter_Constructor_WithAllParameters_SetsProperties` - Full constructor test
- `SpParameter_Constructor_WithNullValue_SetsNullValue` - Null value handling
- `SpParameter_Constructor_WithoutDbTypeAndSize_SetsNulls` - Optional parameters

**SpecialTypeMapper Tests** (8 test methods):
- `SpecialTypeMapperIntegrationTests` - Integration tests for Dictionary, KeyValuePair, ValueTuple, Dynamic mapping
- Fixed PostgreSQL column name folding issue (uses quoted identifiers)

**Test Count**: 2309 → 2324 (+15 tests)  
**Coverage Impact**: +0.5% estimated

**Result**: Phase 1 complete - all quick wins achieved

---

### 2026-02-24: Phase 3 - Dialect Tests COMPLETE
**Added**: 182 new unit tests in `SqlDialectTests.cs`

**Coverage Areas**:
- **SqlServerDialect** (10 tests): Escape methods, keywords, paging, identity, FK toggle
- **PostgreSqlDialect** (14 tests): Escape methods, keywords, paging, RETURNING, FK toggle
- **MySqlDialect** (13 tests): Escape methods, keywords, paging, LAST_INSERT_ID, FK toggle
- **SQLiteDialect** (22 tests): Escape methods, keywords, paging, last_insert_rowid, FK toggle, GLOB patterns

**Test Count**: 2324 → 2506 (+182 tests)  
**Coverage Impact**: +2% estimated (106 dialect statements covered)

**Result**: Phase 3 complete - all dialect methods tested

---

## Phase 4: Public API Coverage - IN PROGRESS

### 2026-02-24: SQLite Coverage Expansion
**Issue**: Many integration tests missing `[MicrosoftSqlite][SystemSqlite]` attributes  
**Impact**: +257 tests added (2506 → 2763)  
**Files Fixed**:
- All `Integration/Read/*AsyncTests.cs` (12 files)
- All `Integration/Read/*.cs` sync tests
- All `Integration/Write/*.cs` tests

**Result**: All async and sync API methods now tested against SQLite

### 2026-02-24: CancellationToken Tests Added
**Added**: 6 new CancellationToken tests for async methods
- `QueryPartialAsync_WithCancellationToken_Works`
- `QueryPartialFirstAsync_WithCancellationToken_Works`
- `QueryPartialFirstOrDefaultAsync_WithCancellationToken_Works`
- `QueryPartialSingleAsync_WithCancellationToken_Works`
- `QueryPartialSingleOrDefaultAsync_WithCancellationToken_Works`
- `ExecuteScalarAsync_WithCancellationToken_Works`

**Test Count**: 2763 → 2769 (+6 tests)  
**Result**: All async methods now have CancellationToken coverage

### 2026-02-24: Transaction Rollback Tests Fixed
**Fixed**: 2 failing tests (Postgres/SQL Server)
- Used temporary tables instead of modifying Northwind
- Created temp tables OUTSIDE transaction, inserts INSIDE transaction
- Dialect-specific temp table syntax (TEMP TABLE, #temp, TEMPORARY TABLE)

**Test Count**: 2769 → 2760 (consolidated)  
**Result**: All transaction rollback tests passing

### 2026-02-24: Error Path Tests Added
**Added**: 20 new null validation tests for Write async methods
- `InsertAsync_NullConnection_ThrowsArgumentNullException`
- `UpdateAsync_NullConnection_ThrowsArgumentNullException`
- `DeleteAsync_NullConnection_ThrowsArgumentNullException`
- `UpsertAsync_NullConnection_ThrowsArgumentNullException`

**Test Count**: 2607 → 2627 (+20 tests)  
**Result**: All Write API null validation covered

### 2026-02-24: CommandOptions Overload Tests Added
**Added**: 18 new CommandOptions combination tests
- `Query_WithParametersAndTimeout_ExecutesCorrectly`
- `QueryAsync_WithTimeoutOption_ExecutesCorrectly`
- `QueryAsync_WithParametersAndTimeout_ExecutesCorrectly`
- All tests cover all 5 dialects

**Test Count**: 2615 → 2633 (+18 tests)  
**Result**: CommandOptions overload combinations covered

---

## Current Status

**Test Count**: 2633 tests (VS Test Explorer)  
**Pass Rate**: 100% (2633/2633 passing)
**Overall Progress**: 4/10 phases complete (40%)

---

## Remaining Phase 4 Work
- [ ] Additional CommandOptions combinations (transaction + timeout)

## Coverage by Project (Priority Order)

| Priority | Project | Coverage | Uncovered | Total | Action |
|----------|---------|----------|-----------|-------|--------|
| **P0** | Jaunty.Fluent | 33% | 6199 | 9288 | Major test expansion needed |
| **P1** | Jaunty (netstandard2.0) | 62% | 1557 | 4083 | Public API tests missing |
| **P2** | Jaunty (net8.0) | 71% | 1047 | 3600 | Public API tests missing |
| **P3** | Jaunty.Scaffolding | 49% | 482 | 940 | Tooling - consider exclude |
| **P4** | Jaunty.Scaffolding.Cli | 0% | 154 | 154 | Tooling - exclude |
| **P5** | Jaunty.Internals.Dialects | 82% | 106 | 580 | Dialect-specific tests |
| **P6** | Jaunty.Internals.Read | 83% | 13 | 78 | Minor gaps |
| **P7** | Jaunty.Core | 88% | 35 | 302 | Minor gaps |
| **P8** | Jaunty.Internals.Parameters | 89% | 45 | 405 | Minor gaps |
| **P9** | Jaunty.SpParameter | 90% | 1 | 10 | One line |
| **P10** | Jaunty.Extensions.Reflection | 95% | 51 | 1070 | Near complete |

---

## Phase 1: Quick Wins (< 1 day)

### P9: SpParameter - 1 Line Missing
**File**: `src/Jaunty/StoredProcedure/SpParameter.cs`  
**Uncovered**: 1 statement  
**Action**: Add test for constructor with all parameters

**Test to Add**:
```csharp
[Fact]
public void SpParameter_Constructor_WithAllParameters_SetsProperties()
{
    var param = new SpParameter("TestParam", 42, ParameterDirection.Output, DbType.Int32, 100);
    
    param.Name.Should().Be("TestParam");
    param.Value.Should().Be(42);
    param.Direction.Should().Be(ParameterDirection.Output);
    param.DbType.Should().Be(DbType.Int32);
    // Size property test
}
```

---

### P10: Jaunty.Extensions.Reflection - 51 Statements
**Current**: 95% coverage  
**Action**: Review uncovered lines, likely edge cases in SpecialTypeMappers

**Tests to Add**:
- Dictionary mapper edge cases
- ExpandoObject mapper with null values
- ValueTuple with many elements (>7)

---

## Phase 2: Core Library Gaps (2-3 days)

### P7: Jaunty.Core - 35 Statements (GridReader)
**Files**: `src/Jaunty/Core/GridReader.cs`  
**Uncovered**: 35/302 statements

**Missing Tests**:
- GridReader.Dispose() explicit call
- GridReader.DisposeAsync() 
- Read methods with custom mappers (options.Mapper)
- Error paths when reader is already consumed

**Tests to Add** (`tests/Jaunty.Tests/Integration/Multiple/GridReaderTests.cs`):
```csharp
[Fact]
public void GridReader_Dispose_ClosesReader()
{
    using var grid = _connection.QueryMultiple("SELECT 1; SELECT 2");
    grid.ReadFirst<int>();
    grid.Dispose();
    // Verify reader is closed
}

#if NET8_0_OR_GREATER
[Fact]
public async Task GridReader_DisposeAsync_ClosesReader()
{
    await using var grid = await _connection.QueryMultipleAsync("SELECT 1; SELECT 2");
    await grid.ReadFirstAsync<int>();
    await grid.DisposeAsync();
    // Verify reader is closed
}
#endif

[Fact]
public void GridReader_Read_WithCustomMapper_UsesMapper()
{
    using var grid = _connection.QueryMultiple("SELECT category_id, category_name FROM categories LIMIT 1");
    var results = grid.Read<Category>(new CommandOptions<Category> { Mapper = CustomMapper });
    // Verify custom mapper was used
}

[Fact]
public void GridReader_Read_AfterConsumed_Throws()
{
    using var grid = _connection.QueryMultiple("SELECT 1");
    grid.ReadFirst<int>();
    Assert.Throws<InvalidOperationException>(() => grid.ReadFirst<int>());
}
```

---

### P6: Jaunty.Internals.Read - 13 Statements
**Files**: `src/Jaunty/Internals/Read/`  
**Uncovered**: 13/78 statements

**Likely Missing**:
- Error paths in ExecuteReader
- Edge cases in MultiEntityMapper

---

### P8: Jaunty.Internals.Parameters - 45 Statements
**Files**: `src/Jaunty/Internals/Parameters/`  
**Uncovered**: 45/405 statements

**Missing Tests** (`tests/Jaunty.Tests/Unit/Read/ParameterBinderTests.cs`):
- SqlParameterParser with complex SQL (subqueries, CTEs)
- ParameterBinder with null parameter values
- ParameterBinder with duplicate parameter names in SQL

---

## Phase 3: Dialect-Specific Tests (1-2 days)

### P5: Jaunty.Internals.Dialects - 106 Statements
**Files**: `src/Jaunty/Internals/Dialects/`  
**Uncovered**: 106/580 statements

**Missing Tests** (Create `tests/Jaunty.Tests/Unit/Dialects/`):
- SqlServerDialect: EscapeIdentifier, GetDisableForeignKeyChecksSql
- PostgreSqlDialect: FoldToLowerCase behavior
- MySqlDialect vs MariaDb differences
- SQLiteDialect: Limit without offset

**Test Pattern**:
```csharp
public class SqlServerDialectTests
{
    private readonly SqlServerDialect _dialect = new();

    [Theory]
    [InlineData("table", "[table]")]
    [InlineData("Table Name", "[Table Name]")]
    [InlineData("table]name", "[table]]name]")]
    public void EscapeIdentifier_EscapesCorrectly(string input, string expected)
    {
        _dialect.EscapeIdentifier(input).Should().Be(expected);
    }

    [Fact]
    public void GetDisableForeignKeyChecksSql_ReturnsCorrectSql()
    {
        _dialect.GetDisableForeignKeyChecksSql().Should()
            .Be("ALTER DATABASE CURRENT SET CONSTRAINT_ALL OFF");
    }
}
```

---

## Phase 4: Public API Coverage (3-5 days) - CRITICAL

### P2: Jaunty (net8.0) - 1047 Statements
### P1: Jaunty (netstandard2.0) - 1557 Statements

**Files**: `src/Jaunty/Jaunty.*.cs` (all public API files)  
**Uncovered**: 2604 statements combined

**Strategy**: Most uncovered code is **netstandard2.0 specific** or error paths. Focus on:

1. **Async methods with CancellationToken** - Many async variants not fully tested
2. **CommandOptions overloads** - Some option combinations not tested
3. **Error paths** - Null/empty validation, connection state checks

**Test Pattern** (match existing patterns in `tests/Jaunty.Tests/Integration/`):
```csharp
[Theory]
[SqlServer]
[Postgres]
[MariaDB]
[MicrosoftSqlite]
[SystemSqlite]
public void Method_WithAllOverloads_Works(DialectInfo dialect)
{
    using var connection = _fixture.GetConnection(dialect);
    
    // Test all parameter combinations
    var result1 = connection.Method("SQL");
    var result2 = connection.Method("SQL", new { Param = 1 });
    var result3 = connection.Method("SQL", CommandOptions.WithTimeout(30));
    var result4 = connection.Method("SQL", new { Param = 1 }, CommandOptions.WithTimeout(30));
    
    // Verify all returned expected results
}
```

---

## Phase 5: Fluent API - Major Effort (5-10 days)

### P0: Jaunty.Fluent - 6199 Statements
**Current**: 33% coverage (3089/9288)  
**Gap**: 6199 statements

**Current Tests**: 395 tests (371 passing, 24 failing from IGrouping rename)

**Analysis**: This is the BIGGEST gap - 57% of all uncovered code!

**Missing Coverage Areas**:
1. **JoinExpressionVisitor** - Complex join scenarios
2. **SelectExpressionVisitor** - Complex projections
3. **WhereExpressionVisitor** - Complex predicates
4. **GroupByExpressionVisitor** - After IGrouping fix
5. **QueryBuilder** - All method combinations
6. **JoinedQueryBuilder** - Multi-table queries
7. **GroupedQueryBuilder** - Having clauses, aggregate functions

**Test Organization** (match existing pattern in `tests/Jaunty.Fluent.Tests/`):
```
tests/Jaunty.Fluent.Tests/
├── Integration/
│   ├── FluentJoinTests.cs          (Already exists - expand)
│   ├── FluentSelectTests.cs        (Already exists - expand)
│   ├── FluentWhereTests.cs         (Already exists - expand)
│   ├── FluentGroupByTests.cs       (Already exists - fix IGrouping)
│   ├── FluentOrderByTests.cs       (Already exists)
│   ├── FluentTakeSkipTests.cs      (Already exists)
│   ├── FluentAggregateTests.cs     (Already exists - expand)
│   ├── FluentCteTests.cs           (Already exists - expand)
│   ├── FluentSubqueryTests.cs      (Already exists - expand)
│   ├── FluentSetOperationsTests.cs (Already exists - expand)
│   ├── FluentWindowFunctionTests.cs (Already exists - expand)
│   └── FluentWriteOperationsTests.cs (Already exists - expand)
└── Unit/
    ├── Expressions/
    │   ├── JoinExpressionVisitorTests.cs    (NEW)
    │   ├── SelectExpressionVisitorTests.cs  (NEW)
    │   ├── WhereExpressionVisitorTests.cs   (NEW)
    │   └── GroupByExpressionVisitorTests.cs (NEW)
    └── QueryBuilderTests.cs                 (NEW)
```

**Unit Test Pattern** (for Expression visitors):
```csharp
public class WhereExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();
    private readonly ParameterCollection _parameters = new();

    [Fact]
    public void Visit_EqualityComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.Id == 1;
        var visitor = new WhereExpressionVisitor<Product>(_dialect, _parameters);
        var result = visitor.Translate(expr);
        
        result.Should().Be("[Id] = @Id");
        _parameters.Should().ContainKey("Id");
    }

    [Fact]
    public void Visit_AndAlso_CombinesWithAnd()
    {
        Expression<Func<Product, bool>> expr = p => p.Id == 1 && p.Name == "Test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect, _parameters);
        var result = visitor.Translate(expr);
        
        result.Should().Be("[Id] = @Id AND [Name] = @Name");
    }

    [Fact]
    public void Visit_OrElse_CombinesWithOr()
    {
        Expression<Func<Product, bool>> expr = p => p.Id == 1 || p.Id == 2;
        var visitor = new WhereExpressionVisitor<Product>(_dialect, _parameters);
        var result = visitor.Translate(expr);
        
        result.Should().Be("[Id] = @Id OR [Id] = @Id2");
    }
}
```

---

## Phase 6: Scaffolding Decision (1 day)

### P3 & P4: Scaffolding Projects - 636 Statements

**Recommendation**: **EXCLUDE from coverage target**

**Rationale**:
- Jaunty.Scaffolding.Cli: 0% coverage, 154 statements - CLI tooling
- Jaunty.Scaffolding: 49% coverage, 482 statements - Development tooling
- Total impact: 636 statements (6% of uncovered code)
- These are development tools, not runtime library code

**Action**: Add `<ExcludeFromCoverage>` to .csproj files or update coverage report filters.

---

## Summary: Effort vs. Impact

| Phase | Effort | Coverage Gain | Priority |
|-------|--------|---------------|----------|
| Phase 1: Quick Wins | < 1 day | +0.5% | Do first |
| Phase 2: Core Library | 2-3 days | +3% | High impact |
| Phase 3: Dialects | 1-2 days | +2% | Medium |
| Phase 4: Public API | 3-5 days | +10% | CRITICAL |
| Phase 5: Fluent API | 5-10 days | +20% | BIGGEST GAP |
| Phase 6: Scaffolding | 1 day | Exclude | Decision |

**Total Estimated Effort**: 12-21 days  
**Expected Final Coverage**: 95-98% (excluding Scaffolding)

---

## Immediate Next Steps

1. **Fix IGrouping namespace issue** in Fluent API tests (24 tests failing)
2. **Add Phase 1 quick win tests** (< 1 day, easy wins)
3. **Start Phase 4 Public API tests** (biggest impact for effort)
4. **Parallel track**: Phase 5 Fluent API unit tests (largest gap)

---

## Test Code Standards

All new tests MUST follow existing patterns:

### Dialect Coverage Requirements CRITICAL

**ALL integration tests MUST include all 5 dialects UNLESS:**
- Testing async stored procedures (SQLite doesn't support)
- Testing SQL Server/Postgres/MySQL-specific features

**Required attributes for standard tests:**
```csharp
[Theory]
[SqlServer]
[Postgres]
[MariaDB]
[MicrosoftSqlite]
[SystemSqlite]
public void MyTest(DialectInfo dialect) { }
```

**SQLite Exceptions** (only for these features):
- Async stored procedure execution
- `QueryMultipleAsync` with stored procedures

**Dialect-Specific SQL Handling:**
```csharp
var sql = dialect.Provider == DialectProvider.SqlServer
    ? "SELECT TOP (1) * FROM Categories"
    : "SELECT * FROM categories LIMIT 1";
```

### Integration Tests
- Use `[Theory]` with ALL FIVE dialect attributes
- Use `IClassFixture<DialectFixture>` for connection management
- Use `_fixture.GetConnection(dialect)` for database connections
- Handle SQL dialect differences with ternary operators
- File location: `tests/Jaunty.Tests/Integration/<Area>/`

### Unit Tests
- Use `[Fact]` for simple tests
- Use `[Theory]` with `[InlineData]` for parameterized tests
- Mock external dependencies
- File location: `tests/Jaunty.Tests/Unit/<Area>/`

### Naming Conventions
- Test class: `<MethodOrFeature>Tests.cs`
- Test method: `<Method>_<Scenario>_<ExpectedResult>()`
- Example: `Query_WithNullParameter_HandlesGracefully()`

### Assertions
- Use FluentAssertions: `result.Should().Be(expected)`
- Use `Assert.Throws<T>()` for exceptions
- Avoid magic numbers - use named constants

---

## Next Big Phase: Phase 1 - Quick Wins (< 1 day)

**Target**: SpParameter and Extensions.Reflection (52 statements total)

### P9: SpParameter - 1 Statement Missing
**File**: `src/Jaunty/StoredProcedure/SpParameters.cs` (SpParameter class)  
**Current**: 90% coverage  
**Missing**: Constructor with all parameters (Size property test)

**Test to Add** (`tests/Jaunty.Tests/Unit/StoredProcedures/SpParametersTests.cs`):
```csharp
[Fact]
public void SpParameter_Constructor_WithAllParameters_SetsProperties()
{
    var param = new SpParameter("TestParam", 42, ParameterDirection.Output, DbType.Int32, 100);
    
    param.Name.Should().Be("TestParam");
    param.Value.Should().Be(42);
    param.Direction.Should().Be(ParameterDirection.Output);
    param.DbType.Should().Be(DbType.Int32);
    param.Size.Should().Be(100);
}
```

### P10: Jaunty.Extensions.Reflection - 51 Statements
**Current**: 95% coverage  
**Files**: `src/Jaunty.Extensions.Reflection/`

**Missing Tests**:
- SpecialTypeMappers with edge cases
- Dictionary mapper with null values
- ValueTuple with many elements (>7)
- ExpandoObject mapper error paths

**Action**: Review dotCover report for exact uncovered lines in SpecialTypeMappers.cs

---

**Estimated Time**: < 1 day  
**Expected Coverage Gain**: +0.5% overall
