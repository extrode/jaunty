using System.Data;

namespace Jaunty.Fluent;

/// <summary>
/// Async SelectPartial operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public async Task<List<IDictionary<string, object?>>> SelectPartialAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartial(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T>> SelectPartialAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartial(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IDictionary<string, object?>> SelectPartialFirstAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirst(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialFirstAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirst(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirstOrDefault(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirstOrDefault(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IDictionary<string, object?>> SelectPartialSingleAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingle(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialSingleAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingle(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingleOrDefault(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingleOrDefault(columns, mapper), cancellationToken).ConfigureAwait(false);
    }
}
