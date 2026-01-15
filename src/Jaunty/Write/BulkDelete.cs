using System.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Deletes multiple entities from the database in a single transaction.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <returns>The number of rows deleted.</returns>
    public static int BulkDelete<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
        return BulkDelete(connection, entities, default);
    }

    /// <summary>
    /// Deletes multiple entities from the database in a single transaction with command options.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows deleted.</returns>
    public static int BulkDelete<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return BulkDeleteCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Deletes multiple entities from the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// cleanup operations, or scenarios where you explicitly don't need FK validation.
    /// This can leave orphaned records in child tables.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static int BulkDeleteIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
        return BulkDeleteIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Deletes multiple entities from the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// cleanup operations, or scenarios where you explicitly don't need FK validation.
    /// This can leave orphaned records in child tables.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static int BulkDeleteIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return BulkDeleteCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkDeleteCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : class, new()
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
                "Use BulkDelete instead, or disable constraints manually before calling this method.");

        bool wasClosed = connection.State == ConnectionState.Closed;
        IDbTransaction? transaction = options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                connection.Open();

            if (ownTransaction)
                transaction = connection.BeginTransaction();

            if (ignoreConstraints)
            {
                using var fkOffCmd = connection.CreateCommand();
                fkOffCmd.Transaction = transaction;
                fkOffCmd.CommandText = dialect.GetDisableForeignKeyChecksSql()!;
                fkOffCmd.ExecuteNonQuery();
            }

            int totalDeleted = 0;

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = cached.DeleteSql;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                PrepareDeleteParameters(command, cached.Metadata);

                foreach (var entity in entityList)
                {
                    SetDeleteParameterValues(command, entity, cached.Metadata);
                    totalDeleted += command.ExecuteNonQuery();
                }

                if (ignoreConstraints)
                {
                    using var fkOnCmd = connection.CreateCommand();
                    fkOnCmd.Transaction = transaction;
                    fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                    fkOnCmd.ExecuteNonQuery();
                }

                if (ownTransaction)
                    transaction!.Commit();

                return totalDeleted;
            }
            catch
            {
                if (ignoreConstraints)
                {
                    try
                    {
                        using var fkOnCmd = connection.CreateCommand();
                        fkOnCmd.Transaction = transaction;
                        fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                        fkOnCmd.ExecuteNonQuery();
                    }
                    catch { /* Best effort */ }
                }

                if (ownTransaction)
                    transaction?.Rollback();

                throw;
            }
        }
        finally
        {
            if (ownTransaction)
                transaction?.Dispose();

            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static void PrepareDeleteParameters(IDbCommand command, EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.Property.Name;
            command.Parameters.Add(param);
        }
    }

    private static void SetDeleteParameterValues<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : class
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            var param = (IDbDataParameter)command.Parameters[i]!;
            param.Value = key.Property.GetValue(entity) ?? DBNull.Value;
        }
    }
}
