using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously deletes multiple entities from the database in a single transaction.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public static Task<int> BulkDeleteAsync<T>(this DbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        return BulkDeleteAsync(connection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes multiple entities from the database in a single transaction with command options.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public static Task<int> BulkDeleteAsync<T>(this DbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return BulkDeleteCoreAsync(connection, entities, options, ignoreConstraints: false, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes multiple entities from the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// cleanup operations, or scenarios where you explicitly don't need FK validation.
    /// This can leave orphaned records in child tables.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static Task<int> BulkDeleteIgnoreConstraintsAsync<T>(this DbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        return BulkDeleteIgnoreConstraintsAsync(connection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes multiple entities from the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// cleanup operations, or scenarios where you explicitly don't need FK validation.
    /// This can leave orphaned records in child tables.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static Task<int> BulkDeleteIgnoreConstraintsAsync<T>(this DbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return BulkDeleteCoreAsync(connection, entities, options, ignoreConstraints: true, cancellationToken);
    }

    private static async Task<int> BulkDeleteCoreAsync<T>(DbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints, CancellationToken cancellationToken) where T : class, new()
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

        if (!cached.HasPrimaryKey)
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete SQL could not be generated.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkDeleteAsync instead, or disable constraints manually before calling this method.");

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

            int totalDeleted = 0;

            try
            {
#if NET8_0_OR_GREATER
                await using var command = connection.CreateCommand();
#else
                using var command = connection.CreateCommand();
#endif
                command.Transaction = transaction;
                command.CommandText = cached.DeleteSql;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                PrepareDeleteParameters(command, cached.Metadata);

                foreach (var entity in entityList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SetDeleteParameterValues(command, entity, cached.Metadata);
                    totalDeleted += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

                return totalDeleted;
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
