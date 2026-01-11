using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
#if ASYNC_ENUMERABLE_SUPPORT
        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
#else
        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return connection is not DbConnection dbConnection
                ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
                : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
#endif
    }
}
