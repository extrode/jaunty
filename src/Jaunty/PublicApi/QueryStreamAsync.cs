using System.Data.Common;

using Jaunty.InternalApi.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(DbConnection connection)
    {
#if ASYNC_ENUMERABLE_SUPPORT
        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
#else
        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryStreamAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
#endif
    }
}
