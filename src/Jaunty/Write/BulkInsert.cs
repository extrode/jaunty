using System.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Inserts multiple entities into the database in a single transaction.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>The number of rows inserted.</returns>
    public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
        return BulkInsert(connection, entities, default);
    }

    /// <summary>
    /// Inserts multiple entities into the database in a single transaction with command options.
    /// Foreign key constraints are enforced.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows inserted.</returns>
    public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return BulkInsertCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Inserts multiple entities into the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// data imports, or scenarios where you explicitly don't need FK validation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
        return BulkInsertIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Inserts multiple entities into the database, bypassing foreign key constraint checks.
    /// WARNING: This temporarily disables referential integrity. Use only for migrations,
    /// data imports, or scenarios where you explicitly don't need FK validation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="NotSupportedException">Thrown if the database doesn't support FK toggling (e.g., SQL Server).</exception>
    public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return BulkInsertCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkInsertCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif

        // Materialize to list to avoid multiple enumeration
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
                "Use BulkInsert instead, or disable constraints manually before calling this method.");

        bool wasClosed = connection.State == ConnectionState.Closed;
        IDbTransaction? transaction = options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                connection.Open();

            // Create our own transaction if one wasn't provided
            if (ownTransaction)
                transaction = connection.BeginTransaction();

            // Disable FK checks if requested
            if (ignoreConstraints)
            {
                using var fkOffCmd = connection.CreateCommand();
                fkOffCmd.Transaction = transaction;
                fkOffCmd.CommandText = dialect.GetDisableForeignKeyChecksSql()!;
                fkOffCmd.ExecuteNonQuery();
            }

            int totalInserted = 0;

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = cached.InsertSql;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                // Prepare parameters once, reuse for each entity
                PrepareInsertParameters(command, cached.Metadata);

                foreach (var entity in entityList)
                {
                    SetInsertParameterValues(command, entity, cached.Metadata);
                    totalInserted += command.ExecuteNonQuery();
                }

                // Re-enable FK checks before commit
                if (ignoreConstraints)
                {
                    using var fkOnCmd = connection.CreateCommand();
                    fkOnCmd.Transaction = transaction;
                    fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                    fkOnCmd.ExecuteNonQuery();
                }

                if (ownTransaction)
                    transaction!.Commit();

                return totalInserted;
            }
            catch
            {
                // Re-enable FK checks even on error
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

    private static void PrepareInsertParameters(IDbCommand command, EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.NonIdentityColumns;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            if (col.IsComputed)
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.Property.Name;
            command.Parameters.Add(param);
        }
    }

    private static void SetInsertParameterValues<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : class
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.NonIdentityColumns;
        int paramIndex = 0;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            if (col.IsComputed)
                continue;

            var param = (IDbDataParameter)command.Parameters[paramIndex++]!;
            param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
        }
    }
}
