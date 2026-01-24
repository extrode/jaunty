using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
#if ASYNC_ENUMERABLE_SUPPORT
    public static IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }
#else
        public static Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
#endif
}
