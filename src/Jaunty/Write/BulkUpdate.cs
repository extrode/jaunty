using System.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Updates multiple entities in the database in a single transaction.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to update.</param>
    /// <returns>The number of rows updated.</returns>
    public static int BulkUpdate<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
        return BulkUpdate(connection, entities, default);
    }

    /// <summary>
    /// Updates multiple entities in the database in a single transaction with command options.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to update.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows updated.</returns>
    public static int BulkUpdate<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return BulkUpdateCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Updates multiple entities in the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// data corrections, or scenarios where you explicitly don't need FK validation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to update.</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static int BulkUpdateIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
        return BulkUpdateIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Updates multiple entities in the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// data corrections, or scenarios where you explicitly don't need FK validation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to update.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static int BulkUpdateIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return BulkUpdateCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkUpdateCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : class, new()
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
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No updateable columns found.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkUpdate instead, or disable constraints manually before calling this method.");

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

            int totalUpdated = 0;

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = cached.UpdateSql;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                PrepareUpdateParameters(command, cached.Metadata);

                foreach (var entity in entityList)
                {
                    SetUpdateParameterValues(command, entity, cached.Metadata);
                    totalUpdated += command.ExecuteNonQuery();
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

                return totalUpdated;
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

    private static void PrepareUpdateParameters(IDbCommand command, EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> allColumns = metadata.Columns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        // SET clause parameters
        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];
            if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed)
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.Property.Name;
            command.Parameters.Add(param);
        }

        // WHERE clause parameters (primary keys)
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.Property.Name;
            command.Parameters.Add(param);
        }
    }

    private static void SetUpdateParameterValues<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : class
    {
        IReadOnlyList<ColumnMetadata> allColumns = metadata.Columns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        int paramIndex = 0;

        // SET clause values
        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];
            if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed)
                continue;

            var param = (IDbDataParameter)command.Parameters[paramIndex++]!;
            param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
        }

        // WHERE clause values (primary keys)
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            var param = (IDbDataParameter)command.Parameters[paramIndex++]!;
            param.Value = key.Property.GetValue(entity) ?? DBNull.Value;
        }
    }
}
