using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    private static int UpdateCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasPrimaryKey)
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No updateable columns found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.UpdateSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindUpdateParameters(command, entity, cached.Metadata);
            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static async Task<int> UpdateCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasPrimaryKey)
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No updateable columns found.");

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
            command.CommandText = cached.UpdateSql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindUpdateParameters(command, entity, cached.Metadata);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

    private static void BindUpdateParameters<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : class
    {
        IReadOnlyList<ColumnMetadata> allColumns = metadata.Columns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        // Bind SET clause parameters (non-key, non-identity, non-computed)
        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];

            if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed)
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.Property.Name;
            param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        // Bind WHERE clause parameters (primary keys)
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.Property.Name;
            param.Value = key.Property.GetValue(entity) ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }
}
