using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;
using Jaunty.Internals.Read;
using Jaunty.Interceptors;
using Jaunty.Internals;

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

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs so Insert participates in registered
        // ICommandInterceptor auditing/logging the same way Query/GetAll/etc. do.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return pipeline.ExecuteWithInterception(
                cached.InsertCommandText,
                entity,
                connection,
                options.CommandType,
                () => InsertCoreDirect(connection, entity, cached, binder, options));
        }

        return InsertCoreDirect(connection, entity, cached, binder, options);
    }

    private static long InsertCoreDirect<T>(IDbConnection connection, T entity, CachedCrudSql cached, Action<IDbCommand, T> binder, CommandOptions options) where T : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.InsertCommandText;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring GetByIdSimpleCoreDirect) so an incompatible
            // transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            binder(command, entity);

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            if (cached.HasIdentityKey)
            {
                var result = command.ExecuteScalar();

                // AUD-R35-133: the write-back used to be gated on `id > 0`, which conflated "no key
                // came back" with "the key that came back was not positive". A negative identity is
                // ordinary - IDENTITY(-2147483648, 1) is the standard wide-range-key seed on SQL
                // Server - and under the old test such an insert returned the real key to the caller
                // while leaving entity.Id at its default, so the row existed under a key the object
                // in hand did not carry. The `> 0` test is right for the row-counting use AUD-R24
                // adopted it for, where a suppressed insert must not be counted; that argument says
                // nothing about whether to populate the entity. The null/DBNull case still returns 0
                // without writing back, so an insert that yields no key never clobbers an id the
                // caller had already set.
                if (result is null or DBNull)
                    return 0;

                long id = ScalarConverter<long>.Convert(result);
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

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return await pipeline.ExecuteWithInterceptionAsync(
                cached.InsertCommandText,
                entity,
                dbConnection,
                options.CommandType,
                () => InsertCoreDirectAsync(dbConnection, entity, cached, binder, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await InsertCoreDirectAsync(dbConnection, entity, cached, binder, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<long> InsertCoreDirectAsync<T>(DbConnection dbConnection, T entity, CachedCrudSql cached, Action<IDbCommand, T> binder, CommandOptions options, CancellationToken cancellationToken) where T : new()
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
            command.CommandText = cached.InsertCommandText;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            binder(command, entity);

            JauntyConfig.Logger?.Invoke(command.CommandText, entity);

            if (cached.HasIdentityKey)
            {
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

                // AUD-R35-133, the async twin. See InsertCoreDirect for why the gate is now
                // "a key came back" rather than "the key is positive".
                if (result is null or DBNull)
                    return 0;

                long id = ScalarConverter<long>.Convert(result);
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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

}
