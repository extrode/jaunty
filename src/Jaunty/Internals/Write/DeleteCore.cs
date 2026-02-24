using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Interfaces;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty;

public static partial class Jaunty
{
    internal static int DeleteByEntityCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.Transaction = options.Transaction;
            command.CommandText = cached.DeleteSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            var binder = WriteParameterCache<T>.DeleteBinder;
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

    internal static async ValueTask<int> DeleteByEntityCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

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
            command.CommandText = cached.DeleteSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            var binder = WriteParameterCache<T>.DeleteBinder;
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

    internal static int DeleteByIdSimpleCore<T>(IDbConnection connection, object id, CommandOptions options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.Transaction = options.Transaction;
            command.CommandText = cached.DeleteByIdSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<int> DeleteByIdSimpleCoreAsync<T>(DbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

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
            command.CommandText = cached.DeleteByIdSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            DbParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

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

    internal static int DeleteByIdCore<T, TId>(IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.Transaction = options.Transaction;
            command.CommandText = cached.DeleteByIdSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<int> DeleteByIdCoreAsync<T, TId>(DbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

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
            command.CommandText = cached.DeleteByIdSql;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            DbParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

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
