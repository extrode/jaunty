using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async ValueTask<List<T>> QueryCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var list = new List<T>();
            if (reader is DbDataReader dbReader)
            {
                var map = DrDispatcher.Resolve(dbReader, options, mode);
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    list.Add(map(dbReader));
            }
            else
            {
                var map = DrDispatcher.Resolve(reader, options, mode);
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(map(reader));
                }
            }
            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> QueryFirstCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
                var map = DrDispatcher.Resolve(dbReader, options, mode);
                return map(dbReader);
            }
            
            if (!reader.Read()) throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            var mapFallback = DrDispatcher.Resolve(reader, options, mode);
            return mapFallback(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> QueryFirstOrDefaultCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;
                var map = DrDispatcher.Resolve(dbReader, options, mode);
                return map(dbReader);
            }
            
            if (!reader.Read()) return default;
            var mapFallback = DrDispatcher.Resolve(reader, options, mode);
            return mapFallback(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> QuerySingleCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");

                var map = DrDispatcher.Resolve(dbReader, options, mode);
                T? entity = map(dbReader);
                return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                    ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                    : entity!;
            }
            
            if (!reader.Read()) throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            var mapFallback = DrDispatcher.Resolve(reader, options, mode);
            T? entityFallback = mapFallback(reader);
            return reader.Read()
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                : entityFallback!;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> QuerySingleOrDefaultCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;

                var map = DrDispatcher.Resolve(dbReader, options, mode);
                T? entity = map(dbReader);
                return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                    ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                    : entity;
            }
            
            if (!reader.Read()) return default;
            var mapFallback = DrDispatcher.Resolve(reader, options, mode);
            T? entityFallback = mapFallback(reader);
            return reader.Read()
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                : entityFallback;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> QueryScalarCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken)
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                return !await dbReader.ReadAsync(ct).ConfigureAwait(false) || await dbReader.IsDBNullAsync(0, ct).ConfigureAwait(false) ? default!
                    : await dbReader.GetFieldValueAsync<T>(0, ct).ConfigureAwait(false);
            }

            if (!reader.Read() || reader.IsDBNull(0)) return default!;
            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<T> QueryStreamCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = connection.CreateCommand();
#else
            using DbCommand command = connection.CreateCommand();
#endif
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

#if NET8_0_OR_GREATER
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
            var map = DrDispatcher.Resolve(reader, options, mode);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield return map(reader);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }
#else
    private static async ValueTask<IEnumerable<T>> QueryStreamCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
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
            var map = DrDispatcher.Resolve(reader, options, mode);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(map(reader));
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
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
            var results = new List<(T1, T2)>();

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
        var result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
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
                return ((T1, T2)?)null;

            var mappingFallback = MultiEntityMapper<T1, T2>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);

            return (t1Fallback, t2Fallback);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(T1, T2)> QuerySingleMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        var result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : result.Value;
    }

    private static async ValueTask<(T1, T2)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
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

                if (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

                return (t1, t2);
            }
            
            if (!reader.Read())
                return ((T1, T2)?)null;

            var mappingFallback = MultiEntityMapper<T1, T2>.Build(reader);

            var t1Fallback = new T1();
            var t2Fallback = new T2();

            mappingFallback.ApplyT1(t1Fallback, reader);
            mappingFallback.ApplyT2(t2Fallback, reader);

            if (reader.Read())
                throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

            return (t1Fallback, t2Fallback);
        }, cancellationToken).ConfigureAwait(false);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    private static async IAsyncEnumerable<(T1, T2)> QueryStreamMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

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
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }
#endif

    #endregion
}


