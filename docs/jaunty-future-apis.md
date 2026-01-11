# Jaunty Future API Todo List

This document outlines APIs that are planned for future implementation in Jaunty.

## High Priority

### Bulk Operations
- `BulkInsert<T>(IEnumerable<T> entities, string tableName, CommandOptions options = default)` where T : new()
- `Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, string tableName, CommandOptions options = default, CancellationToken cancellationToken = default)` where T : new()
- `BulkUpdate<T>(IEnumerable<T> entities, string tableName, string keyProperty = "Id", CommandOptions options = default)` where T : new()
- `Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, string tableName, string keyProperty = "Id", CommandOptions options = default, CancellationToken cancellationToken = default)` where T : new()
- `BulkDelete<T>(IEnumerable<object> ids, string tableName, string keyProperty = "Id", CommandOptions options = default)` where T : new()
- `Task<int> BulkDeleteAsync<T>(IEnumerable<object> ids, string tableName, string keyProperty = "Id", CommandOptions options = default, CancellationToken cancellationToken = default)` where T : new()

### Advanced Parameter Binding
- `TableValuedParameterHelper` - Complete implementation for SQL Server TVPs
- `CollectionParameterHelper.ExpandInClause` - Better collection expansion for IN clauses
- `DynamicParameters` - Full dynamic parameter support

### LINQ Support
- `IQueryable<T> From<T>()` - Basic LINQ query support
- Expression tree compilation for LINQ queries

## Medium Priority

### Change Tracking
- `DbContext`-like functionality with change tracking
- Entity state management (Added, Modified, Deleted, Unchanged)
- `SaveChanges()` and `SaveChangesAsync()` methods

### Relationship Handling
- Navigation property support
- `Include<T, TProperty>()` for eager loading
- Lazy loading capabilities
- One-to-many, many-to-one, many-to-many relationship support

### Caching
- Query result caching
- Metadata caching improvements
- Connection pooling enhancements

### Advanced Configuration
- Connection resilience with retry policies
- Command execution interceptors
- Query execution logging

## Low Priority

### Migration Support
- Code-first schema generation
- Migration system integration
- Database schema comparison tools

### Advanced Mapping Features
- Inheritance mapping strategies
- Complex type mapping
- Value converter support
- Custom type handlers

### Performance Monitoring
- Query execution profiling
- Performance counters
- Diagnostic logging
- Query plan analysis

### Additional Database Providers
- Specific optimizations for different database systems
- Provider-specific functionality exposure
- Cross-platform database feature support

## Future Considerations

### Async Streaming Enhancements
- `IAsyncEnumerable<T> QueryAsyncStream<T>()` with better cancellation support
- Memory-efficient large dataset processing

### Transaction Management
- Unit of Work pattern implementation
- Nested transaction support
- Transaction scope integration

### Validation Integration
- Integration with validation frameworks
- Automatic validation during CRUD operations
- Custom validation rules support

### Audit Trail
- Automatic change tracking and logging
- Who/when/what information capture
- Audit history querying capabilities

### Soft Delete
- Built-in soft delete support
- Automatic filtering of soft-deleted records
- Undelete functionality

### Concurrency Control
- Optimistic concurrency support
- Row version/timestamp handling
- Conflict detection and resolution

### Multi-Tenant Support
- Schema-based multi-tenancy
- Row-level security
- Tenant isolation mechanisms

This todo list represents features that would enhance Jaunty's functionality and make it more competitive with other ORMs while maintaining its core philosophy of respecting SQL and providing high performance.