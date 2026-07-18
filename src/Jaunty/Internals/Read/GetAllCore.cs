using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Read;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    internal static List<T> GetAllCore<T>(IDbConnection connection, CommandOptions<T> options) where T : new()
    {
        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            if (connection is DbConnection dbConnection)
            {
                using DbCommand command = dbConnection.CreateCommand();
                command.CommandText = cached.SelectAllSql;

                if (options.Transaction is DbTransaction dbTx)
                    command.Transaction = dbTx;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                JauntyConfig.Logger?.Invoke(command.CommandText, null);

                using DbDataReader reader = command.ExecuteReader();
                var list = new List<T>(JauntyConfig.QueryResultCapacity);
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

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                JauntyConfig.Logger?.Invoke(command.CommandText, null);

                using IDataReader reader = command.ExecuteReader();
                var list = new List<T>(JauntyConfig.QueryResultCapacity);
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

            if (options.Transaction is DbTransaction dbTx)
                command.Transaction = dbTx;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            JauntyConfig.Logger?.Invoke(command.CommandText, null);

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var list = new List<T>(JauntyConfig.QueryResultCapacity);
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

            if (options.Transaction is DbTransaction dbTx)
                command.Transaction = dbTx;

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

#if ASYNC_ENUMERABLE_SUPPORT
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

            if (options.Transaction is DbTransaction dbTx)
                command.Transaction = dbTx;

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
#else
    internal static async ValueTask<IEnumerable<T>> GetAllStreamCoreAsync<T>(DbConnection dbConnection, CommandOptions<T> options, CancellationToken cancellationToken) where T : new()
    {
        return await GetAllCoreAsync<T>(dbConnection, options, cancellationToken).ConfigureAwait(false);
    }
#endif
}
