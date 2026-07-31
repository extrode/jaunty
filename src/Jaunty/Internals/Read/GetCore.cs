using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Read;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;
using Jaunty.Interfaces;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;
using Jaunty.Interceptors;
using Jaunty.Internals;

namespace Jaunty;

public static partial class Jaunty
{
    internal static T? GetByIdSimpleCore<T>(IDbConnection connection, object id, CommandOptions<T> options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException($"Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs so GetById participates in registered
        // ICommandInterceptor auditing/logging the same way GetAll/Query/etc. do.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return pipeline.ExecuteWithInterception(
                cached.SelectByIdSql,
                cached.DescribeIdParameter(id),
                connection,
                options.CommandType,
                () => GetByIdSimpleCoreDirect(connection, id, cached, options));
        }

        return GetByIdSimpleCoreDirect(connection, id, cached, options);
    }

    private static T? GetByIdSimpleCoreDirect<T>(IDbConnection connection, object id, CachedCrudSql cached, CommandOptions<T> options) where T : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.SelectByIdSql;

            // R27 batch 5: previously reported to the interceptor pipeline but never applied
            // (the AUD-R26 GetAllCore fix did not bring the by-id siblings along). Guarded the
            // same way: a `default` CommandOptions<T> carries CommandType 0, which providers
            // reject outright.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring GetAllCoreDirect/ExecuteReaderDirect) so an
            // incompatible transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, cached.DescribeIdParameter(id));

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
            throw new InvalidOperationException($"Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return pipeline.ExecuteWithInterception(
                cached.SelectByIdSql,
                cached.DescribeIdParameter(id),
                connection,
                options.CommandType,
                () => GetByIdTypedCoreDirect<T, TId>(connection, id, cached, options));
        }

        return GetByIdTypedCoreDirect<T, TId>(connection, id, cached, options);
    }

    private static T? GetByIdTypedCoreDirect<T, TId>(IDbConnection connection, TId id, CachedCrudSql cached, CommandOptions<T> options) where T : IEntity<TId>, new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.SelectByIdSql;

            // R27 batch 5: previously reported to the interceptor pipeline but never applied
            // (the AUD-R26 GetAllCore fix did not bring the by-id siblings along). Guarded the
            // same way: a `default` CommandOptions<T> carries CommandType 0, which providers
            // reject outright.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring GetAllCoreDirect/ExecuteReaderDirect) so an
            // incompatible transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameter(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, cached.DescribeIdParameter(id));

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
        param.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(id, primaryKey.Property) ?? DBNull.Value;
        command.Parameters.Add(param);
    }
    internal static async ValueTask<T?> GetByIdSimpleCoreAsync<T>(DbConnection dbConnection, object id, CommandOptions<T> options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException($"Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return await pipeline.ExecuteWithInterceptionAsync(
                cached.SelectByIdSql,
                cached.DescribeIdParameter(id),
                dbConnection,
                options.CommandType,
                () => GetByIdSimpleCoreDirectAsync(dbConnection, id, cached, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await GetByIdSimpleCoreDirectAsync(dbConnection, id, cached, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> GetByIdSimpleCoreDirectAsync<T>(DbConnection dbConnection, object id, CachedCrudSql cached, CommandOptions<T> options, CancellationToken cancellationToken) where T : new()
    {
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
            command.CommandText = cached.SelectByIdSql;

            // R27 batch 5: see the sync sites - applied, not merely announced, with the same
            // guard against the struct default's CommandType 0.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameterAsync(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, cached.DescribeIdParameter(id));

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    internal static async ValueTask<T?> GetByIdTypedCoreAsync<T, TId>(DbConnection dbConnection, TId id, CommandOptions<T> options, CancellationToken cancellationToken) where T : IEntity<TId>, new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        if (string.IsNullOrEmpty(cached.SelectByIdSql))
            throw new InvalidOperationException($"Cannot get entity of type '{typeof(T).Name}' by ID: Ensure it has exactly one primary key.");

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return await pipeline.ExecuteWithInterceptionAsync(
                cached.SelectByIdSql,
                cached.DescribeIdParameter(id),
                dbConnection,
                options.CommandType,
                () => GetByIdTypedCoreDirectAsync<T, TId>(dbConnection, id, cached, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await GetByIdTypedCoreDirectAsync<T, TId>(dbConnection, id, cached, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> GetByIdTypedCoreDirectAsync<T, TId>(DbConnection dbConnection, TId id, CachedCrudSql cached, CommandOptions<T> options, CancellationToken cancellationToken) where T : IEntity<TId>, new()
    {
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
            command.CommandText = cached.SelectByIdSql;

            // R27 batch 5: see the sync sites - applied, not merely announced, with the same
            // guard against the struct default's CommandType 0.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            AddGetPrimaryKeyParameterAsync(command, cached, id!);

            JauntyConfig.Logger?.Invoke(command.CommandText, cached.DescribeIdParameter(id));

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    private static void AddGetPrimaryKeyParameterAsync(DbCommand command, CachedCrudSql cached, object id)
    {
        ColumnMetadata primaryKey = cached.Metadata.PrimaryKeys[0];
        DbParameter param = command.CreateParameter();
        param.ParameterName = "@" + primaryKey.ColumnName;
        param.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(id, primaryKey.Property) ?? DBNull.Value;
        command.Parameters.Add(param);
    }
}
