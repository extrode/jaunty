# Jaunty API Documentation - January 15, 2026

## Summary of Jaunty's Current State

### Core Architecture
Jaunty is a high-performance micro-ORM for .NET that uses C# 13 extension syntax. It provides both synchronous and asynchronous APIs with strict mapping by default.

### Main Features
1. **Query Operations**: Comprehensive query methods (Query, QueryFirst, QuerySingle, QueryScalar, etc.) with both strict and partial mapping modes
2. **CRUD Operations**: Insert, Update, Delete operations with both sync and async variants
3. **Multiple Result Sets**: QueryMultiple functionality with GridReader for handling multiple result sets
4. **Streaming**: QueryStream methods for memory-efficient processing of large result sets
5. **Stored Procedures**: Full support for stored procedures with input, output, and return parameters
6. **Fluent API**: Type-safe query builder with LINQ-style syntax (Jaunty.Fluent package)

### Public API Documentation

#### 1. Query Methods
- `Query<T>()`, `QueryAsync<T>()` - Basic query operations
- `QueryFirst<T>()`, `QueryFirstOrDefault<T>()`, `QuerySingle<T>()`, `QuerySingleOrDefault<T>()` - Single result operations
- `QueryPartial<T>()`, `QueryPartialAsync<T>()` - Partial mapping operations
- `QueryScalar<T>()`, `QueryScalarAsync<T>()` - Scalar value operations

#### 2. Streaming Methods
- `QueryStream<T>()`, `QueryStreamAsync<T>()` - Streaming operations
- `QueryPartialStream<T>()`, `QueryPartialStreamAsync<T>()` - Partial streaming operations

#### 3. Multiple Result Set Methods
- `QueryMultiple()`, `QueryMultipleAsync()` - Multiple result set operations
- `GridReader` - For reading from multiple result sets

#### 4. Stored Procedure Methods
- `ExecuteStoredProcedure<T>()`, `ExecuteStoredProcedureAsync<T>()` - Execute stored procedures
- Support for input, output, and return parameters via `SpParameters` class

#### 5. CRUD Methods
- `Insert<T>()`, `InsertAsync<T>()` - Insert operations
- `Update<T>()`, `UpdateAsync<T>()` - Update operations  
- `Delete<T>()`, `DeleteAsync<T>()` - Delete operations

#### 6. Fluent API (Jaunty.Fluent)
- `From<T>()` - Entry point for fluent queries
- Method chaining for WHERE, JOIN, ORDER BY, GROUP BY, etc.
- Expression-based and string-based column selection
- Full async support for all operations

### Key Differentiators
1. **Strict by Default**: All properties must have matching columns (fail-fast approach)
2. **Partial Mapping**: Explicit `QueryPartial` methods for mapping only existing columns
3. **Modern C# Features**: Uses C# 13 extension syntax, records, primary constructors
4. **Performance Focused**: Compiled expression trees, metadata caching, minimal allocations
5. **Type Safety**: Strongly typed fluent API with compile-time validation

### Current Limitations
1. **SQLite Multiple Result Sets**: Limited support due to SQLite's inherent limitations
2. **No Dynamic Objects**: No support for `dynamic` or `Dictionary<string, object>` results
3. **No Collection Parameter Expansion**: Doesn't automatically expand collections in IN clauses
4. **No Multi-Mapping**: No built-in support for mapping multiple result sets to complex objects (like Dapper's multi-mapping)

### Implementation Quality
- Clean, well-structured codebase
- Good separation of concerns
- Proper async/await patterns
- Comprehensive error handling
- Good use of modern C# features
- Extensive test coverage (though some tests fail due to SQLite limitations)

The library is well-designed and implements a solid micro-ORM with a focus on performance and type safety. The main outstanding issues relate to SQLite's limitations with multiple result sets and a few missing advanced features compared to more mature ORMs like Dapper.