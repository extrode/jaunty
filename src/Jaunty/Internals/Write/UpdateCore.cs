using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty;

public static partial class Jaunty
{
    internal static int UpdateCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found or no columns to update.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.Transaction = options.Transaction;
            command.CommandText = cached.UpdateSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters
            var binder = WriteParameterCache<T>.UpdateBinder;
            if (binder != null)
            {
                binder(command, entity);
            }
            else
            {
                throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'.");
            }

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<int> UpdateCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found or no columns to update.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using var command = connection.CreateCommand();
#else
            using var command = connection.CreateCommand();
#endif
            command.Transaction = options.Transaction as DbTransaction;
            command.CommandText = cached.UpdateSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters
            var binder = WriteParameterCache<T>.UpdateBinder;
            if (binder != null)
            {
                binder(command, entity);
            }
            else
            {
                throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'.");
            }

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

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
}
