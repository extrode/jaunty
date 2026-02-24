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
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.Transaction = options.Transaction;
            command.CommandText = ComposeInsertCommandText(cached);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters using our decision tree
            var binder = WriteParameterCache<T>.InsertBinder;
            if (binder is not null)
            {
                binder(command, entity);
            }
            else
            {
                throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");
            }

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            if (cached.HasIdentityKey)
            {
                var result = command.ExecuteScalar();
                long id = result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
                
                if (id > 0)
                {
                    WriteParameterCache<T>.IdSetter?.Invoke(entity, id);
                }
                
                return id;
            }

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<long> InsertCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

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
            command.CommandText = ComposeInsertCommandText(cached);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters
            var binder = WriteParameterCache<T>.InsertBinder;
            if (binder != null)
            {
                binder(command, entity);
            }
            else
            {
                throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'.");
            }

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            if (cached.HasIdentityKey)
            {
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                long id = result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
                
                if (id > 0)
                {
                    WriteParameterCache<T>.IdSetter?.Invoke(entity, id);
                }
                
                return id;
            }

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

    private static string ComposeInsertCommandText(CachedCrudSql cached)
    {
        if (!cached.HasIdentityKey)
            return cached.InsertSql;

        var lastInsertIdSql = cached.LastInsertIdSql?.TrimStart();
        if (!string.IsNullOrEmpty(lastInsertIdSql) &&
            lastInsertIdSql.StartsWith("RETURNING", StringComparison.OrdinalIgnoreCase))
        {
            return $"{cached.InsertSql} {cached.LastInsertIdSql}";
        }

        return $"{cached.InsertSql}; {cached.LastInsertIdSql}";
    }
}
