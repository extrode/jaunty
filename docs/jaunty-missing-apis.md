# Missing APIs Analysis: Jaunty vs Competitors

This document analyzes APIs that are missing in Jaunty compared to competitors like Dapper, Entity Framework, and other ORMs, ranked from must-have to nice-to-have.

## Must-Have Missing APIs

### 1. Complete Write Operations
**Status**: Partially implemented (basic CRUD added)
**Missing**: 
- Bulk operations (BulkInsert, BulkUpdate, BulkDelete)
- Upsert operations (InsertOrUpdate, Merge)
- Batch operations for multiple commands

**Competitor Comparison**:
- **Dapper**: Basic CRUD through Execute methods, no built-in bulk operations
- **Dapper.Extensions**: Provides basic CRUD operations
- **Entity Framework**: Full CRUD with change tracking

### 2. Table-Valued Parameter (TVP) Support
**Status**: Not implemented
**Missing**: Native support for SQL Server TVPs for bulk operations

**Competitor Comparison**:
- **Dapper**: Supports TVPs through custom parameter creation
- **Entity Framework**: No direct TVP support, requires raw SQL
- **Native ADO.NET**: Full TVP support

### 3. Dynamic Parameter Expansion
**Status**: Not implemented
**Missing**: Automatic expansion of collections in IN clauses (`WHERE id IN @ids`)

**Competitor Comparison**:
- **Dapper**: Supports collection expansion automatically
- **Entity Framework**: Supports collection expansion through LINQ

### 4. Result Set Merging (Multi-Mapping)
**Status**: Not implemented
**Missing**: Ability to map multiple result sets into a single complex object

**Competitor Comparison**:
- **Dapper**: Supports multi-mapping with splitOn parameter
- **Entity Framework**: Supports Include() for related data

## Should-Have Missing APIs

### 5. Dictionary Support
**Status**: Not implemented
**Missing**: Query methods that return `Dictionary<string, object>` or similar

**Competitor Comparison**:
- **Dapper**: Supports `Query<Dictionary<string, object>>`
- **Entity Framework**: Can be achieved through projections

### 6. Dynamic Object Support
**Status**: Not implemented
**Missing**: Query methods that return `dynamic` objects

**Competitor Comparison**:
- **Dapper**: Supports `Query<dynamic>`
- **Entity Framework**: Can be achieved through projections

### 7. Advanced Parameter Binding
**Status**: Basic implemented
**Missing**: 
- Complex object parameter binding
- Nested property parameter binding
- Automatic parameter type inference

**Competitor Comparison**:
- **Dapper**: Good parameter binding with some limitations
- **Entity Framework**: Automatic parameter handling

### 8. Connection Resilience
**Status**: Not implemented
**Missing**: Automatic retry policies for transient failures

**Competitor Comparison**:
- **Dapper**: Relies on underlying ADO.NET connection resilience
- **Entity Framework**: Built-in connection resilience options

## Could-Have Missing APIs

### 9. LINQ Support
**Status**: Not implemented
**Missing**: LINQ-to-SQL translation capabilities

**Competitor Comparison**:
- **Dapper**: No native LINQ support (requires manual SQL)
- **Entity Framework**: Full LINQ support
- **Dapper Contrib**: Limited LINQ support

### 10. Change Tracking
**Status**: Not implemented
**Missing**: Automatic change tracking and SaveChanges functionality

**Competitor Comparison**:
- **Dapper**: No change tracking (by design)
- **Entity Framework**: Full change tracking with SaveChanges

### 11. Caching
**Status**: Not implemented
**Missing**: Query result caching

**Competitor Comparison**:
- **Dapper**: No built-in caching (application level)
- **Entity Framework**: Second-level caching options

### 12. Transaction Scope Support
**Status**: Basic implemented
**Missing**: Full TransactionScope integration

**Competitor Comparison**:
- **Dapper**: Basic transaction support
- **Entity Framework**: Full TransactionScope integration

## Nice-to-Have Missing APIs

### 13. Migration Support
**Status**: Not implemented
**Missing**: Code-first migration system

**Competitor Comparison**:
- **Dapper**: No migration support (by design)
- **Entity Framework**: Full migration system

### 14. Validation Integration
**Status**: Not implemented
**Missing**: Built-in validation during CRUD operations

**Competitor Comparison**:
- **Dapper**: No validation (application level)
- **Entity Framework**: Can integrate with validation frameworks

### 15. Auditing Support
**Status**: Not implemented
**Missing**: Automatic audit trail for entity changes

**Competitor Comparison**:
- **Dapper**: No auditing (application level)
- **Entity Framework**: Can be implemented with interceptors

### 16. Soft Delete
**Status**: Not implemented
**Missing**: Automatic soft delete handling

**Competitor Comparison**:
- **Dapper**: No soft delete (manual implementation)
- **Entity Framework**: Can be implemented with query filters

### 17. Concurrency Control
**Status**: Not implemented
**Missing**: Optimistic concurrency with row version/timestamp

**Competitor Comparison**:
- **Dapper**: Manual concurrency control
- **Entity Framework**: Built-in optimistic concurrency support

## Detailed Comparison with Dapper

| Feature | Jaunty | Dapper | Gap |
|---------|--------|--------|-----|
| Basic Query | yes | yes | None |
| Async Query | yes | yes | None |
| Parameter Binding | yes | yes | None |
| Multiple Result Sets | yes | yes | None |
| Streaming | yes | (recent) | None |
| Strict Mapping | yes | (loose by default) | **Jaunty Advantage** |
| Dynamic Objects | no | yes | Missing in Jaunty |
| Dictionary Results | no | yes | Missing in Jaunty |
| Collection Parameter Expansion | no | yes | Missing in Jaunty |
| TVP Support | no | (manual) | Missing in Jaunty |
| Multi-Mapping | no | yes | Missing in Jaunty |
| Basic CRUD | (newly added) | (manual Execute) | Recently addressed |
| Performance | (high) | (high) | Comparable |

## Detailed Comparison with Entity Framework

| Feature | Jaunty | EF Core | Gap |
|---------|--------|---------|-----|
| LINQ Support | no | yes | Missing in Jaunty |
| Change Tracking | no | yes | Missing in Jaunty |
| Migrations | no | yes | Missing in Jaunty |
| Relationships | no | yes | Missing in Jaunty |
| Lazy Loading | no | yes | Missing in Jaunty |
| Caching | no | yes | Missing in Jaunty |
| Validation | no | yes | Missing in Jaunty |
| SQL Generation | no | yes | By Design in Jaunty |
| Basic CRUD | (newly added) | yes | Recently addressed |
| Performance | (high) | (lower) | **Jaunty Advantage** |
| SQL Control | (full) | (limited) | **Jaunty Advantage** |

## Priority Recommendations

### Immediate (Must-Have)
1. Complete the basic CRUD operations with better API consistency
2. Add collection parameter expansion for IN clauses
3. Implement basic TVP support for SQL Server

### Short-term (Should-Have)
1. Add dictionary result support
2. Implement dynamic object support
3. Enhance parameter binding capabilities

### Long-term (Could-Have)
1. Consider limited LINQ support for basic operations
2. Add connection resilience features
3. Implement basic caching mechanisms

### Future Consideration (Nice-to-Have)
1. Migration system integration
2. Validation framework integration
3. Auditing capabilities

## Summary

Jaunty currently has a focused feature set that emphasizes performance and SQL control. The most critical gaps compared to competitors are in:
1. Write operations (partially addressed)
2. Parameter expansion for collections
3. Dynamic and dictionary result support
4. Multi-mapping capabilities

The trade-offs are intentional - Jaunty prioritizes performance and SQL control over convenience features that might compromise these goals.