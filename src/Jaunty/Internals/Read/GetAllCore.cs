using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Read;
using Jaunty.Internals.Write;
using Jaunty.Interceptors;
using Jaunty.Internals;

namespace Jaunty;

public static partial class Jaunty
{
    internal static List<T> GetAllCore<T>(IDbConnection connection, CommandOptions<T> options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in ExecuteReader.cs so GetAll participates in registered
        // ICommandInterceptor auditing/logging the same way Query/QueryFirst/etc. do.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return pipeline.ExecuteWithInterception(
                cached.SelectAllSql,
                null,
                connection,
                options.CommandType,
                () => GetAllCoreDirect(connection, cached, options));
        }

        return GetAllCoreDirect(connection, cached, options);
    }

    private static List<T> GetAllCoreDirect<T>(IDbConnection connection, CachedCrudSql cached, CommandOptions<T> options) where T : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            if (connection is DbConnection dbConnection)
            {
                using DbCommand command = dbConnection.CreateCommand();
                command.CommandText = cached.SelectAllSql;
                // AUD-R26: previously reported to the interceptor pipeline but never applied, so an
                // interceptor was told the command was a stored procedure while it ran as text.
                // Guarded exactly as QueryCore guards it: CommandOptions<T> is a struct, so a
                // `default` options value carries CommandType 0, which is not a valid enum member
                // and which providers reject outright.
                if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                    command.CommandType = options.CommandType;

                command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                JauntyConfig.Logger?.Invoke(command.CommandText, null);

                using DbDataReader reader = command.ExecuteReader();
                // AUD-R26: GetAll accepted CommandOptions<T>.ExpectedRowCount and discarded it, while
                // every QueryCore result list honours it. GetAll is the API where the caller is most
                // likely to know the table size.
                var list = new List<T>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);
                if (!reader.Read())
                    return list;

                Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
                do { list.Add(map(reader)); }
                while (reader.Read());
                return list;
            }
            else
            {
                using IDbCommand command = connection.CreateCommand();
                command.CommandText = cached.SelectAllSql;
                // AUD-R26: previously reported to the interceptor pipeline but never applied, so an
                // interceptor was told the command was a stored procedure while it ran as text.
                // Guarded exactly as QueryCore guards it: CommandOptions<T> is a struct, so a
                // `default` options value carries CommandType 0, which is not a valid enum member
                // and which providers reject outright.
                if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                    command.CommandType = options.CommandType;

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                JauntyConfig.Logger?.Invoke(command.CommandText, null);

                using IDataReader reader = command.ExecuteReader();
                // AUD-R26: GetAll accepted CommandOptions<T>.ExpectedRowCount and discarded it, while
                // every QueryCore result list honours it. GetAll is the API where the caller is most
                // likely to know the table size.
                var list = new List<T>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);
                if (!reader.Read())
                    return list;

                Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
                do { list.Add(map(reader)); }
                while (reader.Read());
                return list;
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    internal static async ValueTask<List<T>> GetAllCoreAsync<T>(DbConnection dbConnection, CommandOptions<T> options, CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in ExecuteReaderAsync.cs so GetAllAsync participates in registered
        // ICommandInterceptor auditing/logging the same way QueryAsync/QueryFirstAsync/etc. do.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return await pipeline.ExecuteWithInterceptionAsync(
                cached.SelectAllSql,
                null,
                dbConnection,
                options.CommandType,
                () => GetAllCoreDirectAsync(dbConnection, cached, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await GetAllCoreDirectAsync(dbConnection, cached, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<List<T>> GetAllCoreDirectAsync<T>(DbConnection dbConnection, CachedCrudSql cached, CommandOptions<T> options, CancellationToken cancellationToken) where T : new()
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
            command.CommandText = cached.SelectAllSql;
            // AUD-R26: previously reported to the interceptor pipeline but never applied, so an
            // interceptor was told the command was a stored procedure while it ran as text.
            // Guarded exactly as QueryCore guards it: CommandOptions<T> is a struct, so a
            // `default` options value carries CommandType 0, which is not a valid enum member
            // and which providers reject outright.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            JauntyConfig.Logger?.Invoke(command.CommandText, null);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            // AUD-R26: GetAll accepted CommandOptions<T>.ExpectedRowCount and discarded it, while
            // every QueryCore result list honours it. GetAll is the API where the caller is most
            // likely to know the table size.
            var list = new List<T>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return list;

            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            do { list.Add(map(reader)); }
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false));

            return list;
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

    // Deliberately no InterceptorPipeline here, unlike GetAllCore above - see the rationale on
    // QueryStreamCore in QueryCore.cs. Same for GetAllStreamCoreAsync below.
    internal static IEnumerable<T> GetAllStreamCore<T>(IDbConnection connection, CommandOptions<T> options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (connection is DbConnection dbConnection)
        {
            foreach (T item in GetAllStreamCoreFast<T>(dbConnection, cached, options))
                yield return item;
            yield break;
        }

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.SelectAllSql;
            // AUD-R26: previously reported to the interceptor pipeline but never applied, so an
            // interceptor was told the command was a stored procedure while it ran as text.
            // Guarded exactly as QueryCore guards it: CommandOptions<T> is a struct, so a
            // `default` options value carries CommandType 0, which is not a valid enum member
            // and which providers reject outright.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            JauntyConfig.Logger?.Invoke(command.CommandText, null);

            using IDataReader reader = command.ExecuteReader();
            if (!reader.Read()) yield break;

            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            do { yield return map(reader); }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<T> GetAllStreamCoreFast<T>(DbConnection dbConnection, CachedCrudSql cached, CommandOptions<T> options) where T : new()
    {
        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) dbConnection.Open();

            using DbCommand command = dbConnection.CreateCommand();
            command.CommandText = cached.SelectAllSql;
            // AUD-R26: previously reported to the interceptor pipeline but never applied, so an
            // interceptor was told the command was a stored procedure while it ran as text.
            // Guarded exactly as QueryCore guards it: CommandOptions<T> is a struct, so a
            // `default` options value carries CommandType 0, which is not a valid enum member
            // and which providers reject outright.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            JauntyConfig.Logger?.Invoke(command.CommandText, null);

            using DbDataReader reader = command.ExecuteReader();
            if (!reader.Read()) yield break;

            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            do { yield return map(reader); }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
                dbConnection.Close();
        }
    }

    internal static async IAsyncEnumerable<T> GetAllStreamCoreAsync<T>(DbConnection dbConnection, CommandOptions<T> options, [EnumeratorCancellation] CancellationToken cancellationToken) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(dbConnection);

        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = cached.SelectAllSql;
            // AUD-R26: previously reported to the interceptor pipeline but never applied, so an
            // interceptor was told the command was a stored procedure while it ran as text.
            // Guarded exactly as QueryCore guards it: CommandOptions<T> is a struct, so a
            // `default` options value carries CommandType 0, which is not a valid enum member
            // and which providers reject outright.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            JauntyConfig.Logger?.Invoke(command.CommandText, null);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, MappingMode.Strict);
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return map(reader);
            }
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
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
