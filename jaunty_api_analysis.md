# Updated Jaunty API Analysis

Based on my comprehensive analysis of the Jaunty codebase and documentation, here is an updated breakdown of the API status:

## Public/Available APIs

Jaunty now offers a comprehensive set of APIs across multiple assemblies:

### Core Jaunty Assembly (Jaunty)
#### Query Operations
- **Basic Queries**: `Query<T>()`, `QueryAsync<T>()` with strict mapping by default
- **Partial Queries**: `QueryPartial<T>()`, `QueryPartialAsync<T>()` for partial mapping
- **Scalar Queries**: `QueryScalar<T>()`, `QueryScalarAsync<T>()` for single values
- **Single Result Queries**: `QueryFirst<T>()`, `QueryFirstOrDefault<T>()`, `QuerySingle<T>()`, `QuerySingleOrDefault<T>()` with async variants
- **Streaming Queries**: `QueryStream<T>()`, `QueryPartialStream<T>()` with async variants using `IAsyncEnumerable<T>`

#### Write Operations
- **CRUD Operations**: `Insert<T>()`, `Update<T>()`, `Delete<T>()` with async variants
- **Bulk Operations**: `BulkInsert<T>()`, `BulkUpdate<T>()`, `BulkDelete<T>()` with async variants
- **Upsert Operations**: `Upsert<T>()` with async variants

#### Multiple Result Sets
- **GridReader**: `QueryMultiple()` and `QueryMultipleAsync()` returning `GridReader` for handling multiple result sets
- **GridReader Methods**: `Read<T>()`, `ReadPartial<T>()`, and various single/result methods with async variants

#### Multi-Entity Mapping
- **Multi-Mapping**: `Query<T1, T2>()` and variants for joining multiple entities with combiners

#### Core Components
- **CommandOptions**: Struct for specifying transaction, timeout, custom mapper, and command type
- **Configuration**: `JauntyConfig` with resolvers for schema, table, and column names
- **Attributes**: `[Table]`, `[Column]`, `[Ignore]`, `[Key]`, `[DatabaseGenerated]` for mapping customization

### Fluent API Assembly (Jaunty.Fluent)
#### Fluent Query Building
- **Entry Points**: `From<T>()`, `Into<T>()`, `Cte<T>()` extension methods for building queries fluently
- **Chained Operations**: Complete fluent interface supporting `Where()`, `OrderBy()`, `Join()`, `GroupBy()`, `Take()`, `Skip()`, etc.
- **LINQ Expression Support**: Expression-based operations for type safety (e.g., `Where(p => p.Id == 1)`)
- **Raw SQL Support**: String-based operations for complex conditions (e.g., `WhereRaw("custom_sql_condition")`)

#### Join Operations
- **Multiple Join Types**: `InnerJoin<T>()`, `LeftJoin<T>()`, `RightJoin<T>()`
- **Multi-Level Joins**: Support for 3+ table joins with `IJoinedQuery3<T1, T2, T3>`
- **Join Conditions**: Expression-based and string-based join conditions

#### Set Operations
- **Set Operations**: `Union()`, `UnionAll()`, `Except()`, `Intersect()` for combining queries
- **CTE Support**: Common Table Expression queries with `Cte<T>()` builder

#### Aggregation Functions
- **Scalar Aggregates**: `Count()`, `Sum()`, `Avg()`, `Min()`, `Max()` with expression support
- **Async Variants**: All aggregation functions have async equivalents

#### Terminal Operations
- **Full Entity Selection**: `Select()`, `SelectFirst()`, `SelectSingle()` with strict mapping
- **Partial Entity Selection**: `SelectPartial()` with column specification by string or expression
- **Async Variants**: All terminal operations have async equivalents

#### SQL Generation
- **SQL Introspection**: `ToSql()` methods for debugging and logging
- **Projection Support**: Support for window functions and complex projections

### Scaffolding Assembly (Jaunty.Scaffolding)
- **Database-First Generation**: Tools to generate C# entity classes from database schema
- **Code Generation**: Automated entity generation with appropriate attributes

## Planned/Nice-to-Have APIs

Based on the documentation analysis, here are the planned enhancements ranked by priority:

### Must-Have (Critical for ORM Competitiveness)
1. **Collection Parameter Expansion**: Support for `WHERE id IN @ids` scenarios where collections are automatically expanded
2. **Dynamic Object Support**: Query methods that return `dynamic` objects for ad-hoc queries
3. **Dictionary Support**: Query methods that return `Dictionary<string, object>` for flexible result handling

### Should-Have (Important for Developer Experience)
1. **Enhanced Multi-Mapping**: More sophisticated result set merging for complex object relationships
2. **Advanced Parameter Binding**: Support for complex object parameter binding and nested property binding
3. **Improved 3-Way Join API**: Add missing async variants and pagination support for 3-way joins (as noted in KNOWN_LIMITATIONS.md)

### Could-Have (Nice for Advanced Scenarios)
1. **Connection Resilience**: Automatic retry policies for transient failures
2. **Caching Mechanisms**: Query result caching options

## APIs That Won't Be Implemented

Jaunty has deliberately chosen not to implement certain features to maintain its core philosophy:

### Core Design Decisions Against Implementation
1. **Full LINQ-to-SQL Translation**: No complex Expression tree compilation to maintain predictability
2. **Automatic Change Tracking**: No extensive automatic change tracking to keep the library lightweight
3. **Lazy Loading**: No automatic lazy loading to prevent N+1 query problems
4. **Proxy Generation**: No dynamic proxy generation to avoid complexity and performance overhead
5. **Schema Creation/Management**: No code-first schema generation as Jaunty focuses on data access, not schema management
6. **Automatic Migration System**: No built-in migrations to keep focus on data access
7. **Entity Framework-like Context**: No full DbContext with change tracking to maintain simplicity

## Summary

Jaunty has evolved significantly beyond my initial assessment. The addition of the Fluent API assembly represents a major enhancement that adds a comprehensive query builder while maintaining the core philosophy of respecting your SQL. The library now offers:

1. **Dual API Approach**: Both raw SQL execution (core Jaunty) and fluent query building (Jaunty.Fluent)
2. **Type Safety**: Expression-based operations in the fluent API provide compile-time safety
3. **Flexibility**: Raw SQL support for complex scenarios alongside fluent building
4. **Comprehensive Coverage**: Full CRUD operations, joins, aggregations, CTEs, and set operations
5. **Modern Features**: Async/await support, streaming, multiple result sets, and advanced parameter binding

The library strikes an excellent balance between the raw power of direct SQL execution and the convenience of fluent query building. The Fluent API provides many of the conveniences found in other ORMs while maintaining the performance and predictability that Jaunty is known for.

The ecosystem now includes scaffolding tools for database-first development, making it a complete solution for data access in .NET applications.