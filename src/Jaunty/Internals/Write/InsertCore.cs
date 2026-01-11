using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Interfaces;
using Jaunty.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    private static long InsertCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();

            // For identity columns, append the last insert ID SQL
            if (cached.HasIdentityKey)
            {
                command.CommandText = cached.InsertSql + "; " + cached.LastInsertIdSql;
            }
            else
            {
                command.CommandText = cached.InsertSql;
            }

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters from entity properties
            BindInsertParameters(command, entity, cached.Metadata);

            if (cached.HasIdentityKey)
            {
                // Execute and get identity
                object? result = command.ExecuteScalar();
                long generatedId = ConvertToLong(result);

                // Populate IEntity.Id if applicable
                PopulateEntityId(entity, generatedId, cached.Metadata);

                return generatedId;
            }
            else
            {
                // Non-identity insert - just execute
                int affectedRows = command.ExecuteNonQuery();
                return affectedRows;
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static async Task<long> InsertCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = connection.CreateCommand();
#else
            using DbCommand command = connection.CreateCommand();
#endif

            command.CommandText = cached.HasIdentityKey ? cached.InsertSql + "; " + cached.LastInsertIdSql : cached.InsertSql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindInsertParameters(command, entity, cached.Metadata);

            if (cached.HasIdentityKey)
            {
                object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                long generatedId = ConvertToLong(result);

                PopulateEntityId(entity, generatedId, cached.Metadata);

                return generatedId;
            }
            else
            {
                int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return affectedRows;
            }
        }
        finally
        {
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

    private static void BindInsertParameters<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : class
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

    private static void PopulateEntityId<T>(T entity, long generatedId, EntityMetadata metadata) where T : class
    {
        // Check for IEntity interface
        if (entity is IEntity entityWithId)
        {
            entityWithId.Id = generatedId;
            return;
        }

        // Check for IEntity<T> interface
        Type entityType = typeof(T);
        Type[] interfaces = entityType.GetInterfaces();

        for (int i = 0; i < interfaces.Length; i++)
        {
            Type iface = interfaces[i];
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEntity<>))
            {
                Type idType = iface.GetGenericArguments()[0];

                // Find the Id property from the interface
                System.Reflection.PropertyInfo? idProperty = entityType.GetProperty("Id");
                if (idProperty is not null && idProperty.CanWrite)
                {
                    object convertedId = Convert.ChangeType(generatedId, idType);
                    idProperty.SetValue(entity, convertedId);
                }
                return;
            }
        }

        // Not an IEntity - no auto-population needed
    }

    private static long ConvertToLong(object? value)
    {
        if (value is null || value == DBNull.Value)
            return 0;

        return Convert.ToInt64(value);
    }
}
