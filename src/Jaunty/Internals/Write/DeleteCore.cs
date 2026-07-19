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
        Action<IDbCommand, T> binder = WriteParameterCache<T>.DeleteBinder
            ?? throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.DeleteSql;

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

    internal static async ValueTask<int> DeleteByEntityCoreAsync<T>(DbConnection dbConnection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        Action<IDbCommand, T> binder = WriteParameterCache<T>.DeleteBinder
            ?? throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

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
            command.CommandText = cached.DeleteSql;

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

    internal static int DeleteByIdSimpleCore<T>(IDbConnection connection, object id, CommandOptions options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.DeleteByIdSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<int> DeleteByIdSimpleCoreAsync<T>(DbConnection dbConnection, object id, CommandOptions options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

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
            command.CommandText = cached.DeleteByIdSql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

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

    internal static int DeleteByIdCore<T, TId>(IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.DeleteByIdSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static async ValueTask<int> DeleteByIdCoreAsync<T, TId>(DbConnection dbConnection, object id, CommandOptions options, CancellationToken cancellationToken) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

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
            command.CommandText = cached.DeleteByIdSql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

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

    private static void AddPrimaryKeyParameter(IDbCommand command, CachedCrudSql cached, object id)
    {
        ColumnMetadata primaryKey = cached.Metadata.PrimaryKeys[0];
        IDbDataParameter param = command.CreateParameter();
        param.ParameterName = "@" + primaryKey.ColumnName;
        param.Value = id ?? DBNull.Value;
        command.Parameters.Add(param);
    }

    private static void AddPrimaryKeyParameter(DbCommand command, CachedCrudSql cached, object id)
    {
        ColumnMetadata primaryKey = cached.Metadata.PrimaryKeys[0];
        DbParameter param = command.CreateParameter();
        param.ParameterName = "@" + primaryKey.ColumnName;
        param.Value = id ?? DBNull.Value;
        command.Parameters.Add(param);
    }
}
