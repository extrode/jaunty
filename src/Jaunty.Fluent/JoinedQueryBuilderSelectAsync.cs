using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Internals.Enums;
using Jaunty.Internals.Read;

namespace Jaunty.Fluent;

/// <summary>
/// Async select operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public async Task<List<TFrom>> SelectAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = BuildSelectSql(columns);

        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialAsync<TFrom>(sql, _parameters.ToParameterObject(), cancellationToken).ConfigureAwait(false);

        return Select();
    }

    public async Task<List<T>> SelectAsync<T>(CancellationToken cancellationToken = default)
        where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            List<TFrom> result = await SelectAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<List<TFrom>, List<T>>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            List<TJoin> result = await SelectJoinedAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<List<TJoin>, List<T>>(ref result);
        }

        return await Task.Run(() => SelectWithMapping<T>(MappingMode.Strict), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T>> SelectAsync<T>(
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectWithMapper(mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(T1, T2)>> SelectAsync<T1, T2>(CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        if (typeof(T1) != typeof(TFrom))
            throw new ArgumentException($"T1 must be {typeof(TFrom).Name}, got {typeof(T1).Name}", nameof(T1));

        if (typeof(T2) != typeof(TJoin))
            throw new ArgumentException($"T2 must be {typeof(TJoin).Name}, got {typeof(T2).Name}", nameof(T2));

        List<(TFrom From, TJoin Joined)> result = await SelectBothInternalAsync(cancellationToken).ConfigureAwait(false);
        return Unsafe.As<List<(TFrom, TJoin)>, List<(T1, T2)>>(ref result);
    }

    public async Task<TFrom> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirst(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TFrom?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirstOrDefault(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync<T>(CancellationToken cancellationToken = default)
        where T : new()
    {
        return await Task.Run(() => SelectFirst<T>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync<T>(
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirst(mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default)
        where T : new()
    {
        return await Task.Run(() => SelectFirstOrDefault<T>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync<T>(
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirstOrDefault(mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<(TFrom From, TJoin Joined)> SelectFirstBothAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirstBoth(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Count(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => LongCount(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<TJoin>> SelectJoinedAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectJoined(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<(TFrom From, TJoin Joined)>> SelectBothInternalAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectBothInternal(), cancellationToken).ConfigureAwait(false);
    }
}
