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
    private static async Task<List<T>> QueryCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var list = new List<T>();
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);

            if (reader is DbDataReader dbReader)
            {
                while (await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    list.Add(map(reader));
            }
            else
            {
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(map(reader));
                }
            }
            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> QueryFirstCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        T? entity = await QueryFirstOrDefaultCoreAsync<T>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return entity is null ? throw new InvalidOperationException("Sequence contains no elements") : entity;
    }

    private static async Task<T?> QueryFirstOrDefaultCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;
                Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
                return map(reader);
            }
            // Fallback for non-DbDataReader
            if (!reader.Read()) return default;
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, options, mode);
            return mapFallback(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> QuerySingleCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        T? entity = await QuerySingleOrDefaultCoreAsync<T>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return entity ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    private static async Task<T?> QuerySingleOrDefaultCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                    return default;

                Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
                T? entity = map(reader);
                return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                    ? throw new InvalidOperationException("Sequence contains more than one element")
                    : entity;
            }
            // Fallback for non-DbDataReader
            if (!reader.Read()) return default;
            Func<IDataReader, T> mapFallback = DrDispatcher.Resolve(reader, options, mode);
            T? entityFallback = mapFallback(reader);
            return reader.Read()
                ? throw new InvalidOperationException("Sequence contains more than one element")
                : entityFallback;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> QueryScalarCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken)
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
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
        }
    }
#else
    private static async Task<IEnumerable<T>> QueryStreamCoreAsync<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken = default) where T : new()
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
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
        }

        return results;
    }
#endif

    #region Multi-Entity Async Core Methods

    private static async Task<List<(T1, T2)>> QueryMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<(T1, T2)>();

            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
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
            while (await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false));

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(T1, T2)> QueryFirstMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        var result = await QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException("Sequence contains no elements") : result.Value;
    }

    private static async Task<(T1, T2)?> QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                return ((T1, T2)?)null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return (t1, t2);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(T1, T2)> QuerySingleMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        var result = await QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(connection, sql, parameters, options, mode, cancellationToken).ConfigureAwait(false);
        return result is null ? throw new InvalidOperationException("Sequence contains no elements") : result.Value;
    }

    private static async Task<(T1, T2)?> QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                return ((T1, T2)?)null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            if (await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("Sequence contains more than one element");

            return (t1, t2);
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
#if NET8_0_OR_GREATER
            await connection.CloseAsync().ConfigureAwait(false);
#else
            connection.Close();
#endif
        }
    }
#endif

    #endregion
}
