using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Write;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;
using Jaunty.Internals.Read;

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
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return JauntyConfig.InterceptorPipeline.ExecuteWithInterception(
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
                long id = result is null or DBNull ? 0 : ScalarConverter<long>.Convert(result);

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

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return await JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
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
                long id = result is null or DBNull ? 0 : ScalarConverter<long>.Convert(result);

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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

}
