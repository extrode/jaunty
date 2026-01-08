using System.Data.Common;

using Jaunty.InternalApi.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(DbConnection connection)
    {
#if NET8_0_OR_GREATER || ASYNC_ENUMERABLE_SUPPORT
        public IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryPartialStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
#else
        public Task<IEnumerable<T>> QueryPartialStreamAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryPartialStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryPartialStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryPartialStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
#endif
    }
}
