# Ranking of Missing APIs in Jaunty

This document ranks the APIs that are missing in Jaunty compared to competitors like Dapper and Entity Framework, from must-have to nice-to-have.

## Must-Have (Critical for ORM Competitiveness)

### 1. Complete Write Operations
**Priority**: Critical (Partially Addressed)
**Description**: Complete CRUD operations with proper parameter binding
**Current Status**: Basic Insert/Update/Delete implemented with both sync and async versions
**Implemented**:
- `Insert<T>()` and `InsertAsync<T>()` - Insert single entity
- `Update<T>()` and `UpdateAsync<T>()` - Update single entity
- `Delete<T>()` and `DeleteAsync<T>()` - Delete by entity or ID
- All methods support CommandOptions for transactions and timeouts

**Still Missing**:
- Bulk operations (BulkInsert, BulkUpdate, BulkDelete)
- Upsert operations
- Range operations (InsertRange, UpdateRange, DeleteRange)
- Proper parameter validation for write operations

**Competitor Comparison**:
- **Dapper**: Basic Execute methods, no built-in CRUD
- **Dapper Extensions**: Basic CRUD operations
- **Entity Framework**: Complete CRUD with change tracking

### 2. Collection Parameter Expansion
**Priority**: Critical  
**Description**: Automatic expansion of collections in IN clauses (`WHERE id IN @ids`)
**Current Status**: Not implemented
**Missing**: `WHERE id IN @ids` with collection parameter expansion

**Competitor Comparison**:
- **Dapper**: Automatically expands collections in IN clauses
- **Entity Framework**: Handles collections in LINQ expressions

### 3. Dynamic Object Support
**Priority**: Critical
**Description**: Support for `dynamic` objects in queries
**Current Status**: Not implemented
**Missing**: `Query<dynamic>()` support

**Competitor Comparison**:
- **Dapper**: Full dynamic object support
- **Entity Framework**: Limited dynamic support

### 4. Dictionary Support
**Priority**: Critical
**Description**: Support for `Dictionary<string, object>` results
**Current Status**: Not implemented
**Missing**: `Query<Dictionary<string, object>>()` support

**Competitor Comparison**:
- **Dapper**: Supports dictionary results
- **Entity Framework**: Can project to dictionaries

## Should-Have (Important for Developer Experience)

### 5. Table-Valued Parameter (TVP) Support
**Priority**: High
**Description**: Support for SQL Server Table-Valued Parameters for bulk operations
**Current Status**: Not implemented
**Missing**: Native TVP support for efficient bulk operations

**Competitor Comparison**:
- **Dapper**: Can work with TVPs through manual parameter creation
- **Entity Framework**: No direct TVP support

### 6. Result Set Merging (Multi-Mapping)
**Priority**: High
**Description**: Ability to map multiple result sets into complex objects
**Current Status**: Limited support through GridReader
**Missing**: Direct multi-mapping like `Query<T, U, TResult>()`

**Competitor Comparison**:
- **Dapper**: Excellent multi-mapping support with splitOn parameter
- **Entity Framework**: Include() for related data

### 7. Async Streaming (IAsyncEnumerable)
**Priority**: High
**Description**: Streaming large result sets asynchronously
**Current Status**: Basic streaming implemented
**Missing**: `IAsyncEnumerable<T>` support for .NET 8+

**Competitor Comparison**:
- **Dapper**: Supports IAsyncEnumerable in newer versions
- **Entity Framework**: Full async streaming support

### 8. Advanced Parameter Binding
**Priority**: High
**Description**: Complex parameter binding scenarios
**Current Status**: Basic parameter binding
**Missing**:
- Complex object parameter binding
- Nested property parameter binding
- Automatic parameter type inference

**Competitor Comparison**:
- **Dapper**: Good parameter binding with some limitations
- **Entity Framework**: Automatic parameter handling

## Could-Have (Nice for Advanced Scenarios)

### 9. Basic LINQ Support
**Priority**: Medium
**Description**: Limited LINQ-to-SQL translation for basic operations
**Current Status**: No LINQ support
**Missing**: Basic LINQ operations like Where, Select, OrderBy

**Competitor Comparison**:
- **Dapper**: No native LINQ support
- **Entity Framework**: Full LINQ support

### 10. Connection Resilience
**Priority**: Medium
**Description**: Automatic retry policies for transient failures
**Current Status**: Not implemented
**Missing**: Retry policies and connection resilience

**Competitor Comparison**:
- **Dapper**: Relies on underlying ADO.NET
- **Entity Framework**: Built-in connection resilience options

### 11. Transaction Scope Support
**Priority**: Medium
**Description**: Full TransactionScope integration
**Current Status**: Basic transaction support
**Missing**: Full TransactionScope integration

**Competitor Comparison**:
- **Dapper**: Basic transaction support
- **Entity Framework**: Full TransactionScope integration

### 12. Caching
**Priority**: Medium
**Description**: Query result caching
**Current Status**: No caching
**Missing**: Query result caching mechanisms

**Competitor Comparison**:
- **Dapper**: No built-in caching
- **Entity Framework**: Second-level caching options

## Nice-to-Have (Convenience Features)

### 13. Migration Support
**Priority**: Low
**Description**: Code-first migration system
**Current Status**: Not implemented
**Missing**: Migration system

**Competitor Comparison**:
- **Dapper**: No migration support (by design)
- **Entity Framework**: Full migration system

### 14. Change Tracking
**Priority**: Low
**Description**: Automatic change tracking and SaveChanges functionality
**Current Status**: Not implemented
**Missing**: Change tracking context

**Competitor Comparison**:
- **Dapper**: No change tracking (by design)
- **Entity Framework**: Full change tracking

### 15. Validation Integration
**Priority**: Low
**Description**: Built-in validation during CRUD operations
**Current Status**: Not implemented
**Missing**: Validation framework integration

**Competitor Comparison**:
- **Dapper**: No validation (application level)
- **Entity Framework**: Can integrate with validation frameworks

### 16. Soft Delete
**Priority**: Low
**Description**: Automatic soft delete handling
**Current Status**: Not implemented
**Missing**: Automatic soft delete support

**Competitor Comparison**:
- **Dapper**: Manual implementation required
- **Entity Framework**: Can be implemented with query filters

### 17. Auditing Support
**Priority**: Low
**Description**: Automatic audit trail for entity changes
**Current Status**: Not implemented
**Missing**: Audit trail functionality

**Competitor Comparison**:
- **Dapper**: Manual implementation required
- **Entity Framework**: Can be implemented with interceptors

### 18. Concurrency Control
**Priority**: Low
**Description**: Optimistic concurrency with row version/timestamp
**Current Status**: Not implemented
**Missing**: Concurrency control mechanisms

**Competitor Comparison**:
- **Dapper**: Manual concurrency control
- **Entity Framework**: Built-in optimistic concurrency support

## Summary of Competitive Position

### Jaunty's Advantages:
1. **Strict Mapping by Default**: Prevents silent mapping bugs
2. **Performance**: Compiled expression trees and metadata caching
3. **SQL Control**: Respects your SQL completely
4. **Clean API**: Intuitive method names and consistent patterns
5. **Async Support**: Comprehensive async/await support

### Jaunty's Disadvantages vs Competitors:
1. **Limited Write Operations**: Basic CRUD only, no bulk operations
2. **No Dynamic Support**: No `dynamic` object queries
3. **No Collection Expansion**: No automatic IN clause expansion
4. **No Multi-Mapping**: Limited complex object mapping
5. **No LINQ**: No query composition capabilities

### Priority Implementation Order:
1. **Collection Parameter Expansion** - Critical for usability
2. **Dynamic Object Support** - Important for flexibility
3. **Dictionary Support** - Useful for ad-hoc queries
4. **TVP Support** - Important for SQL Server users
5. **Multi-Mapping** - Valuable for complex queries
6. **Async Streaming** - Important for large datasets

The ranking reflects the importance of each feature for making Jaunty a competitive ORM while maintaining its core philosophy of respecting SQL and providing high performance.