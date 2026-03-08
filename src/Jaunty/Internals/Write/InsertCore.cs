using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty;

public static partial class Jaunty
{
    internal static long InsertCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
        Action<IDbCommand, T> binder = WriteParameterCache<T>.InsertBinder
            ?? throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.InsertCommandText;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            binder(command, entity);

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            if (cached.HasIdentityKey)
            {
                var result = command.ExecuteScalar();
                long id = result is null or DBNull ? 0 : Convert.ToInt64(result);

                if (id > 0)
                    WriteParameterCache<T>.IdSetter?.Invoke(entity, id);

                return id;
            }

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<long> InsertCoreAsync<T>(DbConnection dbConnection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        Action<IDbCommand, T> binder = WriteParameterCache<T>.InsertBinder
            ?? throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = dbConnection.CreateCommand();
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = cached.InsertCommandText;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            binder(command, entity);

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            if (cached.HasIdentityKey)
            {
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                long id = result is null or DBNull ? 0 : Convert.ToInt64(result);

                if (id > 0)
                    WriteParameterCache<T>.IdSetter?.Invoke(entity, id);

                return id;
            }

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await dbConnection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => dbConnection.Close(), cancellationToken).ConfigureAwait(false);
#endif
            }
        }
    }

}