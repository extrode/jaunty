using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;
using Jaunty.Interceptors;

namespace Jaunty;

public static partial class Jaunty
{
    private static async ValueTask<List<T>> QueryCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            var list = new List<T>(JauntyConfig.QueryResultCapacity);
            if (reader is DbDataReader dbReader)
            {
                Func<DbDataReader, T> map = DrDispatcher.Resolve(dbReader, options, mode);
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    list.Add(map(dbReader));
            }
            else
            {
                Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(map(reader));
                }
            }
            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> QueryFirstCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
                Func<DbDataReader, T> map = DrDispatcher.Resolve(dbReader, options, mode);
                return map(dbReader);
            }

            if (!reader.Read()) throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            ct.ThrowIfCancellationRequested();
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, options, mode);
            return mapFallback(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> QueryFirstOrDefaultCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;
                Func<DbDataReader, T> map = DrDispatcher.Resolve(dbReader, options, mode);
                return map(dbReader);
            }

            if (!reader.Read()) return default;
            ct.ThrowIfCancellationRequested();
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, options, mode);
            return mapFallback(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> QuerySingleCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync<T>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");

                Func<DbDataReader, T> map = DrDispatcher.Resolve(dbReader, options, mode);
                T? entity = map(dbReader);
                return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                    ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                    : entity!;
            }

            if (!reader.Read()) throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            ct.ThrowIfCancellationRequested();
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, options, mode);
            T? entityFallback = mapFallback(reader);
            ct.ThrowIfCancellationRequested();
            return reader.Read()
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                : entityFallback!;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> QuerySingleOrDefaultCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;

                Func<DbDataReader, T> map = DrDispatcher.Resolve(dbReader, options, mode);
                T? entity = map(dbReader);
                return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                    ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                    : entity;
            }

            if (!reader.Read()) return default;
            ct.ThrowIfCancellationRequested();
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, options, mode);
            T? entityFallback = mapFallback(reader);
            ct.ThrowIfCancellationRequested();
            return reader.Read()
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                : entityFallback;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> QueryScalarCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dbConnection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (dbConnection is null) throw new ArgumentNullException(nameof(dbConnection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif

        // Use InterceptorPipeline if registered, otherwise execute directly
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            T result = default!;
            await JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                dbConnection,
                options.CommandType,
                async () =>
                {
                    var wasClosed = dbConnection.State == ConnectionState.Closed;

                    try
                    {
                        if (wasClosed)
                            await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                        await using DbCommand command = dbConnection.CreateCommand();
#else
                        using DbCommand command = dbConnection.CreateCommand();
#endif
                        command.CommandText = sql;

                        if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                            command.CommandType = options.CommandType;

                        if (options.Transaction is DbTransaction dbTransaction)
                            command.Transaction = dbTransaction;

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

                        object? commandResult = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

                        if (commandResult is null or DBNull)
                            result = default!;
                        else if (commandResult is T direct)
                            result = direct;
                        else
                            result = ScalarConverter<T>.Convert(commandResult);

                        return result;
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
                },
                cancellationToken).ConfigureAwait(false);
            return result;
        }

        // Fast path: no interceptors, direct execution
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = dbConnection.CreateCommand();
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (result is null or DBNull)
                return default!;

            if (result is T direct)
                return direct;

            return ScalarConverter<T>.Convert(result);
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

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<T> QueryStreamCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = dbConnection.CreateCommand();
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
            Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return map(reader);
            }
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
#else
    private static async ValueTask<IEnumerable<T>> QueryStreamCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        var results = new List<T>(JauntyConfig.QueryResultCapacity);
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var command = dbConnection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var map = DrDispatcher.Resolve(reader, options, mode);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(map(reader));
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

        return results;
    }
#endif

    #region Multi-Entity Async Core Methods

    private static async ValueTask<List<(T1, T2)>> QueryMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2)>(JauntyConfig.QueryResultCapacity * 2);

            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return results;

                var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();

                    mapping.ApplyT1(t1, dbReader);
                    mapping.ApplyT2(t2, dbReader);

                    results.Add((t1, t2));
                }
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false));
            }
            else
            {
                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                do
                {
                    ct.ThrowIfCancellationRequested();
                    var t1 = new T1();
                    var t2 = new T2();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);

                    results.Add((t1, t2));
                }
                while (reader.Read());
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2)> QueryFirstMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        (T1, T2)? result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return ((T1, T2)?)null;

                var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);

                return (t1, t2);
            }

            if (!reader.Read())
                return null;

            var mappingFallback = MultiEntityMapper<T1, T2>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);

            return (t1Fallback, t2Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2)> QuerySingleMultiEntityCoreAsync<T1, T2>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        (T1, T2)? result = await QuerySingleOrDefaultMultiEntityCoreAsync(dbConnection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync<(T1, T2)?>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return null;

                var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

                return (t1, t2);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);

            ct.ThrowIfCancellationRequested();
            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

            return (t1Fallback, t2Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2)> QueryStreamMultiEntityCoreAsync<T1, T2>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using DbCommand command = dbConnection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                yield return (t1, t2);
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
                await Task.Run(() => dbConnection.Close(), cancellationToken).ConfigureAwait(false);
#endif
            }
        }
    }
#endif

    #endregion
}