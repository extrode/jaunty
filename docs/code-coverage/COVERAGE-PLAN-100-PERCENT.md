# Jaunty 100% Code Coverage Plan

**Target**: 100% Code Coverage for both net8.0 and netstandard2.0
**Created**: 2026-02-24
**Coverage Report**: code-coverage-dotCover-2026-02-26.png
**Last Updated**: 2026-02-26 (Fluent API net8.0: 33% → 72%)
**Test Count**: 2865 (Visual Studio) - All passing

---

## Current Coverage Status

| Target | Coverage | Uncovered | Total | Status |
|--------|----------|-----------|-------|--------|
| **Total** | 76% | 11137 | 45590 | Improving |
| **tests/** | 95% | 1234 | 22478 | Excellent |
| **src/** | 57% | 9903 | 23112 | Needs work |
| **Jaunty (net8.0)** | 76% | 1311 | 5503 | Good |
| **Jaunty (netstandard2.0)** | 70% | 1778 | 5907 | Critical |
| **Jaunty.Fluent (net8.0)** | 72% | 1345 | 4734 | Major improvement! |
| **Jaunty.Fluent (netstandard2.0)** | 0% | 4734 | 4734 | No tests |

---

## Progress Log

### 2026-02-26: FluentAssertions Removal
**Action**: Removed FluentAssertions dependency from all test projects
**Reason**: Licensing restrictions (community license doesn't allow commercial use)
**Replacement**: xUnit Assert exclusively
**Files Updated**: All test projects and the coding standards
**Result**: All 2865 tests passing with xUnit Assert only

### 2026-02-26: Fluent API Performance Fix
**Issue**: Fluent API tests took 69 seconds to run
**Root Cause**: 19 test classes each creating separate SQLite databases
**Fix**: Created `FluentDatabaseFixture` shared fixture (IClassFixture pattern)
**Result**: Tests now run in 3 seconds (23x faster)

### 2026-02-26: Fluent API Coverage Surge
**Before**: Jaunty.Fluent net8.0: 33% coverage
**After**: Jaunty.Fluent net8.0: 72% coverage (+39%!)
**Tests Added**: 
- 87 unit tests for Expression visitors (Where, Select, Join, GroupBy)
- Complex nested WHERE predicate tests
- Window function SQL generation tests
**Remaining**: Jaunty.Fluent netstandard2.0: 0% (no tests run on this target)
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

**Test Count**: 2977 tests (Jaunty.Tests) + 408 tests (Fluent API) = **3385 total**  
**Pass Rate**: 100% (3385/3385 passing)
**Overall Progress**: 7/10 phases complete (70%)

---

## Phase 4: Public API Coverage - COMPLETE
## Phase 5: Fluent API Coverage - IN PROGRESS
## Phase 6: Dialect-Specific SQL Tests - COMPLETE

### 2026-02-24: Dialect Function Tests Added
**Added**: 68 new dialect function tests covering all 4 dialects
- **Case-sensitive/insensitive LIKE**: 4 tests (COLLATE, ILIKE, GLOB)
- **Coalesce/IsNull/NullIf**: 12 tests (COALESCE, ISNULL, IFNULL, NULLIF)
- **String functions**: 12 tests (LEN, LENGTH, SUBSTRING, SUBSTR)
- **Date functions**: 12 tests (YEAR, MONTH, DAY, EXTRACT, STRFTIME)
- **GLOB pattern escaping**: 9 tests (SQLite)

**Dialect Test Count**: 158 → 226 (+68 tests)  
**Result**: All dialect SQL generation methods covered

---

## Remaining Work

### Phase 5: Fluent API Coverage (netstandard2.0)
**Target**: 4734 uncovered statements (0% coverage)
**Issue**: No tests run on netstandard2.0 target
**Options**:
1. Add netstandard2.0 tests (doubles test maintenance)
2. Exclude netstandard2.0 from coverage (recommend)
3. Drop netstandard2.0 support for Jaunty.Fluent

**Estimated Effort**: Decision needed
**Estimated Coverage Gain**: +20% if excluded

### Phase 5b: Fluent API Coverage (net8.0)
**Current**: 72% coverage (1345/4734 uncovered)
**Progress**: Major improvement from 33% → 72% (+39%)
**Remaining Gaps**:
- `Sql.*` builder classes: 0% (44 statements)
- `CaseBuilder<>`: 0% (6 statements)
- `WindowBuilder<>`: 0% (13 statements)
- `WindowAggregateBuilder<>`: 0% (6 statements)
- `JoinedQueryBuilder<>`: 51% (431/884 statements)
- `QueryBuilder<>`: 71% (387/1314 statements)
- `SetOperationBuilder<>`: 71% (77/266 statements)

**Action**: Execution tests for builder classes
**Estimated Effort**: 2-3 days
**Estimated Coverage Gain**: +15-20%

---

### Phase 7: Internal Parameter Binding Tests
**Target**: ParameterBinder edge cases, SqlParameterParser edge cases

**Already Covered**:
- Basic parameter binding
- Duplicate parameters
- Null values
- Collection parameters
- SQL parsing edge cases

**Remaining**:
- [ ] Very large parameter lists (1000+ parameters)
- [ ] Unicode parameter names
- [ ] Special characters in parameter names

**Estimated Effort**: 0.5 days  
**Estimated Coverage Gain**: +1%

---

## Coverage by Project (Priority Order)

| Priority | Project | Coverage | Uncovered | Total | Action |
|----------|---------|----------|-----------|-------|--------|
| **P0** | Jaunty.Fluent (net8.0) | 72% | 1345 | 4734 | Builder execution tests |
| **P1** | Jaunty.Fluent (netstandard2.0) | 0% | 4734 | 4734 | Exclude from coverage |
| **P2** | Jaunty (net8.0) | 76% | 1311 | 5503 | Good coverage |
| **P3** | Jaunty (netstandard2.0) | 70% | 1778 | 5907 | Minor gaps |
| **P4** | Jaunty.Extensions.Reflection | 91% | 99 | 1140 | Near complete |
| **P5** | Jaunty.Scaffolding | 49% | 482 | 940 | Tooling - consider exclude |
| **P6** | Jaunty.Scaffolding.Cli | 0% | 154 | 154 | Tooling - exclude |

**Notes**:
- Jaunty.Fluent net8.0 improved from 33% → 72% (+39%) on 2026-02-26
- Jaunty.Fluent netstandard2.0 has zero tests - recommend exclusion
- Jaunty.Extensions.Reflection at 91% - excellent progress

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
