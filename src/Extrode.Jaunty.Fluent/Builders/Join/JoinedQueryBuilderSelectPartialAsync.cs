using System.Data;
using System.Data.Common;

using Extrode.Jaunty.Core;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Internals;

namespace Extrode.Jaunty.Fluent;

/// <summary>
/// Async SelectPartial operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public Task<List<IDictionary<string, object?>>> SelectPartialAsync(
        string columns,
        CancellationToken cancellationToken = default)
        => SelectPartialAsync(columns, default(CommandOptions), cancellationToken);

    public Task<List<IDictionary<string, object?>>> SelectPartialAsync(
        string columns,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(BuildSelectPartialSql(columns), options, async (reader, ct) =>
        {
            var results = new List<IDictionary<string, object?>>();

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                results.Add(MapToDictionary(reader));

            return results;
        }, cancellationToken);

    public Task<List<T>> SelectPartialAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
        => SelectPartialAsync(columns, mapper, default, cancellationToken);

    public Task<List<T>> SelectPartialAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(BuildSelectPartialSql(columns), options, async (reader, ct) =>
        {
            var results = new List<T>();

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                results.Add(mapper(reader));

            return results;
        }, cancellationToken);

    public Task<IDictionary<string, object?>> SelectPartialFirstAsync(
        string columns,
        CancellationToken cancellationToken = default)
        => SelectPartialFirstAsync(columns, default(CommandOptions), cancellationToken);

    public Task<IDictionary<string, object?>> SelectPartialFirstAsync(
        string columns,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1), options, async (reader, ct) =>
            await reader.ReadAsync(ct).ConfigureAwait(false)
                ? MapToDictionary(reader)
                : throw new InvalidOperationException("Sequence contains no elements."),
            cancellationToken);

    public Task<T> SelectPartialFirstAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
        => SelectPartialFirstAsync(columns, mapper, default, cancellationToken);

    public Task<T> SelectPartialFirstAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1), options, async (reader, ct) =>
            await reader.ReadAsync(ct).ConfigureAwait(false)
                ? mapper(reader)
                : throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'."),
            cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(
        string columns,
        CancellationToken cancellationToken = default)
        => SelectPartialFirstOrDefaultAsync(columns, default(CommandOptions), cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(
        string columns,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1), options, async (reader, ct) =>
            await reader.ReadAsync(ct).ConfigureAwait(false) ? MapToDictionary(reader) : null,
            cancellationToken);

    public Task<T?> SelectPartialFirstOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
        => SelectPartialFirstOrDefaultAsync(columns, mapper, default, cancellationToken);

    public Task<T?> SelectPartialFirstOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1), options, async (reader, ct) =>
            await reader.ReadAsync(ct).ConfigureAwait(false) ? mapper(reader) : default,
            cancellationToken);

    public Task<IDictionary<string, object?>> SelectPartialSingleAsync(
        string columns,
        CancellationToken cancellationToken = default)
        => SelectPartialSingleAsync(columns, default(CommandOptions), cancellationToken);

    public async Task<IDictionary<string, object?>> SelectPartialSingleAsync(
        string columns,
        CommandOptions options,
        CancellationToken cancellationToken = default)
    {
        IDictionary<string, object?>? result = await SelectPartialSingleOrDefaultAsync(columns, options, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    public Task<T> SelectPartialSingleAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
        => SelectPartialSingleAsync(columns, mapper, default, cancellationToken);

    public async Task<T> SelectPartialSingleAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CommandOptions options,
        CancellationToken cancellationToken = default)
    {
        (int count, T? result) = await SelectPartialSingleCoreAsync(columns, mapper, options, cancellationToken).ConfigureAwait(false);

        // R27 batch 8: emptiness by row count, not a null test - with a value-type T the old
        // "result ?? throw" saw default(T) after zero rows and returned 0 instead of throwing.
        if (count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return result!;
    }

    public Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(
        string columns,
        CancellationToken cancellationToken = default)
        => SelectPartialSingleOrDefaultAsync(columns, default(CommandOptions), cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(
        string columns,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => ExecutePartialAsync(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 2), options, async (reader, ct) =>
        {
            IDictionary<string, object?>? result = null;
            int count = 0;

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException("Sequence contains more than one element.");
                result = MapToDictionary(reader);
            }

            return result;
        }, cancellationToken);

    public Task<T?> SelectPartialSingleOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
        => SelectPartialSingleOrDefaultAsync(columns, mapper, default, cancellationToken);

    public async Task<T?> SelectPartialSingleOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CommandOptions options,
        CancellationToken cancellationToken = default)
        => (await SelectPartialSingleCoreAsync(columns, mapper, options, cancellationToken).ConfigureAwait(false)).Value;

    private Task<(int Count, T? Value)> SelectPartialSingleCoreAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CommandOptions options,
        CancellationToken cancellationToken)
        => ExecutePartialAsync(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 2), options, async (reader, ct) =>
        {
            T? result = default;
            int count = 0;

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.");
                result = mapper(reader);
            }

            return (count, result);
        }, cancellationToken);

    /// <summary>
    /// The async twin of <see cref="ExecutePartial{TResult}"/>; see its AUD-R38-031 note.
    /// </summary>
    private async Task<TResult> ExecutePartialAsync<TResult>(
        string sql,
        CommandOptions options,
        Func<DbDataReader, CancellationToken, ValueTask<TResult>> read,
        CancellationToken cancellationToken)
    {
        return await CommandObservation.ExecuteAsync(
            sql, DescribeParameters(), _connection, FluentCommandOptions.Describe(options), Body, cancellationToken).ConfigureAwait(false);

        async ValueTask<TResult> Body()
        {
            if (_connection is not DbConnection dbConn)
                throw new InvalidOperationException("Async operations require a DbConnection.");

#if NET8_0_OR_GREATER
            DbCommand command = dbConn.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConn.CreateCommand();
#endif
            command.CommandText = sql;
            FluentCommandOptions.Apply(command, dbConn, options);
            BindParameters(command);

            CommandObservation.Log(sql, DescribeParameters());

            bool wasClosed = dbConn.State == ConnectionState.Closed;
            if (wasClosed)
                await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
#if NET8_0_OR_GREATER
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using var readerDisposer = reader.ConfigureAwait(false);
#else
                using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

                return await read(reader, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (wasClosed)
                {
#if NET8_0_OR_GREATER
                    await dbConn.CloseAsync().ConfigureAwait(false);
#else
                    dbConn.Close();
#endif
                }
            }
        }
    }
}
