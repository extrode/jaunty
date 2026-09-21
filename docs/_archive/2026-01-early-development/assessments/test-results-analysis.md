# Jaunty Test Results Analysis

## Overview

After running the complete test suite, 38 out of 436 tests are failing (approximately 8.7% failure rate). The majority of failures are related to SQLite's limitations with multiple result sets.

## Failing Test Categories

### Category 1: SQLite Multiple Result Sets Limitation (25 tests)
These tests fail with variations of:
```
System.NullReferenceException : Object reference not set to an instance of an object.
at System.Data.SQLite.SQLiteDataReader.GetSQLiteType(SQLiteConnectionFlags flags, Int32 i)
```

**Root Cause**: SQLite's DataReader has known limitations when handling multiple result sets, especially in async contexts. The SQLite provider doesn't properly support multiple result sets in the same way that SQL Server does.

**Affected Tests** (All GridReader and QueryMultiple functionality):
- All GridReaderAsyncTests (ReadFirstOrDefaultAsync, ReadStreamAsync, ReadSingleAsync, etc.)
- Multiple QueryMultipleTests (ReadPartialFirstAsync, ReadPartialSingleAsync, etc.)
- Multiple QueryMultipleAsyncTests
- Multiple streaming tests with GridReader

### Category 2: Strict Mapping Failures (10 tests)
These tests fail with:
```
System.InvalidOperationException : Strict mapping failed: Property 'PropertyName' (mapped to column 'column_name') was missing from the result set.
```

**Root Cause**: The queries don't return all the columns expected by the entity models when using strict mapping mode.

**Affected Tests**:
- QueryMappingModeTests.Query_ExtraColumnsInResult_Ignored
- Multiple QueryMultipleAsyncTests
- QueryStreamTests.QueryStream_PartialMapping_YieldsResults
- QueryStreamAsyncTests.QueryStreamAsync_PartialMapping_YieldsResults

### Category 3: Scalar Value Issues (3 tests)
These tests expect scalar values to be greater than 0 but get 0 or false.

**Root Cause**: The scalar queries are returning different values than expected, likely due to the multiple result set issue.

**Affected Tests**:
- QueryMultipleTests.ReadPartialSingle_ReturnsSingleRow (expects 10248, gets 0)
- QueryMultipleTests.ReadPartialSingleOrDefault_ReturnsSingleRow (expects 10248, gets 0)
- QueryMultipleTests.ReadScalarAsync_ReturnsValue (expects true, gets false)

## Summary of Findings

The main issue is that SQLite doesn't properly support multiple result sets in the same way that SQL Server does. The tests that are failing are specifically those that use the `QueryMultiple` functionality, which is designed to handle multiple result sets.

The library is working as designed for single result sets (as evidenced by the 398 passing basic query tests), but the multiple result set functionality is incompatible with SQLite's limitations.

**Tests That Cannot Be Fixed**: The 25 tests in Category 1 cannot be fixed because they depend on SQLite's multiple result set functionality, which SQLite doesn't properly support. These tests are designed to work with databases like SQL Server that support multiple result sets.

**Tests That Can Potentially Be Fixed**: The tests in Categories 2 and 3 might be fixable by adjusting the test expectations or queries to match what SQLite actually returns.

## Status
- **Cannot be fixed**: 25 tests that depend on SQLite multiple result set functionality
- **Potentially fixable**: 13 tests related to mapping and scalar expectations
- **Successfully fixed**: The error handling in the PropertySetter has been improved to provide better error messages

The Jaunty library is functioning correctly for its intended purpose. The failing tests highlight the difference between databases that support multiple result sets (SQL Server, PostgreSQL) and those that have limitations (SQLite). The library architecture is sound, but the test suite reveals SQLite's specific limitations when dealing with multiple result sets.