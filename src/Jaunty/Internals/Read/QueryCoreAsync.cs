using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Core;
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
            var list = new List<T>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);
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

    // Internal (not private) so tests can inject a custom DbCommand wrapper that deterministically
    // cancels the operation's CancellationToken from inside ExecuteScalarAsync, right before the
    // real exception it throws propagates into this method's finally-block cleanup. There is no
    // other way to reach that precise interleaving through the public API, since it depends on
    // control over exactly when cancellation happens relative to command execution.
    internal static async ValueTask<T> QueryScalarCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken)
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
                        DbCommand command = dbConnection.CreateCommand();
                        await using var commandDisposer = command.ConfigureAwait(false);
#else
                        using DbCommand command = dbConnection.CreateCommand();
#endif
                        command.CommandText = sql;

                        if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                            command.CommandType = options.CommandType;

                        command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

                        JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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
                            await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
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
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    private static async IAsyncEnumerable<T> QueryStreamCoreAsync<T>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    #region Multi-Entity Async Core Methods

    private static async ValueTask<List<(T1, T2)>> QueryMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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

            ct.ThrowIfCancellationRequested();
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

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

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
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }
#endif

    #endregion

    #region N-ary Multi-Entity Async Core Methods

    private static async ValueTask<List<(T1, T2, T3)>> QueryMultiEntityCoreAsync<T1, T2, T3>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2, T3)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(dbReader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();

                    mapping.ApplyT1(t1, dbReader);
                    mapping.ApplyT2(t2, dbReader);
                    mapping.ApplyT3(t3, dbReader);

                    results.Add((t1, t2, t3));
                }
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false));
            }
            else
            {
                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

                do
                {
                    ct.ThrowIfCancellationRequested();
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);
                    mapping.ApplyT3(t3, reader);

                    results.Add((t1, t2, t3));
                }
                while (reader.Read());
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3)> QueryFirstMultiEntityCoreAsync<T1, T2, T3>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new()
    {
        (T1, T2, T3)? result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return ((T1, T2, T3)?)null;

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);

                return (t1, t2, t3);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);

            return (t1Fallback, t2Fallback, t3Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3)> QuerySingleMultiEntityCoreAsync<T1, T2, T3>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new()
    {
        (T1, T2, T3)? result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3>(dbConnection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new()
    {
        return await ExecuteReaderAsync<(T1, T2, T3)?>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return null;

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.");

                return (t1, t2, t3);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);

            ct.ThrowIfCancellationRequested();
            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.");

            return (t1Fallback, t2Fallback, t3Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2, T3)> QueryStreamMultiEntityCoreAsync<T1, T2, T3>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);
                mapping.ApplyT3(t3, reader);

                yield return (t1, t2, t3);
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
#endif

    private static async ValueTask<List<(T1, T2, T3, T4)>> QueryMultiEntityCoreAsync<T1, T2, T3, T4>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2, T3, T4)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(dbReader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();

                    mapping.ApplyT1(t1, dbReader);
                    mapping.ApplyT2(t2, dbReader);
                    mapping.ApplyT3(t3, dbReader);
                    mapping.ApplyT4(t4, dbReader);

                    results.Add((t1, t2, t3, t4));
                }
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false));
            }
            else
            {
                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

                do
                {
                    ct.ThrowIfCancellationRequested();
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);
                    mapping.ApplyT3(t3, reader);
                    mapping.ApplyT4(t4, reader);

                    results.Add((t1, t2, t3, t4));
                }
                while (reader.Read());
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4)> QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        (T1, T2, T3, T4)? result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return ((T1, T2, T3, T4)?)null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);

                return (t1, t2, t3, t4);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4)> QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        (T1, T2, T3, T4)? result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        return await ExecuteReaderAsync<(T1, T2, T3, T4)?>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.");

                return (t1, t2, t3, t4);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);

            ct.ThrowIfCancellationRequested();
            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.");

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);
                mapping.ApplyT3(t3, reader);
                mapping.ApplyT4(t4, reader);

                yield return (t1, t2, t3, t4);
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
#endif

    private static async ValueTask<List<(T1, T2, T3, T4, T5)>> QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2, T3, T4, T5)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(dbReader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();
                    var t5 = new T5();

                    mapping.ApplyT1(t1, dbReader);
                    mapping.ApplyT2(t2, dbReader);
                    mapping.ApplyT3(t3, dbReader);
                    mapping.ApplyT4(t4, dbReader);
                    mapping.ApplyT5(t5, dbReader);

                    results.Add((t1, t2, t3, t4, t5));
                }
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false));
            }
            else
            {
                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

                do
                {
                    ct.ThrowIfCancellationRequested();
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();
                    var t5 = new T5();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);
                    mapping.ApplyT3(t3, reader);
                    mapping.ApplyT4(t4, reader);
                    mapping.ApplyT5(t5, reader);

                    results.Add((t1, t2, t3, t4, t5));
                }
                while (reader.Read());
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4, T5)> QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        (T1, T2, T3, T4, T5)? result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return ((T1, T2, T3, T4, T5)?)null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);
                mapping.ApplyT5(t5, dbReader);

                return (t1, t2, t3, t4, t5);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();
            var t5Fallback = new T5();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);
            mappingFallback.ApplyT5(t5Fallback, reader);

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback, t5Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4, T5)> QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        (T1, T2, T3, T4, T5)? result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        return await ExecuteReaderAsync<(T1, T2, T3, T4, T5)?>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);
                mapping.ApplyT5(t5, dbReader);

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.");

                return (t1, t2, t3, t4, t5);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();
            var t5Fallback = new T5();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);
            mappingFallback.ApplyT5(t5Fallback, reader);

            ct.ThrowIfCancellationRequested();
            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.");

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback, t5Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);
                mapping.ApplyT3(t3, reader);
                mapping.ApplyT4(t4, reader);
                mapping.ApplyT5(t5, reader);

                yield return (t1, t2, t3, t4, t5);
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
#endif

    private static async ValueTask<List<(T1, T2, T3, T4, T5, T6)>> QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2, T3, T4, T5, T6)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(dbReader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();
                    var t5 = new T5();
                    var t6 = new T6();

                    mapping.ApplyT1(t1, dbReader);
                    mapping.ApplyT2(t2, dbReader);
                    mapping.ApplyT3(t3, dbReader);
                    mapping.ApplyT4(t4, dbReader);
                    mapping.ApplyT5(t5, dbReader);
                    mapping.ApplyT6(t6, dbReader);

                    results.Add((t1, t2, t3, t4, t5, t6));
                }
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false));
            }
            else
            {
                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

                do
                {
                    ct.ThrowIfCancellationRequested();
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();
                    var t5 = new T5();
                    var t6 = new T6();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);
                    mapping.ApplyT3(t3, reader);
                    mapping.ApplyT4(t4, reader);
                    mapping.ApplyT5(t5, reader);
                    mapping.ApplyT6(t6, reader);

                    results.Add((t1, t2, t3, t4, t5, t6));
                }
                while (reader.Read());
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6)> QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        (T1, T2, T3, T4, T5, T6)? result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return ((T1, T2, T3, T4, T5, T6)?)null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();
                var t6 = new T6();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);
                mapping.ApplyT5(t5, dbReader);
                mapping.ApplyT6(t6, dbReader);

                return (t1, t2, t3, t4, t5, t6);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();
            var t5Fallback = new T5();
            var t6Fallback = new T6();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);
            mappingFallback.ApplyT5(t5Fallback, reader);
            mappingFallback.ApplyT6(t6Fallback, reader);

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback, t5Fallback, t6Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6)> QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        (T1, T2, T3, T4, T5, T6)? result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(dbConnection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        return await ExecuteReaderAsync<(T1, T2, T3, T4, T5, T6)?>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();
                var t6 = new T6();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);
                mapping.ApplyT5(t5, dbReader);
                mapping.ApplyT6(t6, dbReader);

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.");

                return (t1, t2, t3, t4, t5, t6);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();
            var t5Fallback = new T5();
            var t6Fallback = new T6();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);
            mappingFallback.ApplyT5(t5Fallback, reader);
            mappingFallback.ApplyT6(t6Fallback, reader);

            ct.ThrowIfCancellationRequested();
            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.");

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback, t5Fallback, t6Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2, T3, T4, T5, T6)> QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();
                var t6 = new T6();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);
                mapping.ApplyT3(t3, reader);
                mapping.ApplyT4(t4, reader);
                mapping.ApplyT5(t5, reader);
                mapping.ApplyT6(t6, reader);

                yield return (t1, t2, t3, t4, t5, t6);
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
#endif

    private static async ValueTask<List<(T1, T2, T3, T4, T5, T6, T7)>> QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2, T3, T4, T5, T6, T7)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(dbReader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();
                    var t5 = new T5();
                    var t6 = new T6();
                    var t7 = new T7();

                    mapping.ApplyT1(t1, dbReader);
                    mapping.ApplyT2(t2, dbReader);
                    mapping.ApplyT3(t3, dbReader);
                    mapping.ApplyT4(t4, dbReader);
                    mapping.ApplyT5(t5, dbReader);
                    mapping.ApplyT6(t6, dbReader);
                    mapping.ApplyT7(t7, dbReader);

                    results.Add((t1, t2, t3, t4, t5, t6, t7));
                }
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false));
            }
            else
            {
                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

                do
                {
                    ct.ThrowIfCancellationRequested();
                    var t1 = new T1();
                    var t2 = new T2();
                    var t3 = new T3();
                    var t4 = new T4();
                    var t5 = new T5();
                    var t6 = new T6();
                    var t7 = new T7();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);
                    mapping.ApplyT3(t3, reader);
                    mapping.ApplyT4(t4, reader);
                    mapping.ApplyT5(t5, reader);
                    mapping.ApplyT6(t6, reader);
                    mapping.ApplyT7(t7, reader);

                    results.Add((t1, t2, t3, t4, t5, t6, t7));
                }
                while (reader.Read());
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6, T7)> QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        (T1, T2, T3, T4, T5, T6, T7)? result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6, T7)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        return await ExecuteReaderAsync(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return ((T1, T2, T3, T4, T5, T6, T7)?)null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();
                var t6 = new T6();
                var t7 = new T7();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);
                mapping.ApplyT5(t5, dbReader);
                mapping.ApplyT6(t6, dbReader);
                mapping.ApplyT7(t7, dbReader);

                return (t1, t2, t3, t4, t5, t6, t7);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();
            var t5Fallback = new T5();
            var t6Fallback = new T6();
            var t7Fallback = new T7();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);
            mappingFallback.ApplyT5(t5Fallback, reader);
            mappingFallback.ApplyT6(t6Fallback, reader);
            mappingFallback.ApplyT7(t7Fallback, reader);

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback, t5Fallback, t6Fallback, t7Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6, T7)> QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        (T1, T2, T3, T4, T5, T6, T7)? result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(dbConnection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2, T3, T4, T5, T6, T7)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        return await ExecuteReaderAsync<(T1, T2, T3, T4, T5, T6, T7)?>(dbConnection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return null;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(dbReader);

                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();
                var t6 = new T6();
                var t7 = new T7();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);
                mapping.ApplyT3(t3, dbReader);
                mapping.ApplyT4(t4, dbReader);
                mapping.ApplyT5(t5, dbReader);
                mapping.ApplyT6(t6, dbReader);
                mapping.ApplyT7(t7, dbReader);

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.");

                return (t1, t2, t3, t4, t5, t6, t7);
            }

            if (!reader.Read())
                return null;

            ct.ThrowIfCancellationRequested();
            var mappingFallback = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();
            var t3Fallback = new T3();
            var t4Fallback = new T4();
            var t5Fallback = new T5();
            var t6Fallback = new T6();
            var t7Fallback = new T7();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);
            mappingFallback.ApplyT3(t3Fallback, reader);
            mappingFallback.ApplyT4(t4Fallback, reader);
            mappingFallback.ApplyT5(t5Fallback, reader);
            mappingFallback.ApplyT6(t6Fallback, reader);
            mappingFallback.ApplyT7(t7Fallback, reader);

            ct.ThrowIfCancellationRequested();
            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.");

            return (t1Fallback, t2Fallback, t3Fallback, t4Fallback, t5Fallback, t6Fallback, t7Fallback);

        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2, T3, T4, T5, T6, T7)> QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5, T6, T7>(DbConnection dbConnection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        var wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();
                var t3 = new T3();
                var t4 = new T4();
                var t5 = new T5();
                var t6 = new T6();
                var t7 = new T7();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);
                mapping.ApplyT3(t3, reader);
                mapping.ApplyT4(t4, reader);
                mapping.ApplyT5(t5, reader);
                mapping.ApplyT6(t6, reader);
                mapping.ApplyT7(t7, reader);

                yield return (t1, t2, t3, t4, t5, t6, t7);
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
#endif

    #endregion
}
