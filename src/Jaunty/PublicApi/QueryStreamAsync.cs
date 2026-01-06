using System.Data.Common;

using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

namespace Jaunty;

public static partial class Jaunty
{
#if NET8_0_OR_GREATER || ASYNC_ENUMERABLE_SUPPORT
    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#else
    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#endif
}
