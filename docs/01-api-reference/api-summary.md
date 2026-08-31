# Jaunty API Reference Summary

## Complete API Overview

This document provides a comprehensive summary of all public APIs available in Jaunty, organized by functionality.

## Query Methods

### Basic Query Operations
- `Query<T>(string sql)` - Execute query with strict mapping
- `Query<T>(string sql, object parameters)` - Execute parameterized query with strict mapping
- `Query<T>(string sql, CommandOptions<T> options)` - Execute query with options using strict mapping
- `Query<T>(string sql, object parameters, CommandOptions<T> options)` - Execute parameterized query with options using strict mapping

### Partial Query Operations
- `QueryPartial<T>(string sql)` - Execute query with partial mapping
- `QueryPartial<T>(string sql, object parameters)` - Execute parameterized query with partial mapping
- `QueryPartial<T>(string sql, CommandOptions<T> options)` - Execute query with options using partial mapping
- `QueryPartial<T>(string sql, object parameters, CommandOptions<T> options)` - Execute parameterized query with options using partial mapping

### Single Result Query Operations
- `QueryFirst<T>(string sql)` - Get first result with strict mapping
- `QueryFirstOrDefault<T>(string sql)` - Get first result or default with strict mapping
- `QuerySingle<T>(string sql)` - Get single result with strict mapping
- `QuerySingleOrDefault<T>(string sql)` - Get single result or default with strict mapping

### Scalar Query Operations
- `QueryScalar<T>(string sql)` - Get scalar value
- `QueryScalar<T>(string sql, object parameters)` - Get scalar value from parameterized query
- `QueryScalar<T>(string sql, CommandOptions options)` - Get scalar value with options
- `QueryScalar<T>(string sql, object parameters, CommandOptions options)` - Get scalar value from parameterized query with options

## Async Query Methods

### Async Basic Query Operations
- `Task<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default)`
- `Task<List<T>> QueryAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)`
- `Task<List<T>> QueryAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)`
- `Task<List<T>> QueryAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)`

### Async Single Result Operations
- `Task<T> QueryFirstAsync<T>(string sql, CancellationToken cancellationToken = default)`
- `Task<T?> QueryFirstOrDefaultAsync<T>(string sql, CancellationToken cancellationToken = default)`
- `Task<T> QuerySingleAsync<T>(string sql, CancellationToken cancellationToken = default)`
- `Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken = default)`

### Async Scalar Operations
- `Task<T> QueryScalarAsync<T>(string sql, CancellationToken cancellationToken = default)`
- `Task<T> QueryScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)`
- `Task<T> QueryScalarAsync<T>(string sql, CommandOptions options, CancellationToken cancellationToken = default)`
- `Task<T> QueryScalarAsync<T>(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)`

## Streaming Methods

### Synchronous Streaming
- `IEnumerable<T> QueryStream<T>(string sql)` - Stream results with strict mapping
- `IEnumerable<T> QueryStream<T>(string sql, object parameters)` - Stream parameterized results with strict mapping
- `IEnumerable<T> QueryStream<T>(string sql, CommandOptions<T> options)` - Stream results with options using strict mapping
- `IEnumerable<T> QueryStream<T>(string sql, object parameters, CommandOptions<T> options)` - Stream parameterized results with options using strict mapping

### Asynchronous Streaming
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, [EnumeratorCancellation] CancellationToken cancellationToken = default)` - Async stream results with strict mapping
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, [EnumeratorCancellation] CancellationToken cancellationToken = default)` - Async stream parameterized results with strict mapping
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CommandOptions<T> options, [EnumeratorCancellation] CancellationToken cancellationToken = default)` - Async stream results with options using strict mapping
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, [EnumeratorCancellation] CancellationToken cancellationToken = default)` - Async stream parameterized results with options using strict mapping

### Partial Streaming
- `IEnumerable<T> QueryPartialStream<T>(string sql)` - Stream results with partial mapping
- `IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, [EnumeratorCancellation] CancellationToken cancellationToken = default)` - Async stream results with partial mapping
- (Similar overloads with parameters and options)

## Multiple Result Set Methods

### QueryMultiple
- `GridReader QueryMultiple(string sql)` - Execute multiple result sets
- `GridReader QueryMultiple(string sql, object parameters)` - Execute multiple parameterized result sets
- `GridReader QueryMultiple(string sql, CommandOptions options)` - Execute multiple result sets with options
- `GridReader QueryMultiple(string sql, object parameters, CommandOptions options)` - Execute multiple parameterized result sets with options

### Async QueryMultiple
- `Task<GridReader> QueryMultipleAsync(string sql, CancellationToken cancellationToken = default)` - Async multiple result sets
- `Task<GridReader> QueryMultipleAsync(string sql, object parameters, CancellationToken cancellationToken = default)` - Async multiple parameterized result sets
- `Task<GridReader> QueryMultipleAsync(string sql, CommandOptions options, CancellationToken cancellationToken = default)` - Async multiple result sets with options
- `Task<GridReader> QueryMultipleAsync(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)` - Async multiple parameterized result sets with options

## CRUD Operations

### Insert Operations
- `long Insert<T>(T entity)` - Insert entity and return identity
- `long Insert<T>(T entity, CommandOptions options)` - Insert entity with options and return identity
- `Task<long> InsertAsync<T>(T entity, CancellationToken cancellationToken = default)` - Async insert entity and return identity
- `Task<long> InsertAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` - Async insert entity with options and return identity

### Update Operations
- `int Update<T>(T entity)` - Update entity
- `int Update<T>(T entity, CommandOptions options)` - Update entity with options
- `Task<int> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)` - Async update entity
- `Task<int> UpdateAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` - Async update entity with options

### Delete Operations
- `int Delete<T>(T entity)` - Delete entity by primary key
- `int Delete<T>(T entity, CommandOptions options)` - Delete entity with options
- `int Delete<T>(object id)` - Delete entity by ID
- `int Delete<T>(object id, CommandOptions options)` - Delete entity by ID with options
- `Task<int> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default)` - Async delete entity
- `Task<int> DeleteAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` - Async delete entity with options
- `Task<int> DeleteAsync<T>(object id, CancellationToken cancellationToken = default)` - Async delete entity by ID
- `Task<int> DeleteAsync<T>(object id, CommandOptions options, CancellationToken cancellationToken = default)` - Async delete entity by ID with options

### Upsert Operations
- `int Upsert<T>(T entity)` - Insert or update entity by key
- `int Upsert<T>(T entity, CommandOptions options)` - Insert or update entity with options
- `Task<int> UpsertAsync<T>(T entity, CancellationToken cancellationToken = default)` - Async insert or update entity
- `Task<int> UpsertAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` - Async insert or update entity with options

Upsert return values are provider-specific "rows affected" semantics:
- SQL Server and PostgreSQL typically return `1` for a successful insert or update.
- MariaDB/MySQL may return `2` for an update path (matched + changed rows), and may return `1` depending on server/client flags.

For cross-provider code, treat any value `> 0` as success instead of asserting an exact constant.

## Get, GetAll, and Execute Methods

See [Get and Execute Operations](get-and-execute-operations.md) for full details.

- `T? Get<T>(object id)` / `Get<T, TId>(TId id)` - Retrieve entity by primary key
- `T GetRequired<T>(object id)` / `GetRequired<T, TId>(TId id)` - Retrieve entity by primary key, throwing if not found
- `List<T> GetAll<T>()` - Retrieve all rows for a mapped table
- `IEnumerable<T> GetAllStream<T>()` - Lazily stream all rows
- `GetAsync`, `GetRequiredAsync`, `GetAllAsync`, `GetAllStreamAsync` - Async equivalents (require `DbConnection`)
- `int Execute(string sql, ...)` - Execute a non-query SQL command
- `int ExecuteBatch(string sql, IEnumerable<object> parameterSets, ...)` - Execute the same SQL once per parameter set
- `ExecuteAsync`, `ExecuteBatchAsync` - Async equivalents

## Multi-Entity Mapping Methods

See [Multi-Entity Mapping](multi-entity-mapping.md) for full details.

- `List<(T1,...,TN)> Query<T1,...,TN>(string sql, ...)` for N = 2..7 - Map joined query rows into N entity types via left-to-right ordinal claiming
- `QueryFirst<T1,...,TN>`, `QueryFirstOrDefault<T1,...,TN>`, `QuerySingle<T1,...,TN>`, `QuerySingleOrDefault<T1,...,TN>`, `QueryStream<T1,...,TN>` - Same arities, single-result and streaming variants
- `MultiEntityCommandOptions<T1,...,TN>` - Transaction/CommandTimeout/CommandType options carrier for multi-entity queries (no per-position custom mapper support)

## Stored Procedure Methods

### Basic Stored Procedure Execution
- `List<T> ExecuteStoredProcedure<T>(string procedureName)` - Execute stored procedure and return results
- `List<T> ExecuteStoredProcedure<T>(string procedureName, object? parameters)` - Execute parameterized stored procedure and return results
- `List<T> ExecuteStoredProcedure<T>(string procedureName, object? parameters, CommandOptions<T> options)` - Execute parameterized stored procedure with options and return results

### Single Result Stored Procedure Methods
- `T ExecuteStoredProcedureFirst<T>(string procedureName)` - Execute stored procedure and return first result
- `T? ExecuteStoredProcedureFirstOrDefault<T>(string procedureName)` - Execute stored procedure and return first result or default
- `T ExecuteStoredProcedureSingle<T>(string procedureName)` - Execute stored procedure and return single result
- `T? ExecuteStoredProcedureSingleOrDefault<T>(string procedureName)` - Execute stored procedure and return single result or default

### Scalar Stored Procedure Methods
- `T ExecuteStoredProcedureScalar<T>(string procedureName)` - Execute stored procedure and return scalar value
- `T ExecuteStoredProcedureScalar<T>(string procedureName, object? parameters)` - Execute parameterized stored procedure and return scalar value
- `T ExecuteStoredProcedureScalar<T>(string procedureName, object? parameters, CommandOptions options)` - Execute parameterized stored procedure with options and return scalar value

### Non-Query Stored Procedure Methods
- `int ExecuteStoredProcedureNonQuery(string procedureName)` - Execute stored procedure that doesn't return results
- `int ExecuteStoredProcedureNonQuery(string procedureName, object? parameters)` - Execute parameterized stored procedure that doesn't return results
- `int ExecuteStoredProcedureNonQuery(string procedureName, object? parameters, CommandOptions options)` - Execute parameterized stored procedure with options that doesn't return results

### Async Stored Procedure Methods
- `Task<List<T>> ExecuteStoredProcedureAsync<T>(string procedureName, CancellationToken cancellationToken = default)` - Async stored procedure execution
- `Task<T> ExecuteStoredProcedureFirstAsync<T>(string procedureName, CancellationToken cancellationToken = default)` - Async first result from stored procedure
- `Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(string procedureName, CancellationToken cancellationToken = default)` - Async first result or default from stored procedure
- `Task<T> ExecuteStoredProcedureSingleAsync<T>(string procedureName, CancellationToken cancellationToken = default)` - Async single result from stored procedure
- `Task<T?> ExecuteStoredProcedureSingleOrDefaultAsync<T>(string procedureName, CancellationToken cancellationToken = default)` - Async single result or default from stored procedure
- `Task<T> ExecuteStoredProcedureScalarAsync<T>(string procedureName, CancellationToken cancellationToken = default)` - Async scalar from stored procedure
- `Task<int> ExecuteStoredProcedureNonQueryAsync(string procedureName, CancellationToken cancellationToken = default)` - Async non-query stored procedure

### Stored Procedure Methods with SpParameters
- Methods supporting input, output, and return parameters using the `SpParameters` class

## Fluent API

### Entry Point
- `IFromClause<T> From<T>(this IDbConnection connection, string? alias = null)` - Start fluent query

### Fluent Query Methods
- `IWhereClause<T> Where(...)` - Add WHERE conditions
- `IOrderByClause<T> OrderBy(...)` - Add ORDER BY clauses
- `IJoinClause<T, TJoin> InnerJoin<TJoin>()` - Add INNER JOIN
- `IJoinClause<T, TJoin> LeftJoin<TJoin>()` - Add LEFT JOIN
- `IDistinctClause<T> Distinct()` - Add DISTINCT
- `IFromClause<T> Take(int count)` - Add LIMIT/TOP
- `IFromClause<T> Skip(int count)` - Add OFFSET

### Fluent Terminal Operations
- `List<T> Select()` - Execute query and return all results
- `T SelectFirst()` - Execute query and return first result
- `T? SelectFirstOrDefault()` - Execute query and return first result or default
- `T SelectSingle()` - Execute query and return single result
- `T? SelectSingleOrDefault()` - Execute query and return single result or default
- `int Count()` - Execute query and return count
- `TResult Sum<TResult>(Expression<Func<T, TResult>> selector)` - Execute query and return sum
- `Task<List<T>> SelectAsync(...)` - Async versions of all terminal operations

## Configuration

### JauntyConfig Class
- `SchemaNameResolver` - Global schema name resolver
- `TableNameResolver` - Global table name resolver
- `ColumnNameResolver` - Global column name resolver
- `DefaultEnumStorage` - Global default enum storage strategy (numeric or string)
- `RegisterTypeHandler<T>(fromDb, toDb)` / `RegisterTypeHandler<T>(TypeHandler<T>)` - Register a custom type handler
- `RemoveTypeHandler<T>()` - Remove a registered type handler
- `Reset()` - Reset all configuration to defaults

### CommandOptions Struct
- `WithMapper(Func<IDataReader, T> mapper)` - Create options with custom mapper
- `WithTransaction(IDbTransaction transaction)` - Create options with transaction
- `WithTimeout(int seconds)` - Create options with timeout

## Attributes

### Available Attributes
- `[Table("name", Schema = "schema")]` - Map class to table
- `[Column("name")]` - Map property to column
- `[Ignore]` - Exclude property from mapping
- `[Key]` - Mark property as primary key
- `[DatabaseGenerated(DatabaseGeneratedOption option)]` - Specify database generation behavior
- `[EnumStorage(EnumStorage storage)]` - Override enum storage strategy for a single property

## GridReader Methods

### GridReader Read Methods
- `List<T> Read<T>()` - Read next result set with strict mapping
- `List<T> ReadPartial<T>()` - Read next result set with partial mapping
- `T ReadFirst<T>()` - Read first from next result set with strict mapping
- `T? ReadFirstOrDefault<T>()` - Read first or default from next result set with strict mapping
- `T ReadSingle<T>()` - Read single from next result set with strict mapping
- `T? ReadSingleOrDefault<T>()` - Read single or default from next result set with strict mapping
- `T? ReadScalar<T>()` - Read scalar from next result set
- `IEnumerable<T> ReadStream<T>()` - Read next result set as stream with strict mapping
- `IEnumerable<T> ReadPartialStream<T>()` - Read next result set as stream with partial mapping

### Async GridReader Methods
- `Task<List<T>> ReadAsync<T>()` - Async read next result set with strict mapping
- `Task<T> ReadFirstAsync<T>()` - Async read first from next result set with strict mapping
- `Task<T?> ReadFirstOrDefaultAsync<T>()` - Async read first or default from next result set with strict mapping
- `Task<T> ReadSingleAsync<T>()` - Async read single from next result set with strict mapping
- `Task<T?> ReadSingleOrDefaultAsync<T>()` - Async read single or default from next result set with strict mapping
- `Task<T?> ReadScalarAsync<T>()` - Async read scalar from next result set
- `IAsyncEnumerable<T> ReadStreamAsync<T>()` - Async read next result set as stream with strict mapping
- `IAsyncEnumerable<T> ReadPartialStreamAsync<T>()` - Async read next result set as stream with partial mapping

## Key Concepts

### Mapping Modes
- **Strict Mode** (default): All entity properties must have matching columns in result set
- **Partial Mode**: Only map properties with matching columns, ignore missing ones

### Connection Requirements
- Synchronous operations: `IDbConnection`
- Asynchronous operations: `DbConnection` (required for async methods that access data reader)

### Error Handling
- Jaunty provides descriptive error messages for mapping failures
- Strict mode fails fast with clear error messages
- Partial mode allows missing columns but maps only available ones

### Performance Considerations
- Metadata is cached per-type for optimal performance
- Expression trees are compiled once and reused
- Connection state is respected (opens if closed, leaves open if already open)
