using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty;

public static partial class Jaunty
{
    internal static int UpdateCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
        Action<IDbCommand, T> binder = WriteParameterCache<T>.UpdateBinder
            ?? throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found or no columns to update.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.UpdateSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            binder(command, entity);

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<int> UpdateCoreAsync<T>(DbConnection dbConnection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        Action<IDbCommand, T> binder = WriteParameterCache<T>.UpdateBinder
            ?? throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found or no columns to update.");

        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = cached.UpdateSql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            binder(command, entity);

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await dbConnection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }
}
