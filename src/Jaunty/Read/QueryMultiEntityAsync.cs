using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously executes a query and maps columns to two entity types by property name.
    /// </summary>
    public static async Task<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        return await ExecuteReaderAsync<List<(T1, T2)>>(connection, sql, parameters, options, async (reader, ct) =>
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

    /// <summary>
    /// Asynchronously executes a query, maps to two entity types, and combines them using a function.
    /// </summary>
    public static async Task<List<TResult>> QueryAsync<T1, T2, TResult>(
        this IDbConnection connection,
        string sql,
        Func<T1, T2, TResult> map,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(map);
#else
        if (map is null) throw new ArgumentNullException(nameof(map));
#endif

        return await ExecuteReaderAsync<List<TResult>>(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<TResult>();

            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                return results;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                results.Add(map(t1, t2));
            }
            while (await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false));

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes a query and returns the first row mapped to two entity types.
    /// </summary>
    public static async Task<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        return await ExecuteReaderAsync<(T1, T2)>(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("Sequence contains no elements");

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return (t1, t2);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes a query and returns the first row mapped to two entity types, or default if empty.
    /// </summary>
    public static async Task<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        return await ExecuteReaderAsync<(T1, T2)?>(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                return null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return (t1, t2);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously executes a query and returns exactly one row mapped to two entity types.
    /// </summary>
    public static async Task<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        return await ExecuteReaderAsync<(T1, T2)>(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("Sequence contains no elements");

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

    /// <summary>
    /// Asynchronously executes a query and returns exactly one row mapped to two entity types, or default if empty.
    /// </summary>
    public static async Task<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        return await ExecuteReaderAsync<(T1, T2)?>(connection, sql, parameters, options, async (reader, ct) =>
        {
            if (!await ((DbDataReader)reader).ReadAsync(ct).ConfigureAwait(false))
                return null;

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

    /// <summary>
    /// Asynchronously executes a query and streams rows mapped to two entity types.
    /// </summary>
    public static async IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif

        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("Connection must be a DbConnection for async operations", nameof(connection));

        var wasClosed = connection.State == ConnectionState.Closed;

        DbCommand? command = null;
        DbDataReader? reader = null;
        MultiEntityMapper<T1, T2>? mapping = null;

        try
        {
            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            command = dbConnection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            mapping = MultiEntityMapper<T1, T2>.Build(reader);

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
            if (reader is not null) await reader.DisposeAsync().ConfigureAwait(false);
            if (command is not null) await command.DisposeAsync().ConfigureAwait(false);
#else
            reader?.Dispose();
            command?.Dispose();
#endif
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
