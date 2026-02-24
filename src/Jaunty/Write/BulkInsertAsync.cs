using System.Data;
using System.Data.Common;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

using Jaunty.Core;
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
                // Provider-specific fast paths removed from core to ensure 100% NativeAOT/Zero-Reflection compatibility.
                // Use explicit provider extensions if specialized bulk operations are needed.

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

                var valueSetter = WriteParameterCache<T>.InsertValueSetter;
                if (valueSetter == null)
                {
                    throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure the class is source-generated or reflection extension is loaded.");
                }

                var pCollection = command.Parameters;

                foreach (var entity in entityList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    valueSetter(pCollection, entity);
                    totalInserted += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
}
