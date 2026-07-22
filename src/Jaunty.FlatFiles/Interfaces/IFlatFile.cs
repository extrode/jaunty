using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

using Jaunty.Core;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.WriteBack;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.Interfaces;

/// <summary>
/// Represents an embedded database engine that can query flat files.
/// </summary>
public interface IFlatFile : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying ADO.NET connection to the embedded database.
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// Registers a file source so it can be queried as a table.
    /// </summary>
    /// <param name="source">The file source to register.</param>
    void RegisterSource(IFileSource source);

    /// <summary>
    /// Registers a file source asynchronously.
    /// </summary>
    /// <param name="source">The file source to register.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    ValueTask RegisterSourceAsync(IFileSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file source metadata for a registered entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The file source info, or null if not registered.</returns>
    IFileSource? GetSource<T>() where T : class, new();

    /// <summary>
    /// Gets whether any mutations have been made to the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    bool IsModified<T>() where T : class, new();

    // ==========================================
    // Query Operations
    // ==========================================

    /// <summary>
    /// Starts a fluent query for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to query.</typeparam>
    /// <returns>A fluent query builder for chaining WHERE, ORDER BY, and SELECT operations.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no file source is registered for the entity type.</exception>
    /// <remarks>
    /// Call <c>.Select()</c> to execute the query and return all columns, or
    /// <c>.SelectPartial(...)</c> to select specific columns.
    /// </remarks>
    IFromClause<T> Query<T>() where T : class, new();

    /// <summary>
    /// Executes a raw SQL query and returns the results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <returns>A list of entities matching the query.</returns>
    List<T> Query<T>(string sql) where T : class, new();

    /// <summary>
    /// Executes a raw SQL query with parameters and returns the results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <returns>A list of entities matching the query.</returns>
    List<T> Query<T>(string sql, params (string Name, object? Value)[] parameters) where T : class, new();

    /// <summary>
    /// Executes a raw SQL query and returns the results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A list of entities matching the query.</returns>
    ValueTask<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Executes a raw SQL query with parameters and returns the results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A list of entities matching the query.</returns>
    ValueTask<List<T>> QueryAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CancellationToken cancellationToken = default) where T : class, new();

    // ==========================================
    // CRUD Operations
    // ==========================================

    /// <summary>
    /// Inserts a single entity into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to insert.</param>
    /// <returns>The number of rows affected.</returns>
    int Insert<T>(T entity) where T : class, new();

    /// <summary>
    /// Inserts a single entity into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <returns>The number of rows affected.</returns>
    int Insert<T>(T entity, CommandOptions options) where T : class, new();

    /// <summary>
    /// Inserts a single entity into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Inserts a single entity into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> InsertAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Inserts multiple entities into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>The number of rows affected.</returns>
    int Insert<T>(IEnumerable<T> entities) where T : class, new();

    /// <summary>
    /// Inserts multiple entities into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <returns>The number of rows affected.</returns>
    int Insert<T>(IEnumerable<T> entities, CommandOptions options) where T : class, new();

    /// <summary>
    /// Inserts multiple entities into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Inserts multiple entities into the flat file table. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Updates rows matching the predicate by setting the specified column to the given value.
    /// Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to update.</param>
    /// <param name="column">An expression selecting the column to update.</param>
    /// <param name="value">The new value for the column.</param>
    /// <returns>The number of rows affected.</returns>
    int Update<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value) where T : class, new();

    /// <summary>
    /// Updates rows matching the predicate by setting the specified column to the given value.
    /// Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to update.</param>
    /// <param name="column">An expression selecting the column to update.</param>
    /// <param name="value">The new value for the column.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <returns>The number of rows affected.</returns>
    int Update<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CommandOptions options) where T : class, new();

    /// <summary>
    /// Updates rows matching the predicate by setting the specified column to the given value.
    /// Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to update.</param>
    /// <param name="column">An expression selecting the column to update.</param>
    /// <param name="value">The new value for the column.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Updates rows matching the predicate by setting the specified column to the given value.
    /// Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to update.</param>
    /// <param name="column">An expression selecting the column to update.</param>
    /// <param name="value">The new value for the column.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Deletes rows matching the predicate. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to delete.</param>
    /// <returns>The number of rows affected.</returns>
    int Delete<T>(Expression<Func<T, bool>> predicate) where T : class, new();

    /// <summary>
    /// Deletes rows matching the predicate. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to delete.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <returns>The number of rows affected.</returns>
    int Delete<T>(Expression<Func<T, bool>> predicate, CommandOptions options) where T : class, new();

    /// <summary>
    /// Deletes rows matching the predicate. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Deletes rows matching the predicate. Promotes the source from VIEW to TABLE on first mutation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">A filter expression to select rows to delete.</param>
    /// <param name="options">Command options for transaction and timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The number of rows affected.</returns>
    ValueTask<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new();

    // ==========================================
    // Write-Back Operations
    // ==========================================

    /// <summary>
    /// Saves modified data to a new file (non-destructive). The original file is not modified.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="outputPath">The file path to write to. Format is inferred from extension.</param>
    void Save<T>(string outputPath) where T : class, new();

    /// <summary>
    /// Saves modified data to a new file (non-destructive). The original file is not modified.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="outputPath">The file path to write to. Format is inferred from extension.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    ValueTask SaveAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Saves modified data using the specified write-back mode.
    /// When <see cref="WriteBackMode.Overwrite"/>, the original file is replaced atomically (temp + rename).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="mode">The write-back mode.</param>
    void Save<T>(WriteBackMode mode) where T : class, new();

    /// <summary>
    /// Saves modified data using the specified write-back mode.
    /// When <see cref="WriteBackMode.Overwrite"/>, the original file is replaced atomically (temp + rename).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="mode">The write-back mode.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    ValueTask SaveAsync<T>(WriteBackMode mode, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Exports data for the specified entity type to a file, with format inferred from extension.
    /// Can be used for cross-format export (e.g., CSV source → Parquet output).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="outputPath">The file path to export to. Format is inferred from extension.</param>
    void Export<T>(string outputPath) where T : class, new();

    /// <summary>
    /// Exports data for the specified entity type to a file, with format inferred from extension.
    /// Can be used for cross-format export (e.g., CSV source → Parquet output).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="outputPath">The file path to export to. Format is inferred from extension.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    ValueTask ExportAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new();

    // ==========================================
    // Import Operations
    // ==========================================

    /// <summary>
    /// Imports data for the specified entity type into a target database connection.
    /// Reads from the flat file source (via DuckDB) and writes to the target using batched INSERTs.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="targetConnection">The target ADO.NET connection to import into.</param>
    /// <param name="options">Import options (batch size, conflict strategy, etc.).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The total number of rows imported.</returns>
    ValueTask<long> ImportIntoAsync<T>(DbConnection targetConnection, ImportOptions options = default, CancellationToken cancellationToken = default) where T : class, new();
}