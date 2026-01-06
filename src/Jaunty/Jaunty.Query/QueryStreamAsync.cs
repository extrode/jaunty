using System.Data;
using System.Data.Common;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
#if NET8_0_OR_GREATER || ASYNC_ENUMERABLE_SUPPORT
    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, null, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, null, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, null, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, null, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Strict, null, default);
    }
#else
    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, null, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, null, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, null, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, null, cancellationToken);
    }

    public static Task<IEnumerable<T>> QueryStreamAsync<T>(this DbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return QueryStreamCoreAsync<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Strict, null, default);
    }
#endif
}
