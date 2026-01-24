using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
#if ASYNC_ENUMERABLE_SUPPORT
    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }
#else
        public static Task<IEnumerable<T>> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
#endif
}
