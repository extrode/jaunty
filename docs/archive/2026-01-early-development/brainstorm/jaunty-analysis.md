# Jaunty Project Analysis

## Overview
This document provides a comprehensive analysis of the Jaunty project, identifying inconsistencies and comparing its public APIs with Dapper to determine what features are missing for it to be considered a respected ORM.

## Public API Surface

### Query Methods
- `Query<T>()` - Executes SQL and maps results to strongly-typed list with strict mapping
- `QueryPartial<T>()` - Executes SQL and maps only matching columns to entities
- `QueryFirst<T>()` - Returns first record or throws
- `QueryFirstOrDefault<T>()` - Returns first record or default
- `QuerySingle<T>()` - Returns single record or throws
- `QuerySingleOrDefault<T>()` - Returns single record or default
- `QueryScalar<T>()` - Returns scalar value from first column of first row
- `QueryMultiple()` - Returns multiple result sets via GridReader

### Async Versions
All query methods have async equivalents:
- `QueryAsync<T>()`, `QueryPartialAsync<T>()`, etc.

### Streaming Methods
- `QueryStream<T>()`, `QueryStreamAsync<T>()` - For large result sets
- `QueryPartialStream<T>()`, `QueryPartialStreamAsync<T>()`

### Parameter Support
- Named parameters: `new { Param1 = value1, Param2 = value2 }`
- Positional parameters: `connection.Query<T>(sql, param1, param2)`
- Array parameters: `connection.Query<T>(sql, new object[] { param1, param2 })`

### Configuration Options
- `CommandOptions<T>` for mapper, transaction, and timeout configuration
- `JauntyConfig` for global configuration of naming conventions
- Attribute-based mapping (`[Table]`, `[Column]`, `[Ignore]`)

## Inconsistencies Found

### 1. Language Version Compatibility Issues
- **Issue**: Use of C# 8.0+ features with `netstandard2.0` target
- **Details**: Ternary operators with throw expressions (`condition ? throw expr : value`) are not supported in C# 7.3 (netstandard2.0)
- **Files Affected**: QueryCore.cs, QueryCoreAsync.cs, GridReader.cs, Attribute files
- **Impact**: Build failures when targeting netstandard2.0

### 2. Strict Mapping Philosophy
- **Issue**: Jaunty enforces strict mapping by default (all properties must have matching columns)
- **Comparison**: Dapper allows partial mapping by default
- **Impact**: Different developer experience, may be too restrictive for some use cases

### 3. Limited Dynamic Object Support
- **Issue**: No direct support for `dynamic` objects or `ExpandoObject`
- **Comparison**: Dapper supports querying into dynamic objects
- **Impact**: Less flexibility for ad-hoc queries

### 4. Missing Advanced Parameter Types
- **Issue**: No support for TVPs (Table Valued Parameters)
- **Comparison**: Dapper supports TVPs through custom parameter implementations
- **Impact**: Cannot efficiently pass large datasets to stored procedures

### 5. No Built-in Bulk Operations
- **Issue**: No built-in support for bulk insert/update/delete operations
- **Comparison**: Dapper has ecosystem extensions for bulk operations
- **Impact**: Performance limitations for bulk data operations

### 6. Missing Dictionary Support
- **Issue**: No direct support for querying into dictionaries
- **Comparison**: Dapper supports `Dictionary<string, object>` mapping
- **Impact**: Less flexibility for dynamic result handling

### 7. Limited Stored Procedure Support
- **Issue**: No specific conveniences for stored procedure execution beyond standard queries
- **Comparison**: Dapper provides better stored procedure execution patterns
- **Impact**: More boilerplate code for stored procedure calls

## Missing Features Compared to Dapper

### 1. Dynamic Object Support
- **Missing**: `Query<dynamic>()` support
- **Benefit**: Allows flexible result handling without predefined types
- **Use Case**: Ad-hoc reporting, dynamic queries

### 2. Dictionary Mapping
- **Missing**: Direct dictionary result mapping
- **Benefit**: Flexible key-value result handling
- **Use Case**: Configuration data, lookup tables

### 3. TVP Support
- **Missing**: Table Valued Parameter support
- **Benefit**: Efficient bulk operations with SQL Server
- **Use Case**: Bulk inserts, updates with large datasets

### 4. Buffered/Unbuffered Control
- **Partially Available**: Streaming methods provide some control
- **Enhancement**: More granular control over buffering behavior
- **Benefit**: Memory optimization for large result sets

### 5. IN Clause Helper
- **Missing**: Built-in support for `WHERE col IN (@values)` with collections
- **Benefit**: Safe parameterized IN clauses
- **Use Case**: Filtering by collections of IDs

### 6. Multi-Mapping Support
- **Missing**: Built-in support for joining multiple result sets into complex objects
- **Benefit**: Easy parent-child relationship mapping
- **Use Case**: Master-detail queries

### 7. Simple CRUD Operations
- **Missing**: Built-in Insert/Update/Delete methods
- **Benefit**: Reduced boilerplate for basic operations
- **Note**: Jaunty focuses on query operations, not full ORM features

### 8. Result Reader Optimization
- **Missing**: Direct `IDataReader` to object mapping utilities
- **Benefit**: Maximum performance for custom scenarios
- **Use Case**: High-performance scenarios

## Strengths of Jaunty

### 1. Strict Mapping by Default
- Prevents silent bugs from unmapped properties
- Clear error messages for mapping mismatches
- Predictable behavior

### 2. Performance Focus
- Compiled expression trees for property setters
- Metadata caching for optimal performance
- Zero-allocation per query for metadata

### 3. Clean API Design
- C# 13 extension syntax
- Consistent parameter patterns
- Unified CommandOptions for configuration

### 4. Connection State Management
- Respects existing connection state
- Opens/closes connections appropriately
- No connection leaks

### 5. Comprehensive Async Support
- All methods have async counterparts
- Proper cancellation token support
- ConfigureAwait usage for library code

## Recommendations for Improvement

### 1. Fix Language Compatibility
- Replace ternary throw expressions with if-then-throw statements
- Ensure netstandard2.0 compatibility

### 2. Add Dynamic Support (Optional)
- Consider adding `Query<dynamic>()` support
- Maintain strict mapping as default but allow opt-out

### 3. Enhance Parameter Support
- Add TVP parameter support
- Improve collection parameter handling

### 4. Expand Configuration Options
- Add more flexible mapping modes
- Support for different naming conventions

### 5. Documentation Enhancement
- Complete XML documentation for all public APIs
- Usage examples and best practices

## Conclusion

Jaunty is a well-designed micro-ORM with a focus on performance and strict correctness. Its strict mapping approach prevents common bugs but may be too restrictive for some use cases. The main gaps compared to Dapper are in advanced parameter types, dynamic object support, and convenience features for common scenarios. The build-breaking syntax issues need to be addressed for proper netstandard2.0 compatibility.

While Jaunty positions itself as a "micro-ORM that respects your SQL," it achieves this goal well but lacks some of the convenience features that make Dapper popular. The project would benefit from fixing the language compatibility issues and potentially adding optional flexibility for developers who need it.