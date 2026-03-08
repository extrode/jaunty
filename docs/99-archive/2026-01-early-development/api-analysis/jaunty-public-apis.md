# Jaunty Public API Reference

This document provides a comprehensive list of all public APIs currently available in Jaunty.

## Namespace: Jaunty

### Query Methods (Read Operations)
- `List<T> Query<T>(string sql)` where T : new()
- `List<T> Query<T>(string sql, object parameters)` where T : new()
- `List<T> Query<T>(string sql, CommandOptions<T> options)` where T : new()
- `List<T> Query<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `Task<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> QueryAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> QueryAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> QueryAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### Partial Query Methods (Read Operations)
- `List<T> QueryPartial<T>(string sql)` where T : new()
- `List<T> QueryPartial<T>(string sql, object parameters)` where T : new()
- `List<T> QueryPartial<T>(string sql, CommandOptions<T> options)` where T : new()
- `List<T> QueryPartial<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `Task<List<T>> QueryPartialAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> QueryPartialAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> QueryPartialAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> QueryPartialAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### Scalar Query Methods (Read Operations)
- `T QueryScalar<T>(string sql)`
- `T QueryScalar<T>(string sql, object parameters)`
- `T QueryScalar<T>(string sql, CommandOptions<T> options)`
- `T QueryScalar<T>(string sql, object parameters, CommandOptions<T> options)`

- `Task<T> QueryScalarAsync<T>(string sql, CancellationToken cancellationToken = default)`
- `Task<T> QueryScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)`
- `Task<T> QueryScalarAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)`
- `Task<T> QueryScalarAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)`

### First Query Methods (Read Operations)
- `T QueryFirst<T>(string sql)` where T : new()
- `T QueryFirst<T>(string sql, object parameters)` where T : new()
- `T QueryFirst<T>(string sql, CommandOptions<T> options)` where T : new()
- `T QueryFirst<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `Task<T> QueryFirstAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> QueryFirstAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> QueryFirstAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> QueryFirstAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### FirstOrDefault Query Methods (Read Operations)
- `T? QueryFirstOrDefault<T>(string sql)` where T : new()
- `T? QueryFirstOrDefault<T>(string sql, object parameters)` where T : new()
- `T? QueryFirstOrDefault<T>(string sql, CommandOptions<T> options)` where T : new()
- `T? QueryFirstOrDefault<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `Task<T?> QueryFirstOrDefaultAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> QueryFirstOrDefaultAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### Single Query Methods (Read Operations)
- `T QuerySingle<T>(string sql)` where T : new()
- `T QuerySingle<T>(string sql, object parameters)` where T : new()
- `T QuerySingle<T>(string sql, CommandOptions<T> options)` where T : new()
- `T QuerySingle<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `Task<T> QuerySingleAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> QuerySingleAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> QuerySingleAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> QuerySingleAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### SingleOrDefault Query Methods (Read Operations)
- `T? QuerySingleOrDefault<T>(string sql)` where T : new()
- `T? QuerySingleOrDefault<T>(string sql, object parameters)` where T : new()
- `T? QuerySingleOrDefault<T>(string sql, CommandOptions<T> options)` where T : new()
- `T? QuerySingleOrDefault<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### Streaming Query Methods (Read Operations)
- `IEnumerable<T> QueryStream<T>(string sql)` where T : new()
- `IEnumerable<T> QueryStream<T>(string sql, object parameters)` where T : new()
- `IEnumerable<T> QueryStream<T>(string sql, CommandOptions<T> options)` where T : new()
- `IEnumerable<T> QueryStream<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### Partial Streaming Query Methods (Read Operations)
- `IEnumerable<T> QueryPartialStream<T>(string sql)` where T : new()
- `IEnumerable<T> QueryPartialStream<T>(string sql, object parameters)` where T : new()
- `IEnumerable<T> QueryPartialStream<T>(string sql, CommandOptions<T> options)` where T : new()
- `IEnumerable<T> QueryPartialStream<T>(string sql, object parameters, CommandOptions<T> options)` where T : new()

- `IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)` where T : new()

### Multiple Result Set Methods
- `GridReader QueryMultiple(string sql)`
- `GridReader QueryMultiple(string sql, object parameters)`
- `GridReader QueryMultiple(string sql, CommandOptions options)`
- `GridReader QueryMultiple(string sql, object parameters, CommandOptions options)`

- `Task<GridReader> QueryMultipleAsync(string sql, CancellationToken cancellationToken = default)`
- `Task<GridReader> QueryMultipleAsync(string sql, object parameters, CancellationToken cancellationToken = default)`
- `Task<GridReader> QueryMultipleAsync(string sql, CommandOptions options, CancellationToken cancellationToken = default)`
- `Task<GridReader> QueryMultipleAsync(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)`

### Write Operations (Newly Added)
- `long Insert<T>(T entity)` where T : class, new()
- `long Insert<T>(T entity, CommandOptions options)` where T : class, new()
- `Task<long> InsertAsync<T>(T entity, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<long> InsertAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()

- `int Update<T>(T entity)` where T : class, new()
- `int Update<T>(T entity, CommandOptions options)` where T : class, new()
- `Task<int> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> UpdateAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()

- `int Delete<T>(T entity)` where T : class, new()
- `int Delete<T>(T entity, CommandOptions options)` where T : class, new()
- `int Delete<T>(object id)` where T : class, new()
- `int Delete<T>(object id, CommandOptions options)` where T : class, new()
- `Task<int> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> DeleteAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> DeleteAsync<T>(object id, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> DeleteAsync<T>(object id, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()

### Bulk Write Operations (Newly Added)
- `int BulkInsert<T>(IEnumerable<T> entities)` where T : class, new()
- `int BulkInsert<T>(IEnumerable<T> entities, CommandOptions options)` where T : class, new()
- `int BulkInsertIgnoreConstraints<T>(IEnumerable<T> entities)` where T : class, new()
- `int BulkInsertIgnoreConstraints<T>(IEnumerable<T> entities, CommandOptions options)` where T : class, new()
- `Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkInsertIgnoreConstraintsAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkInsertIgnoreConstraintsAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()

- `int BulkUpdate<T>(IEnumerable<T> entities)` where T : class, new()
- `int BulkUpdate<T>(IEnumerable<T> entities, CommandOptions options)` where T : class, new()
- `int BulkUpdateIgnoreConstraints<T>(IEnumerable<T> entities)` where T : class, new()
- `int BulkUpdateIgnoreConstraints<T>(IEnumerable<T> entities, CommandOptions options)` where T : class, new()
- `Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkUpdateIgnoreConstraintsAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkUpdateIgnoreConstraintsAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()

- `int BulkDelete<T>(IEnumerable<T> entities)` where T : class, new()
- `int BulkDelete<T>(IEnumerable<T> entities, CommandOptions options)` where T : class, new()
- `int BulkDeleteIgnoreConstraints<T>(IEnumerable<T> entities)` where T : class, new()
- `int BulkDeleteIgnoreConstraints<T>(IEnumerable<T> entities, CommandOptions options)` where T : class, new()
- `Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkDeleteIgnoreConstraintsAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)` where T : class, new()
- `Task<int> BulkDeleteIgnoreConstraintsAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default)` where T : class, new()

### Multi-Entity Mapping (Newly Added)
- `List<(T1, T2)> Query<T1, T2>(string sql)` where T1 : new(), T2 : new()
- `List<(T1, T2)> Query<T1, T2>(string sql, object parameters)` where T1 : new(), T2 : new()
- `List<TResult> Query<T1, T2, TResult>(string sql, Func<T1, T2, TResult> combiner)` where T1 : new(), T2 : new()
- `List<TResult> Query<T1, T2, TResult>(string sql, object parameters, Func<T1, T2, TResult> combiner)` where T1 : new(), T2 : new()
- Plus QueryFirst, QuerySingle, QueryStream variants and async versions

### GridReader Methods (for Multiple Result Sets)
- `List<T> Read<T>(CommandOptions<T> options = default)` where T : new()
- `List<T> ReadPartial<T>(CommandOptions<T> options = default)` where T : new()
- `T ReadFirst<T>(CommandOptions<T> options = default)` where T : new()
- `T? ReadFirstOrDefault<T>(CommandOptions<T> options = default)` where T : new()
- `T ReadPartialFirst<T>(CommandOptions<T> options = default)` where T : new()
- `T? ReadPartialFirstOrDefault<T>(CommandOptions<T> options = default)` where T : new()
- `T ReadSingle<T>(CommandOptions<T> options = default)` where T : new()
- `T? ReadSingleOrDefault<T>(CommandOptions<T> options = default)` where T : new()
- `T ReadPartialSingle<T>(CommandOptions<T> options = default)` where T : new()
- `T? ReadPartialSingleOrDefault<T>(CommandOptions<T> options = default)` where T : new()
- `T? ReadScalar<T>(CommandOptions options = default)`
- `IEnumerable<T> ReadStream<T>(CommandOptions<T> options = default)` where T : new()
- `IEnumerable<T> ReadPartialStream<T>(CommandOptions<T> options = default)` where T : new()

- `Task<List<T>> ReadAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<List<T>> ReadPartialAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> ReadFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> ReadFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> ReadPartialFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> ReadPartialFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> ReadSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> ReadSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T> ReadPartialSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> ReadPartialSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)` where T : new()
- `Task<T?> ReadScalarAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default)`
- `IAsyncEnumerable<T> ReadStreamAsync<T>(CommandOptions<T> options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)` where T : new()
- `IAsyncEnumerable<T> ReadPartialStreamAsync<T>(CommandOptions<T> options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)` where T : new()

## Namespace: Jaunty.Configuration

### Configuration
- `static Func<Type, string>? SchemaNameResolver { get; set; }`
- `static Func<Type, string>? TableNameResolver { get; set; }`
- `static Func<string, string>? ColumnNameResolver { get; set; }`
- `static void Reset()`

## Namespace: Jaunty.Core

### Command Options
- `readonly struct CommandOptions<T>(Func<IDataReader, T>? mapper = null, IDbTransaction? transaction = null, int? commandTimeout = null)`
- `static CommandOptions<T> WithMapper(Func<IDataReader, T> mapper)`
- `static CommandOptions<T> WithTransaction(IDbTransaction transaction)`
- `static CommandOptions<T> WithTimeout(int seconds)`
- `static CommandOptions<T> With(Func<IDataReader, T> mapper, IDbTransaction transaction, int timeoutSeconds)`
- `static implicit operator CommandOptions(CommandOptions<T> options)`

- `readonly struct CommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null)`
- `static CommandOptions WithTransaction(IDbTransaction transaction)`
- `static CommandOptions WithTimeout(int seconds)`
- `static CommandOptions With(IDbTransaction transaction, int timeoutSeconds)`

## Namespace: Jaunty.Attributes

### Attributes
- `TableAttribute(string name, string? schema = null)`
- `ColumnAttribute(string name)`
- `IgnoreAttribute`
- `KeyAttribute`
- `DatabaseGeneratedAttribute(DatabaseGeneratedOption option)`
- `DatabaseGeneratedOption` enum

## Summary
Jaunty currently provides a comprehensive set of APIs for both read and write operations, with full async support, streaming capabilities, multiple result sets, and configuration options. The API surface is well-organized with consistent patterns across sync/async and different operation types.