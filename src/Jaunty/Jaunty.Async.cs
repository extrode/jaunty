using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Enums;
using Jaunty.Internal.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async Task<T> QueryScalarCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                return !await dbReader.ReadAsync(ct).ConfigureAwait(false) || await dbReader.IsDBNullAsync(0, ct).ConfigureAwait(false) ? default!
                    : await dbReader.GetFieldValueAsync<T>(0, ct).ConfigureAwait(false);
            }

            // Fallback for non-DbDataReader
            if (!reader.Read() || reader.IsDBNull(0)) return default!;
            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<T>> QueryCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<T>();
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, mapper, mode);

            if (reader is DbDataReader dbReader)
            {
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    results.Add(map(reader));
            }
            else
            {
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    results.Add(map(reader));
                }
            }
            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TResult> ExecuteReaderAsync<TResult>(IDbConnection connection, string sql, object? parameters,
        CommandOptions options, Func<IDataReader, CancellationToken, Task<TResult>> handler, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var command = dbConnection.CreateCommand();
                command.CommandText = sql;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods
                if (wasClosed) connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = command.ExecuteReader();
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
#else
                    connection.Close();
#endif
            }
        }
    }

#if NET8_0_OR_GREATER || ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<T> QueryStreamCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var map = DrDispatcher.Resolve(reader, mapper, mode);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield return map(reader);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }
#else
    private static async Task<IEnumerable<T>> QueryStreamCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, CancellationToken cancellationToken = default) where T : new()
    {
        var results = new List<T>();
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var map = DrDispatcher.Resolve(reader, mapper, mode);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(map(reader));
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
#else
                    connection.Close();
#endif
        }

        return results;
    }
#endif

    private static async Task<T> QueryFirstCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, CancellationToken cancellationToken = default) where T : new()
    {
        T? entity = await QueryFirstOrDefaultCoreAsync<T>(connection, sql, parameters, options, mode, mapper, cancellationToken).ConfigureAwait(false);
        return entity is null ? throw new InvalidOperationException("Sequence contains no elements") : entity;
    }

    private static async Task<T?> QueryFirstOrDefaultCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;
                Func<IDataReader, T> map = DrDispatcher.Resolve(reader, mapper, mode);
                return map(reader);
            }
            // Fallback for non-DbDataReader
            if (!reader.Read()) return default;
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, mapper, mode);
            return mapFallback(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> QuerySingleCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, CancellationToken cancellationToken = default) where T : new()
    {
        T? entity = await QuerySingleOrDefaultCoreAsync<T>(connection, sql, parameters, options, mode, mapper, cancellationToken).ConfigureAwait(false);
        return entity ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    private static async Task<T?> QuerySingleOrDefaultCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, Func<IDataReader, T>? mapper = null, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;

                Func<IDataReader, T> map = DrDispatcher.Resolve(reader, mapper, mode);
                T? entity = map(reader);
                return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                    ? throw new InvalidOperationException("Sequence contains more than one element")
                    : entity;
            }
            // Fallback for non-DbDataReader
            if (!reader.Read()) return default;
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, mapper, mode);
            T? entityFallback = mapFallback(reader);
            return reader.Read()
                ? throw new InvalidOperationException("Sequence contains more than one element")
                : entityFallback;
        }, cancellationToken).ConfigureAwait(false);
    }
}