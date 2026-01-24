using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
#if ASYNC_ENUMERABLE_SUPPORT
    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    public static IAsyncEnumerable<T> QueryStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#else
        public static Task<IEnumerable<T>> QueryStreamAsync<T>(this IDbConnection connection,  string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryStreamAsync<T>(this IDbConnection connection,  string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryStreamAsync<T>(this IDbConnection connection,  string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public static Task<IEnumerable<T>> QueryStreamAsync<T>(this IDbConnection connection,  string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
#endif
}
