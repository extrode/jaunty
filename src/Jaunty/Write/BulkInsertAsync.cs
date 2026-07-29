using System.Data;
using System.Data.Common;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Write;
using Jaunty.Internals.Read;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously inserts multiple entities into the database in a single transaction.
    /// </summary>
    /// <remarks>
    /// Identity values are populated back onto entities only when the row count and provider
    /// combination routes through the loop-based insert path (one command per entity). The
    /// multi-row VALUES and native bulk-copy paths - used automatically for larger batches on
    /// providers that support them - do not populate identity values, since there is no
    /// provider-agnostic way to map a single "last inserted id" back to individual rows within a
    /// batched or native bulk statement. Callers that need populated IDs should use single-row
    /// <see cref="InsertAsync{T}(IDbConnection, T, CancellationToken)"/> in a loop, or query the
    /// inserted rows back afterward.
    /// </remarks>
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
    /// <remarks>
    /// See <see cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/> for
    /// the identity-population caveat: only the loop-based insert path populates entity IDs back.
    /// </remarks>
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
    /// <remarks>
    /// <para>
    /// See <see cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/> for
    /// the identity-population caveat: only the loop-based insert path populates entity IDs back.
    /// </para>
    /// <para>
    /// For datasets at or above the native bulk-copy threshold, constraints are bypassed via the
    /// provider's bulk-copy <c>CheckConstraints</c> option rather than session-level FK-toggle SQL;
    /// the two mechanisms are not guaranteed to be equivalent across providers/constraint types.
    /// The <see cref="NotSupportedException"/> below only applies to datasets below that threshold.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support session-level foreign key toggling
    /// (e.g., SQL Server) and the dataset is below the native bulk-copy threshold.
    /// </exception>
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
    /// <remarks>
    /// <para>
    /// See <see cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/> for
    /// the identity-population caveat: only the loop-based insert path populates entity IDs back.
    /// </para>
    /// <para>
    /// For datasets at or above the native bulk-copy threshold, constraints are bypassed via the
    /// provider's bulk-copy <c>CheckConstraints</c> option rather than session-level FK-toggle SQL;
    /// the two mechanisms are not guaranteed to be equivalent across providers/constraint types.
    /// The <see cref="NotSupportedException"/> below only applies to datasets below that threshold.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support session-level foreign key toggling
    /// and the dataset is below the native bulk-copy threshold.
    /// </exception>
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

    private static ValueTask<int> BulkInsertCoreAsync<T>(DbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints, CancellationToken cancellationToken) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif

        IList<T> entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0)
            return new ValueTask<int>(0);

        BulkEntityValidator.ThrowIfAnyNull(entityList, nameof(entities));

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        var bulkParameters = new BulkOperationParameters("BulkInsert", typeof(T), entityList.Count);

        // AUD-R26: the whole operation is reported once, not once per statement - a 100,000-row
        // BulkInsert is one logical write, and firing the pipeline per row would both swamp an
        // auditor and cost more than the bulk path saves. The body below is unchanged; it lives in
        // a local function so the transaction, FK-toggle and rollback logic is captured rather than
        // re-threaded through a new signature.

        return WriteInterception.ExecuteAsync(
            cached.InsertSql,
            bulkParameters,
            connection,
            options.CommandType,
            Body,
            cancellationToken);

        async ValueTask<int> Body()
        {
            WriteInterception.Log(cached.InsertSql, bulkParameters);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

            // Check if native bulk copy should be used. This must run before the SupportsForeignKeyToggle
            // guard below: the native path honors ignoreConstraints itself via BulkCopyOptions.CheckConstraints
            // and never touches the session-level FK-toggle pragma, so a dialect that supports native bulk
            // copy but not session-level toggling (e.g. SQL Server) must still be able to reach it.
            if (BulkCopyConfiguration.EnableNativeBulkCopy &&
                dialect.SupportsNativeBulkCopy &&
                entityList.Count >= BulkCopyConfiguration.MinimumRowsForNativeBulkCopy)
            {
                IBulkCopyProvider? bulkProvider = dialect.CreateBulkCopyProvider();
                if (bulkProvider != null && bulkProvider.IsSupported)
                {
                    return await BulkInsertNativeCoreAsync(connection, entityList, cached, bulkProvider, options, ignoreConstraints, cancellationToken).ConfigureAwait(false);
                }
            }

            if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
                throw new NotSupportedException(
                    $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                    "Use BulkInsertAsync instead, or disable constraints manually before calling this method.");

            ForeignKeyToggleCoordinator.ValidateTransactionCompatibility(ignoreConstraints, dialect, options.Transaction, connection.GetType().Name);
            bool requiresAutocommit = ForeignKeyToggleCoordinator.RequiresPreTransactionToggle(ignoreConstraints, dialect);

            bool wasClosed = connection.State == ConnectionState.Closed;
            DbTransaction? transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);
            bool ownTransaction = transaction is null;

            try
            {
                if (wasClosed)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                if (ignoreConstraints && requiresAutocommit)
                    await ForeignKeyToggleCoordinator.DisableAsync(connection, dialect, null, cancellationToken).ConfigureAwait(false);

                if (ownTransaction)
                {
    #if NET8_0_OR_GREATER
                    transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    #else
                    transaction = connection.BeginTransaction();
    #endif
                }

                if (ignoreConstraints && !requiresAutocommit)
                    await ForeignKeyToggleCoordinator.DisableAsync(connection, dialect, transaction, cancellationToken).ConfigureAwait(false);

                int totalInserted = 0;

                try
                {
                    Action<IDataParameterCollection, T>? valueSetter = WriteParameterCache<T>.InsertValueSetter;
                    if (valueSetter == null)
                        throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

                    // Multi-row INSERT reduces round-trips for network databases, but hurts
                    // in-process providers like SQLite where parameter object overhead exceeds savings.
                    if (dialect.SupportsMultiRowInsert && entityList.Count > 1 && !IsSqliteDialect(dialect))
                    {
                        totalInserted = await BulkInsertMultiRowAsync(connection, entityList, cached, dialect, transaction, options, valueSetter, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        totalInserted = await BulkInsertLoopAsync(connection, entityList, cached, transaction, options, valueSetter, cancellationToken).ConfigureAwait(false);
                    }

                    if (ignoreConstraints && !requiresAutocommit)
                        await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, transaction, cancellationToken).ConfigureAwait(false);

                    if (ownTransaction)
                    {
    #if NET8_0_OR_GREATER
                        await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
    #else
                        transaction!.Commit();
    #endif
                    }

                    if (ignoreConstraints && requiresAutocommit)
                        await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, null, cancellationToken).ConfigureAwait(false);

                    return totalInserted;
                }
                catch
                {
                    if (ignoreConstraints && !requiresAutocommit)
                    {
                        try
                        {
                            await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, transaction, CancellationToken.None).ConfigureAwait(false);
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

                    if (ignoreConstraints && requiresAutocommit)
                    {
                        try
                        {
                            await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, null, CancellationToken.None).ConfigureAwait(false);
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
                    await Task.Run(() => connection.Close()).ConfigureAwait(false);
    #endif
                }
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
        IReadOnlyList<ColumnMetadata> insertableColumns = ColumnMetadataHelper.GetInsertableColumns(cached.Metadata);
        int colCount = insertableColumns.Count;
        if (colCount == 0) return 0;

        // Compute optimal batch size respecting provider parameter limits
        int maxBatchSize = Math.Min((dialect.MaxParametersPerStatement - 1) / colCount, 1000);
        if (maxBatchSize < 1) maxBatchSize = 1;

        // Use cached compiled property getters for multi-row binding
        Func<T, object?>[] getters = MultiRowInsertCache.GetOrBuildGetters<T>(cached.Metadata);

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
            DbCommand command = connection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = connection.CreateCommand();
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
                    DbParameter p = command.CreateParameter();
                    // Parameter name matches SQL generated in MultiRowInsertCache.Build()
                    p.ParameterName = insertableColumns[c].ColumnName + "_" + row;
                    p.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(getters[c](entity), insertableColumns[c].Property) ?? DBNull.Value;
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
        // One command per entity, so - unlike the MultiRow/Native paths - per-row identity
        // retrieval is feasible here: use InsertCommandText (INSERT + identity-retrieval SQL)
        // and ExecuteScalarAsync when the entity has an identity key, mirroring InsertCoreAsync's
        // behavior, so BulkInsertAsync populates entity IDs the same way single-row InsertAsync does.
        Action<T, long>? idSetter = cached.HasIdentityKey ? WriteParameterCache<T>.IdSetter : null;

#if NET8_0_OR_GREATER
        DbCommand command = connection.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#else
        using DbCommand command = connection.CreateCommand();
#endif
        command.Transaction = transaction;
        command.CommandText = idSetter is not null ? cached.InsertCommandText : cached.InsertSql;

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        PrepareInsertParameters(command, cached.Metadata);

        // Set first entity values before Prepare() so providers can infer parameter types.
        // Prepare() is a best-effort optimization; some providers (e.g. SQL Server on .NET Framework)
        // require explicit DbType on all parameters, which we can't guarantee here.
        DbParameterCollection pCollection = command.Parameters;
        valueSetter(pCollection, entityList[0]);
        try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }

        cancellationToken.ThrowIfCancellationRequested();
        int totalInserted = await ExecuteInsertAndSetIdAsync(command, entityList[0], idSetter, cancellationToken).ConfigureAwait(false);

        for (int i = 1; i < entityList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            valueSetter(pCollection, entityList[i]);
            totalInserted += await ExecuteInsertAndSetIdAsync(command, entityList[i], idSetter, cancellationToken).ConfigureAwait(false);
        }

        return totalInserted;
    }

    internal static async ValueTask<int> ExecuteInsertAndSetIdAsync<T>(DbCommand command, T entity, Action<T, long>? idSetter, CancellationToken cancellationToken)
    {
        if (idSetter is null)
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        // AUD-R26: ScalarConverter pins InvariantCulture; Convert.ToInt64 did not, so a provider
        // that returns the generated key as a string parsed under the host locale. Restructured
        // rather than suppressed with `!` - the compiler cannot see non-nullness through a bool
        // local, and `!` is how the two null-guard defects in this round were introduced.
        if (result is null or DBNull)
            return 0;

        idSetter(entity, ScalarConverter<long>.Convert(result));
        return 1;
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
        DbTransaction? transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);
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
                TableLock = TableLockOption.Default
            };

            int totalInserted = 0;

            try
            {
                // Create EntityDataReader for streaming entity-to-datareader conversion
                EntityMetadata metadata = cached.Metadata;
                using var reader = new EntityDataReader<T>(entityList, metadata);

                // Execute native bulk copy
                int providerResult = await bulkProvider.CopyToServerAsync(connection, cached.Metadata.SchemaName, cached.Metadata.TableName, reader, bulkOptions, cancellationToken).ConfigureAwait(false);

                // Some providers (e.g. SqlBulkCopy) return -1; use entityList.Count as fallback
                totalInserted = providerResult >= 0 ? providerResult : entityList.Count;

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
                if (ownTransaction && transaction is not null)
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
#else
                        transaction.Rollback();
#endif
                    }
                    catch { /* Best effort */ }
                }
                throw;
            }
        }
        finally
        {
            if (ownTransaction && transaction is not null)
            {
#if NET8_0_OR_GREATER
                await transaction.DisposeAsync().ConfigureAwait(false);
#else
                transaction.Dispose();
#endif
            }

            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }
}
