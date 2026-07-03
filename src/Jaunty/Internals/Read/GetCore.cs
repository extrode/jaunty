using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Read;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;
using Jaunty.Interfaces;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    internal static T? GetByIdSimpleCore<T>(IDbConnection connection, object id, CommandOptions<T> options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException("Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.SelectByIdSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            if (connection is DbConnection dbConnection)
            {
                return ExecuteReaderForGet(dbConnection, command, options);
            }

            return ExecuteReaderForGetNonAsync(connection, command, options);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    internal static T? GetByIdTypedCore<T, TId>(IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException("Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.SelectByIdSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            if (connection is DbConnection dbConnection)
            {
                return ExecuteReaderForGet(dbConnection, command, options);
            }

            return ExecuteReaderForGetNonAsync(connection, command, options);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    private static T? ExecuteReaderForGet<T>(DbConnection dbConnection, IDbCommand command, CommandOptions<T> options) where T : new()
    {
        using (DbDataReader reader = ((DbCommand)command).ExecuteReader())
        {
            if (!reader.Read())
                return default;

            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            return map(reader);
        }
    }

    private static T? ExecuteReaderForGetNonAsync<T>(IDbConnection connection, IDbCommand command, CommandOptions<T> options) where T : new()
    {
        using (IDataReader reader = command.ExecuteReader())
        {
            if (!reader.Read())
                return default;

            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            return map(reader);
        }
    }

    private static void AddGetPrimaryKeyParameter(IDbCommand command, CachedCrudSql cached, object id)
    {
        ColumnMetadata primaryKey = cached.Metadata.PrimaryKeys[0];
        IDbDataParameter param = command.CreateParameter();
        param.ParameterName = "@" + primaryKey.ColumnName;
        param.Value = id ?? DBNull.Value;
        command.Parameters.Add(param);
    }
    internal static async ValueTask<T?> GetByIdSimpleCoreAsync<T>(DbConnection dbConnection, object id, CommandOptions<T> options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException($"Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

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
            command.CommandText = cached.SelectByIdSql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameterAsync(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return default;

            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            return map(reader);
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

    internal static async ValueTask<T?> GetByIdTypedCoreAsync<T, TId>(DbConnection dbConnection, TId id, CommandOptions<T> options, CancellationToken cancellationToken) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException($"Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

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
            command.CommandText = cached.SelectByIdSql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameterAsync(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return default;

            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            return map(reader);
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

    private static void AddGetPrimaryKeyParameterAsync(DbCommand command, CachedCrudSql cached, object id)
    {
        ColumnMetadata primaryKey = cached.Metadata.PrimaryKeys[0];
        DbParameter param = command.CreateParameter();
        param.ParameterName = "@" + primaryKey.ColumnName;
        param.Value = id ?? DBNull.Value;
        command.Parameters.Add(param);
    }
}
