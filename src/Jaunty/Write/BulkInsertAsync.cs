using System.Data;
using System.Data.Common;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.BulkCopy;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously inserts multiple entities into the database in a single transaction.
    /// </summary>
    public static ValueTask<int> BulkInsertAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts multiple entities into the database in a single transaction with command options.
    /// </summary>
    public static ValueTask<int> BulkInsertAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertCoreAsync(dbConnection, entities, options, ignoreConstraints: false, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts multiple entities into the database, bypassing foreign key constraint checks.
    /// </summary>
    public static ValueTask<int> BulkInsertIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertIgnoreConstraintsAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts multiple entities into the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    public static ValueTask<int> BulkInsertIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertCoreAsync(dbConnection, entities, options, ignoreConstraints: true, cancellationToken);
    }

    private static async ValueTask<int> BulkInsertCoreAsync<T>(DbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints, CancellationToken cancellationToken) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif

        var entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0)
            return 0;

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkInsertAsync instead, or disable constraints manually before calling this method.");

        // Check if native bulk copy should be used
        if (BulkCopyConfiguration.EnableNativeBulkCopy &&
            dialect.SupportsNativeBulkCopy &&
            entityList.Count >= BulkCopyConfiguration.MinimumRowsForNativeBulkCopy)
        {
            var bulkProvider = dialect.CreateBulkCopyProvider();
            if (bulkProvider != null && bulkProvider.IsSupported)
            {
                return await BulkInsertNativeCoreAsync(connection, entityList, cached, bulkProvider, options, ignoreConstraints, cancellationToken).ConfigureAwait(false);
            }
        }

        bool wasClosed = connection.State == ConnectionState.Closed;
        DbTransaction? transaction = options.Transaction as DbTransaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (ownTransaction)
            {
#if NET8_0_OR_GREATER
                transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#else
                transaction = connection.BeginTransaction();
#endif
            }

            if (ignoreConstraints)
            {
#if NET8_0_OR_GREATER
                await using var fkOffCmd = connection.CreateCommand();
#else
                using var fkOffCmd = connection.CreateCommand();
#endif
                fkOffCmd.Transaction = transaction;
                fkOffCmd.CommandText = dialect.GetDisableForeignKeyChecksSql()!;
                await fkOffCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            int totalInserted = 0;

            try
            {
                var valueSetter = WriteParameterCache<T>.InsertValueSetter;
                if (valueSetter == null)
                    throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

                // Multi-row INSERT reduces round-trips for network databases, but hurts
                // in-process providers like SQLite where parameter object overhead exceeds savings.
                if (dialect.SupportsMultiRowInsert && entityList.Count > 1 && dialect is not SQLiteDialect)
                {
                    totalInserted = await BulkInsertMultiRowAsync(connection, entityList, cached, dialect, transaction, options, valueSetter, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    totalInserted = await BulkInsertLoopAsync(connection, entityList, cached, transaction, options, valueSetter, cancellationToken).ConfigureAwait(false);
                }

                if (ignoreConstraints)
                {
#if NET8_0_OR_GREATER
                    await using var fkOnCmd = connection.CreateCommand();
#else
                    using var fkOnCmd = connection.CreateCommand();
#endif
                    fkOnCmd.Transaction = transaction;
                    fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                    await fkOnCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                if (ownTransaction)
                {
#if NET8_0_OR_GREATER
                    await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
                    transaction!.Commit();
#endif
                }

                return totalInserted;
            }
            catch
            {
                if (ignoreConstraints)
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        await using var fkOnCmd = connection.CreateCommand();
#else
                        using var fkOnCmd = connection.CreateCommand();
#endif
                        fkOnCmd.Transaction = transaction;
                        fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                        await fkOnCmd.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* Best effort */ }
                }

                if (ownTransaction)
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        await transaction!.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
#else
                        transaction?.Rollback();
#endif
                    }
                    catch { /* Best effort */ }
                }

                throw;
            }
        }
        finally
        {
            if (ownTransaction)
            {
#if NET8_0_OR_GREATER
                if (transaction is not null)
                    await transaction.DisposeAsync().ConfigureAwait(false);
#else
                transaction?.Dispose();
#endif
            }

            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }

    /// <summary>
    /// Async multi-row INSERT path: batches entities into INSERT ... VALUES (...), (...), ... statements.
    /// Dramatically reduces round-trips compared to individual INSERTs.
    /// </summary>
    private static async ValueTask<int> BulkInsertMultiRowAsync<T>(
        DbConnection connection,
        IList<T> entityList,
        CachedCrudSql cached,
        ISqlDialect dialect,
        DbTransaction? transaction,
        CommandOptions options,
        Action<IDataParameterCollection, T> valueSetter,
        CancellationToken cancellationToken) where T : new()
    {
        var insertableColumns = ColumnMetadataHelper.GetInsertableColumns(cached.Metadata);
        int colCount = insertableColumns.Count;
        if (colCount == 0) return 0;

        // Compute optimal batch size respecting provider parameter limits
        int maxBatchSize = Math.Min((dialect.MaxParametersPerStatement - 1) / colCount, 1000);
        if (maxBatchSize < 1) maxBatchSize = 1;

        // Use cached compiled property getters for multi-row binding
        var getters = MultiRowInsertCache.GetOrBuildGetters<T>(cached.Metadata);

        int totalInserted = 0;
        int entityCount = entityList.Count;
        int offset = 0;

        while (offset < entityCount)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int batchSize = Math.Min(maxBatchSize, entityCount - offset);

            string sql = MultiRowInsertCache.GetOrBuild(
                typeof(T), connection.GetType(), batchSize, cached.Metadata, dialect);

#if NET8_0_OR_GREATER
            await using var command = connection.CreateCommand();
#else
            using var command = connection.CreateCommand();
#endif
            command.Transaction = transaction;
            command.CommandText = sql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Create parameters for all rows in this batch
            for (int row = 0; row < batchSize; row++)
            {
                T entity = entityList[offset + row];
                for (int c = 0; c < colCount; c++)
                {
                    var p = command.CreateParameter();
                    // Parameter name matches SQL generated in MultiRowInsertCache.Build()
                    p.ParameterName = insertableColumns[c].ColumnName + "_" + row;
                    p.Value = getters[c](entity) ?? DBNull.Value;
                    command.Parameters.Add(p);
                }
            }

            totalInserted += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            offset += batchSize;
        }

        return totalInserted;
    }

    /// <summary>
    /// Async loop-based INSERT fallback: executes individual INSERT statements.
    /// Used when multi-row INSERT is not supported or for single entities.
    /// </summary>
    private static async ValueTask<int> BulkInsertLoopAsync<T>(
        DbConnection connection,
        IList<T> entityList,
        CachedCrudSql cached,
        DbTransaction? transaction,
        CommandOptions options,
        Action<IDataParameterCollection, T> valueSetter,
        CancellationToken cancellationToken) where T : new()
    {
#if NET8_0_OR_GREATER
        await using var command = connection.CreateCommand();
#else
        using var command = connection.CreateCommand();
#endif
        command.Transaction = transaction;
        command.CommandText = cached.InsertSql;

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        PrepareInsertParameters(command, cached.Metadata);

        // Set first entity values before Prepare() so providers can infer parameter types.
        // Prepare() is a best-effort optimization; some providers (e.g. SQL Server on .NET Framework)
        // require explicit DbType on all parameters, which we can't guarantee here.
        var pCollection = command.Parameters;
        valueSetter(pCollection, entityList[0]);
        try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }

        int totalInserted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        for (int i = 1; i < entityList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            valueSetter(pCollection, entityList[i]);
            totalInserted += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return totalInserted;
    }

    /// <summary>
    /// Async native bulk copy path: uses database-specific bulk copy APIs (e.g., SqlBulkCopy, NpgsqlBinaryImporter).
    /// Provides 10-100x performance improvement for large datasets (100+ rows).
    /// </summary>
    private static async ValueTask<int> BulkInsertNativeCoreAsync<T>(
        DbConnection connection,
        IList<T> entityList,
        CachedCrudSql cached,
        IBulkCopyProvider bulkProvider,
        CommandOptions options,
        bool ignoreConstraints,
        CancellationToken cancellationToken) where T : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        DbTransaction? transaction = options.Transaction as DbTransaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (ownTransaction)
            {
#if NET8_0_OR_GREATER
                transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#else
                transaction = connection.BeginTransaction();
#endif
            }

            // Build bulk copy options from CommandOptions
            var bulkOptions = new BulkCopyOptions
            {
                BatchSize = BulkCopyConfiguration.DefaultBatchSize,
                Timeout = options.CommandTimeout ?? BulkCopyConfiguration.DefaultTimeout,
                Transaction = transaction,
                IdentityMode = BulkCopyConfiguration.DefaultIdentityMode,
                CheckConstraints = !ignoreConstraints && BulkCopyConfiguration.DefaultCheckConstraints,
                TableLock = BulkCopyConfiguration.DefaultCheckConstraints ? TableLockOption.BulkLock : TableLockOption.Default
            };

            int totalInserted = 0;

            try
            {
                // Create EntityDataReader for streaming entity-to-datareader conversion
                var metadata = cached.Metadata;
                using var reader = new EntityDataReader<T>(entityList, metadata);

                // Execute native bulk copy
                totalInserted = await bulkProvider.CopyToServerAsync(connection, cached.Metadata.TableName, reader, bulkOptions, cancellationToken).ConfigureAwait(false);

                if (ownTransaction)
                {
#if NET8_0_OR_GREATER
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
                    transaction.Commit();
#endif
                }

                return totalInserted;
            }
            catch
            {
                if (ownTransaction)
                {
#if NET8_0_OR_GREATER
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
#else
                    transaction.Rollback();
#endif
                }
                throw;
            }
        }
        finally
        {
            if (ownTransaction)
            {
#if NET8_0_OR_GREATER
                await transaction.DisposeAsync().ConfigureAwait(false);
#else
                transaction.Dispose();
#endif
            }

            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
