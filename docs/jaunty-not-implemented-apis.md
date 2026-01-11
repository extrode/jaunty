# APIs Jaunty Will NOT Implement

This document outlines APIs and features that Jaunty has deliberately chosen not to implement, maintaining its focus on being a micro-ORM that respects your SQL.

## Core Design Philosophy

Jaunty is designed as a micro-ORM that executes your SQL and maps results to objects with strict, predictable behavior. It does NOT aim to be a full-featured ORM with extensive abstractions.

## APIs and Features NOT Implemented

### 1. Query Builder DSL
- **Will NOT implement**: Fluent query builders like `.Where().Select().OrderBy()`
- **Reason**: Goes against the philosophy of respecting your SQL
- **Alternative**: Write your own SQL queries

### 2. LINQ-to-SQL Translation (Limited)
- **Will NOT implement**: Full Expression tree compilation to SQL
- **Current Status**: Basic LINQ support may be added, but complex queries will remain as raw SQL
- **Reason**: Maintains predictability and allows complex SQL that LINQ can't express

### 3. Automatic Change Tracking (Outside DbContext Scope)
- **Will NOT implement**: Extensive automatic change tracking without explicit context
- **Current Status**: Manual CRUD operations only
- **Reason**: Keeps the library lightweight and predictable

### 4. Lazy Loading by Default
- **Will NOT implement**: Automatic lazy loading of navigation properties
- **Reason**: Leads to N+1 query problems and unpredictable behavior
- **Alternative**: Explicit loading with `Include` or separate queries

### 5. Proxy Generation
- **Will NOT implement**: Dynamic proxy generation for change tracking
- **Reason**: Adds complexity, performance overhead, and runtime dependencies
- **Alternative**: Use POCOs with explicit change tracking if needed

### 6. Complex Inheritance Mapping
- **Will NOT implement**: Table-per-hierarchy, table-per-type inheritance strategies
- **Reason**: Adds complexity and often leads to inefficient queries
- **Alternative**: Use composition or explicit mapping for inheritance scenarios

### 7. Automatic Schema Creation/Management
- **Will NOT implement**: Code-first schema generation
- **Reason**: Jaunty is not a schema management tool
- **Alternative**: Use dedicated migration tools or create schema separately

### 8. Extensive Caching Layer
- **Will NOT implement**: Complex second-level caching systems
- **Reason**: Caching needs vary greatly between applications
- **Alternative**: Implement application-level caching as needed

### 9. Connection Resilience with Automatic Retries
- **Will NOT implement**: Built-in retry policies for transient failures
- **Reason**: Retry logic is application-specific and complex
- **Alternative**: Implement retry policies at the application level

### 10. Extensive Configuration Options
- **Will NOT implement**: Hundreds of configuration options
- **Reason**: Keeps API simple and focused
- **Current Approach**: Minimal configuration with sensible defaults

### 11. Stored Procedure Generation
- **Will NOT implement**: Automatic stored procedure generation from C# methods
- **Reason**: Goes against the "respect your SQL" philosophy
- **Alternative**: Write and call stored procedures directly with Query methods

### 12. Complex Transaction Management
- **Will NOT implement**: Distributed transaction management
- **Reason**: Beyond the scope of a micro-ORM
- **Alternative**: Use ADO.NET transaction management directly

### 13. Extensive Validation Framework
- **Will NOT implement**: Built-in validation rules and validation engine
- **Reason**: Validation is business logic, not data access concern
- **Alternative**: Use application-level validation frameworks

### 14. Automatic Migration System
- **Will NOT implement**: Built-in migration system like EF Core Migrations
- **Reason**: Schema management is separate from data access
- **Alternative**: Use dedicated migration tools (Flyway, DbUp, etc.)

### 15. Extensive Logging Framework
- **Will NOT implement**: Built-in comprehensive logging system
- **Reason**: Logging is application concern, not ORM concern
- **Alternative**: Use application-level logging with interceptors if needed

### 16. Complex Relationship Management
- **Will NOT implement**: Complex relationship management with automatic loading
- **Current Status**: Basic relationship support through manual queries
- **Reason**: Keeps the library focused on core data access

### 17. Entity Framework-like Context
- **Will NOT implement**: Full DbContext with change tracking, identity resolution
- **Current Status**: Direct connection extension methods only
- **Reason**: Maintains simplicity and performance

### 18. Automatic SQL Generation for CRUD
- **Will NOT implement**: Automatic SELECT/INSERT/UPDATE/DELETE SQL generation
- **Current Status**: Manual SQL required for all operations
- **Reason**: Maintains control and predictability

### 19. Extensive Type Handlers
- **Will NOT implement**: Hundreds of custom type handlers for every possible type
- **Current Status**: Standard ADO.NET type mapping with some extensions
- **Reason**: Focuses on core functionality

### 20. Complex Configuration Inheritance
- **Will NOT implement**: Complex configuration inheritance systems
- **Current Status**: Simple global configuration options
- **Reason**: Keeps configuration straightforward

## Summary

Jaunty's decision to not implement these features is intentional and aligns with its core mission:
- Execute your SQL exactly as written
- Map results to objects with predictable behavior
- Maintain high performance
- Keep the API surface minimal and focused
- Respect the developer's control over SQL

This approach ensures that Jaunty remains a tool that enhances ADO.NET without replacing it or adding unnecessary complexity.