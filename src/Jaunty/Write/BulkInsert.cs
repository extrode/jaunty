using System.Data;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Inserts multiple entities into the database in a single transaction.
    /// </summary>
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
        IList<T> entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0)
            return 0;

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling.");

        // Check if native bulk copy should be used
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

        bool wasClosed = connection.State == ConnectionState.Closed;
        IDbTransaction? transaction = options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed) connection.Open();
            if (ownTransaction) transaction = connection.BeginTransaction();

            if (ignoreConstraints)
            {
                using IDbCommand fkOffCmd = connection.CreateCommand();
                fkOffCmd.Transaction = transaction;
                fkOffCmd.CommandText = dialect.GetDisableForeignKeyChecksSql()!;
                fkOffCmd.ExecuteNonQuery();
            }

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

                if (ignoreConstraints)
                {
                    using IDbCommand fkOnCmd = connection.CreateCommand();
                    fkOnCmd.Transaction = transaction;
                    fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                    fkOnCmd.ExecuteNonQuery();
                }

                if (ownTransaction) transaction!.Commit();

                return totalInserted;
            }
            catch
            {
                if (ignoreConstraints)
                {
                    try
                    {
                        using IDbCommand fkOnCmd = connection.CreateCommand();
                        fkOnCmd.Transaction = transaction;
                        fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                        fkOnCmd.ExecuteNonQuery();
                    }
                    catch { }
                }

                if (ownTransaction) transaction?.Rollback();
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
        IDbTransaction? transaction = options.Transaction;
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
                int providerResult = bulkProvider.CopyToServer(connection, cached.Metadata.TableName, reader, bulkOptions);

                // Some providers (e.g. SqlBulkCopy) return -1; use entityList.Count as fallback
                totalInserted = providerResult >= 0 ? providerResult : entityList.Count;

                if (ownTransaction) transaction!.Commit();

                return totalInserted;
            }
            catch
            {
                if (ownTransaction) transaction?.Rollback();
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
                    p.Value = getters[c](entity) ?? DBNull.Value;
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
        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = cached.InsertSql;

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        PrepareInsertParameters(command, cached.Metadata);

        // Set first entity values before Prepare() so providers can infer parameter types.
        // Prepare() is a best-effort optimization; some providers (e.g. SQL Server on .NET Framework)
        // require explicit DbType on all parameters, which we can't guarantee here.
        IDataParameterCollection pCollection = command.Parameters;
        valueSetter(pCollection, entityList[0]);
        try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }

        int totalInserted = command.ExecuteNonQuery();

        for (int i = 1; i < entityList.Count; i++)
        {
            valueSetter(pCollection, entityList[i]);
            totalInserted += command.ExecuteNonQuery();
        }

        return totalInserted;
    }
    private static bool IsSqliteDialect(ISqlDialect dialect)
        => dialect is SQLiteDialect || dialect.GetType().Name.Contains("SQLite", StringComparison.OrdinalIgnoreCase);
}
