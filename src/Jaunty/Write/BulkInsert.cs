using System.Data;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Inserts multiple entities into the database in a single transaction.
    /// </summary>
    /// <remarks>
    /// Identity values are populated back onto entities only when the row count and provider
    /// combination routes through the loop-based insert path (one command per entity). The
    /// multi-row VALUES and native bulk-copy paths - used automatically for larger batches on
    /// providers that support them - do not populate identity values, since there is no
    /// provider-agnostic way to map a single "last inserted id" back to individual rows within a
    /// batched or native bulk statement. Callers that need populated IDs should use single-row
    /// <see cref="Insert{T}(IDbConnection, T)"/> in a loop, or query the inserted rows back afterward.
    /// </remarks>
    public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertCore(connection, entities, default, ignoreConstraints: false);
    }

    /// <summary>
    /// Inserts multiple entities into the database in a single transaction with command options.
    /// </summary>
    /// <remarks>
    /// See <see cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/> for the identity-population
    /// caveat: only the loop-based insert path populates entity IDs back.
    /// </remarks>
    public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Inserts multiple entities into the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See <see cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/> for the identity-population
    /// caveat: only the loop-based insert path populates entity IDs back.
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
    public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Inserts multiple entities into the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See <see cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/> for the identity-population
    /// caveat: only the loop-based insert path populates entity IDs back.
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
    public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkInsertCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : new()
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
            return 0;

        BulkEntityValidator.ThrowIfAnyNull(entityList, nameof(entities));

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

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
                return BulkInsertNativeCore(connection, entityList, cached, bulkProvider, options, ignoreConstraints);
            }
        }

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkInsert instead, or disable constraints manually before calling this method.");

        ForeignKeyToggleCoordinator.ValidateTransactionCompatibility(ignoreConstraints, dialect, options.Transaction, connection.GetType().Name);
        bool requiresAutocommit = ForeignKeyToggleCoordinator.RequiresPreTransactionToggle(ignoreConstraints, dialect);

        bool wasClosed = connection.State == ConnectionState.Closed;

        // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
        // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
        // IDbTransaction through it throws an opaque InvalidCastException. Validate via
        // AsyncTransactionValidator first (mirroring InsertCoreDirect) so an incompatible
        // transaction gets Jaunty's clear ArgumentException instead.
        IDbTransaction? transaction = connection is System.Data.Common.DbConnection
            ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
            : options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed) connection.Open();

            if (ignoreConstraints && requiresAutocommit)
                ForeignKeyToggleCoordinator.DisableSync(connection, dialect, null);

            if (ownTransaction) transaction = connection.BeginTransaction();

            if (ignoreConstraints && !requiresAutocommit)
                ForeignKeyToggleCoordinator.DisableSync(connection, dialect, transaction);

            int totalInserted = 0;

            try
            {
                Action<IDataParameterCollection, T>? valueSetter = WriteParameterCache<T>.InsertValueSetter;
                if (valueSetter == null)
                    throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

                // Multi-row INSERT reduces round-trips for network databases, but hurts
                // in-process providers like SQLite where parameter object overhead exceeds savings.
                // SQLite (incl. the Extensions.Reflection wrapper dialect) must take the
                // prepared-loop path: multi-row VALUES suffers from quadratic parameter
                // binding in Microsoft.Data.Sqlite, measured ~16x slower (PROD-120).
                if (dialect.SupportsMultiRowInsert && entityList.Count > 1 && !IsSqliteDialect(dialect))
                {
                    totalInserted = BulkInsertMultiRow(connection, entityList, cached, dialect, transaction, options, valueSetter);
                }
                else
                {
                    totalInserted = BulkInsertLoop(connection, entityList, cached, transaction, options, valueSetter);
                }

                if (ignoreConstraints && !requiresAutocommit)
                    ForeignKeyToggleCoordinator.EnableSync(connection, dialect, transaction);

                if (ownTransaction) transaction!.Commit();

                if (ignoreConstraints && requiresAutocommit)
                    ForeignKeyToggleCoordinator.EnableSync(connection, dialect, null);

                return totalInserted;
            }
            catch
            {
                if (ignoreConstraints && !requiresAutocommit)
                {
                    try { ForeignKeyToggleCoordinator.EnableSync(connection, dialect, transaction); }
                    catch { }
                }

                if (ownTransaction)
                {
                    try { transaction?.Rollback(); }
                    catch { /* Best effort - do not mask the original exception */ }
                }

                if (ignoreConstraints && requiresAutocommit)
                {
                    try { ForeignKeyToggleCoordinator.EnableSync(connection, dialect, null); }
                    catch { }
                }
                throw;
            }
        }
        finally
        {
            if (ownTransaction) transaction?.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    /// <summary>
    /// Native bulk copy path: uses database-specific bulk copy APIs (e.g., SqlBulkCopy, NpgsqlBinaryImporter).
    /// Provides 10-100x performance improvement for large datasets (100+ rows).
    /// </summary>
    private static int BulkInsertNativeCore<T>(IDbConnection connection, IList<T> entityList, CachedCrudSql cached, IBulkCopyProvider bulkProvider, CommandOptions options, bool ignoreConstraints) where T : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
        // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
        // IDbTransaction through it throws an opaque InvalidCastException. Validate via
        // AsyncTransactionValidator first (mirroring InsertCoreDirect) so an incompatible
        // transaction gets Jaunty's clear ArgumentException instead.
        IDbTransaction? transaction = connection is System.Data.Common.DbConnection
            ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
            : options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed) connection.Open();
            if (ownTransaction) transaction = connection.BeginTransaction();

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
                using var reader = new EntityDataReader<T>(entityList, cached.Metadata);

                // Execute native bulk copy
                int providerResult = bulkProvider.CopyToServer(connection, cached.Metadata.SchemaName, cached.Metadata.TableName, reader, bulkOptions);

                // Some providers (e.g. SqlBulkCopy) return -1; use entityList.Count as fallback
                totalInserted = providerResult >= 0 ? providerResult : entityList.Count;

                if (ownTransaction) transaction!.Commit();

                return totalInserted;
            }
            catch
            {
                if (ownTransaction)
                {
                    try { transaction?.Rollback(); }
                    catch { /* Best effort - do not mask the original exception */ }
                }
                throw;
            }
        }
        finally
        {
            if (ownTransaction) transaction?.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    /// <summary>
    /// Multi-row INSERT path: batches entities into INSERT ... VALUES (...), (...), ... statements.
    /// Dramatically reduces round-trips compared to individual INSERTs.
    /// </summary>
    private static int BulkInsertMultiRow<T>(IDbConnection connection, IList<T> entityList, CachedCrudSql cached, ISqlDialect dialect, IDbTransaction? transaction, CommandOptions options, Action<IDataParameterCollection, T> valueSetter) where T : new()
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
            int batchSize = Math.Min(maxBatchSize, entityCount - offset);

            string sql = MultiRowInsertCache.GetOrBuild(
                typeof(T), connection.GetType(), batchSize, cached.Metadata, dialect);

            using IDbCommand command = connection.CreateCommand();
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
                    IDbDataParameter p = command.CreateParameter();
                    // Parameter name matches SQL generated in MultiRowInsertCache.Build()
                    p.ParameterName = insertableColumns[c].ColumnName + "_" + row;
                    p.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(getters[c](entity), insertableColumns[c].Property) ?? DBNull.Value;
                    command.Parameters.Add(p);
                }
            }

            totalInserted += command.ExecuteNonQuery();
            offset += batchSize;
        }

        return totalInserted;
    }

    /// <summary>
    /// Loop-based INSERT fallback: executes individual INSERT statements.
    /// Used when multi-row INSERT is not supported or for single entities.
    /// </summary>
    private static int BulkInsertLoop<T>(IDbConnection connection, IList<T> entityList, CachedCrudSql cached, IDbTransaction? transaction, CommandOptions options, Action<IDataParameterCollection, T> valueSetter) where T : new()
    {
        // One command per entity, so - unlike the MultiRow/Native paths - per-row identity
        // retrieval is feasible here: use InsertCommandText (INSERT + identity-retrieval SQL)
        // and ExecuteScalar when the entity has an identity key, mirroring InsertCore's behavior,
        // so BulkInsert populates entity IDs the same way single-row Insert does.
        Action<T, long>? idSetter = cached.HasIdentityKey ? WriteParameterCache<T>.IdSetter : null;

        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = idSetter is not null ? cached.InsertCommandText : cached.InsertSql;

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        PrepareInsertParameters(command, cached.Metadata);

        // Set first entity values before Prepare() so providers can infer parameter types.
        // Prepare() is a best-effort optimization; some providers (e.g. SQL Server on .NET Framework)
        // require explicit DbType on all parameters, which we can't guarantee here.
        IDataParameterCollection pCollection = command.Parameters;
        valueSetter(pCollection, entityList[0]);
        try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }

        int totalInserted = ExecuteInsertAndSetId(command, entityList[0], idSetter);

        for (int i = 1; i < entityList.Count; i++)
        {
            valueSetter(pCollection, entityList[i]);
            totalInserted += ExecuteInsertAndSetId(command, entityList[i], idSetter);
        }

        return totalInserted;
    }

    internal static int ExecuteInsertAndSetId<T>(IDbCommand command, T entity, Action<T, long>? idSetter)
    {
        if (idSetter is null)
            return command.ExecuteNonQuery();

        object? result = command.ExecuteScalar();
        long id = result is null or DBNull ? 0 : Convert.ToInt64(result);
        if (id > 0)
            idSetter(entity, id);

        return id > 0 ? 1 : 0;
    }
    private static bool IsSqliteDialect(ISqlDialect dialect)
        => dialect is SQLiteDialect || dialect.GetType().Name.Contains("SQLite", StringComparison.OrdinalIgnoreCase);
}
