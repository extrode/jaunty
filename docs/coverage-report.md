# Jaunty Code Coverage Report
**Date:** January 11, 2026

## Overview
This report provides a comprehensive analysis of the Jaunty micro-ORM code coverage, identifying tested and untested public APIs, internal code paths, and recommendations for achieving 100% coverage.

## Public API Coverage Analysis

### Well-Tested APIs (High Coverage)

#### Query Methods
- **`Query<T>()`** - **Well Tested**
  - Tests: `QueryTests.cs`
  - Coverage: Full coverage of basic functionality, parameters, strict mapping, error cases
  - Grade: A+

- **`QueryAsync<T>()`** - **Well Tested**
  - Tests: `QueryAsyncTests.cs`
  - Coverage: Basic functionality, parameters, cancellation tokens
  - Grade: A

- **`QueryScalar<T>()`** - **Well Tested**
  - Tests: `QueryScalarTests.cs`
  - Coverage: Various return types, parameters, null handling
  - Grade: A

- **`QueryScalarAsync<T>()`** - **Well Tested**
  - Tests: `QueryScalarAsyncTests.cs`
  - Coverage: Various return types, parameters, command options, cancellation
  - Grade: A

- **`QueryPartial<T>()`** - **Well Tested** (via partial async tests)
  - Tests: `QueryPartialAsyncTests.cs` (covers both sync and async)
  - Coverage: Partial mapping scenarios
  - Grade: A-

- **`QueryPartialAsync<T>()`** - **Well Tested**
  - Tests: `QueryPartialAsyncTests.cs`
  - Coverage: Partial mapping with async
  - Grade: A

- **`QueryMultiple()`** - **Well Tested**
  - Tests: `QueryMultipleTests.cs`
  - Coverage: Multiple result sets, GridReader functionality
  - Grade: A-

### Untested Public APIs (Zero Coverage)

#### Query Methods
- **`QueryFirst<T>()`** - **Untested**
  - Expected behavior: Returns first record or throws if none
  - Missing tests for: Basic functionality, parameter binding, error cases

- **`QueryFirstAsync<T>()`** - **Untested**
  - Expected behavior: Async version of QueryFirst
  - Missing tests for: Basic functionality, cancellation, error cases

- **`QueryFirstOrDefault<T>()`** - **Untested**
  - Expected behavior: Returns first record or default if none
  - Missing tests for: Basic functionality, parameter binding, null returns

- **`QueryFirstOrDefaultAsync<T>()`** - **Untested**
  - Expected behavior: Async version of QueryFirstOrDefault
  - Missing tests for: Basic functionality, cancellation, null returns

- **`QuerySingle<T>()`** - **Untested**
  - Expected behavior: Returns single record or throws if 0 or >1 records
  - Missing tests for: Success case, zero records, multiple records error

- **`QuerySingleAsync<T>()`** - **Untested**
  - Expected behavior: Async version of QuerySingle
  - Missing tests for: Success case, zero records, multiple records error

- **`QuerySingleOrDefault<T>()`** - **Untested**
  - Expected behavior: Returns single record or default if none, throws if >1
  - Missing tests for: Success case, zero records, multiple records error

- **`QuerySingleOrDefaultAsync<T>()`** - **Untested**
  - Expected behavior: Async version of QuerySingleOrDefault
  - Missing tests for: Success case, zero records, multiple records error

#### Streaming Methods
- **`QueryStream<T>()`** - **Untested**
  - Expected behavior: Returns IEnumerable<T> for streaming large result sets
  - Missing tests for: Basic functionality, parameter binding, streaming behavior

- **`QueryStreamAsync<T>()`** - **Untested**
  - Expected behavior: Returns IAsyncEnumerable<T> for async streaming
  - Missing tests for: Basic functionality, cancellation, streaming behavior

- **`QueryPartialStream<T>()`** - **Untested**
  - Expected behavior: Streaming version of QueryPartial
  - Missing tests for: Partial mapping with streaming

- **`QueryPartialStreamAsync<T>()`** - **Untested**
  - Expected behavior: Async streaming version of QueryPartial
  - Missing tests for: Partial mapping with async streaming

#### Multiple Result Set Methods
- **`QueryMultipleAsync()`** - **Untested**
  - Expected behavior: Async version of QueryMultiple
  - Missing tests for: Multiple result sets, GridReader async methods

#### GridReader Methods
- **`GridReader.Read<T>()`** - **Partially Tested** (only through QueryMultiple)
  - Expected behavior: Reads current result set as List<T>
  - Missing tests for: Direct usage, error cases

- **`GridReader.ReadPartial<T>()`** - **Partially Tested** (only through QueryMultiple)
  - Expected behavior: Reads current result set as List<T> with partial mapping
  - Missing tests for: Direct usage, error cases

- **`GridReader.ReadFirst<T>()`** - **Untested**
  - Expected behavior: Returns first record from current result set or throws
  - Missing tests for: Basic functionality, error cases

- **`GridReader.ReadFirstAsync<T>()`** - **Untested**
  - Expected behavior: Async version of ReadFirst
  - Missing tests for: Basic functionality, cancellation

- **`GridReader.ReadFirstOrDefault<T>()`** - **Untested**
  - Expected behavior: Returns first record from current result set or default
  - Missing tests for: Basic functionality, null returns

- **`GridReader.ReadFirstOrDefaultAsync<T>()`** - **Untested**
  - Expected behavior: Async version of ReadFirstOrDefault
  - Missing tests for: Basic functionality, cancellation

- **`GridReader.ReadSingle<T>()`** - **Untested**
  - Expected behavior: Returns single record from current result set or throws
  - Missing tests for: Success case, zero records, multiple records error

- **`GridReader.ReadSingleAsync<T>()`** - **Untested**
  - Expected behavior: Async version of ReadSingle
  - Missing tests for: Success case, zero records, multiple records error

- **`GridReader.ReadSingleOrDefault<T>()`** - **Untested**
  - Expected behavior: Returns single record or default, throws if >1
  - Missing tests for: Success case, zero records, multiple records error

- **`GridReader.ReadSingleOrDefaultAsync<T>()`** - **Untested**
  - Expected behavior: Async version of ReadSingleOrDefault
  - Missing tests for: Success case, zero records, multiple records error

- **`GridReader.ReadScalar<T>()`** - **Untested**
  - Expected behavior: Reads first column of first row as T
  - Missing tests for: Basic functionality, null handling

- **`GridReader.ReadScalarAsync<T>()`** - **Untested**
  - Expected behavior: Async version of ReadScalar
  - Missing tests for: Basic functionality, cancellation

- **`GridReader.ReadStream<T>()`** - **Untested**
  - Expected behavior: Streams current result set as IEnumerable<T>
  - Missing tests for: Streaming behavior

- **`GridReader.ReadStreamAsync<T>()`** - **Untested**
  - Expected behavior: Streams current result set as IAsyncEnumerable<T>
  - Missing tests for: Streaming behavior, cancellation

- **`GridReader.ReadPartialStream<T>()`** - **Untested**
  - Expected behavior: Streaming version of ReadPartial
  - Missing tests for: Partial mapping with streaming

- **`GridReader.ReadPartialStreamAsync<T>()`** - **Untested**
  - Expected behavior: Async streaming version of ReadPartial
  - Missing tests for: Partial mapping with async streaming

#### Configuration
- **`JauntyConfig`** - **Untested**
  - Properties: `SchemaNameResolver`, `TableNameResolver`, `ColumnNameResolver`
  - Method: `Reset()`
  - Missing tests for: Configuration changes, reset functionality

### Internal/Implementation Coverage

#### Well-Covered Internal Components
- **`QueryCore<T>()`** - **Well Covered** (through public API tests)
- **`QueryCoreAsync<T>()`** - **Well Covered** (through public API tests)
- **`ExecuteReader()`** - **Well Covered** (through public API tests)
- **`ExecuteReaderAsync()`** - **Well Covered** (through public API tests)
- **`DrDispatcher.Resolve<T>()`** - **Well Covered** (through public API tests)
- **`ParameterBinder.Bind()`** - **Well Covered** (through parameter binding tests)

#### Under-Covered Internal Components
- **`MetadataCache<T>`** - **Partially Covered** (only through integration tests)
  - Missing unit tests for: Strict vs Projection mapping modes, error scenarios
  - Edge cases: Duplicate column mapping, missing properties in strict mode

- **`MappedCache<T>`** - **Potentially Under-Covered**
  - Used by DrDispatcher but may lack specific tests

- **`SqlParameterParser`** - **Potentially Under-Covered**
  - Used by ParameterBinder but may lack specific tests for edge cases

#### Core Internal Logic
- **Connection State Management** - **Well Covered**
- **Transaction Handling** - **Well Covered**
- **Timeout Handling** - **Well Covered**
- **Cancellation Token Handling** - **Well Covered**
- **NULL Value Handling** - **Well Covered**
- **Type Conversion** - **Well Covered**

## Coverage Gaps Summary

### Critical Missing Tests (High Priority)
1. Single result methods (`QuerySingle`, `QuerySingleOrDefault` and async versions)
2. First result methods (`QueryFirst`, `QueryFirstOrDefault` and async versions)
3. Streaming methods (`QueryStream`, `QueryStreamAsync`, etc.)
4. GridReader specific methods (all Read* methods)
5. QueryMultipleAsync

### Important Missing Tests (Medium Priority)
1. JauntyConfig configuration and reset functionality
2. Edge cases in MetadataCache
3. Advanced parameter binding scenarios
4. Complex mapping scenarios

### Nice-to-Have Tests (Lower Priority)
1. Exception scenarios in internal components
2. Performance edge cases
3. Advanced cancellation scenarios

## Recommendations for 100% Coverage

### Phase 1: Critical Coverage (Immediate Priority)
1. **Create tests for single result methods**:
   - `QueryFirst<T>`, `QueryFirstOrDefault<T>`, `QuerySingle<T>`, `QuerySingleOrDefault<T>`
   - Include async versions
   - Test success, failure, and edge cases

2. **Create tests for streaming methods**:
   - `QueryStream<T>`, `QueryStreamAsync<T>`
   - Test memory efficiency and streaming behavior
   - Include partial streaming methods

3. **Create tests for GridReader methods**:
   - All Read* methods in GridReader
   - Both sync and async versions
   - Test with QueryMultiple results

### Phase 2: Configuration and Edge Cases
1. **Test JauntyConfig functionality**:
   - Configuration changes
   - Reset functionality
   - Custom naming conventions

2. **Enhance MetadataCache tests**:
   - Strict vs Projection mode differences
   - Error scenarios
   - Performance with large entities

### Phase 3: Complete Coverage
1. **Unit tests for internal components**:
   - ParameterBinder edge cases
   - SqlParameterParser scenarios
   - MappedCache functionality

## Test Implementation Strategy

### For Missing Single Result Methods:
```csharp
// Example for QueryFirst tests
[Fact]
public void QueryFirst_WithResults_ReturnsFirst()
{
    var result = connection.QueryFirst<Product>("SELECT * FROM products LIMIT 1");
    Assert.NotNull(result);
}

[Fact]
public void QueryFirst_NoResults_Throws()
{
    var ex = Assert.Throws<InvalidOperationException>(() =>
        connection.QueryFirst<Product>("SELECT * FROM products WHERE product_id = -1"));
}
```

### For Streaming Methods:
```csharp
// Example for QueryStream tests
[Fact]
public void QueryStream_LargeResultSet_ProcessesIncrementally()
{
    var results = connection.QueryStream<Product>("SELECT * FROM products");
    int count = 0;
    foreach(var product in results)
    {
        count++;
        if(count > 5) break; // Test that it streams incrementally
    }
    Assert.Equal(5, count);
}
```

## Estimated Coverage Metrics
- **Currently Tested Public APIs**: ~40% (9 out of 22 major method categories)
- **Currently Tested Internal Components**: ~70% (covered through integration tests)
- **Overall Code Coverage Estimate**: ~50-60%

## Next Steps
1. Implement tests for critical missing APIs (Phase 1)
2. Add configuration tests (Phase 2)
3. Enhance internal component unit tests (Phase 3)
4. Run code coverage tool to measure exact percentages
5. Iterate until 100% coverage achieved

This comprehensive approach will ensure Jaunty has robust test coverage across all functionality levels.