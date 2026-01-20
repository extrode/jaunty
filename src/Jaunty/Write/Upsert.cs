using System.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Performs an upsert (INSERT or UPDATE if exists) operation on an entity.
    /// If the entity exists (based on primary key), it updates; otherwise, it inserts.
    /// Returns the number of affected rows (1 for insert, 1 for update).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to upsert.</param>
    /// <returns>Number of affected rows.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity has no primary key or the dialect doesn't support upsert.</exception>
    public static int Upsert<T>(this IDbConnection connection, T entity) where T : class, new()
    {
        return UpsertCore(connection, entity, default);
    }

    /// <summary>
    /// Performs an upsert (INSERT or UPDATE if exists) operation on an entity with command options.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>Number of affected rows.</returns>
    public static int Upsert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
        return UpsertCore(connection, entity, options);
    }

    private static int UpsertCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.SupportsUpsert)
            throw new InvalidOperationException($"The database dialect does not support upsert operations.");

        if (string.IsNullOrEmpty(cached.UpsertSql))
            throw new InvalidOperationException($"Cannot upsert entity of type '{typeof(T).Name}': No primary key found or no upsertable columns.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.UpsertSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters from entity properties (all non-identity, non-computed columns)
            BindUpsertParameters(command, entity, cached.Metadata);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static void BindUpsertParameters<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : class
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.NonIdentityColumns;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];

            // Skip computed columns
            if (col.IsComputed)
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.Property.Name;
            param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }
}
