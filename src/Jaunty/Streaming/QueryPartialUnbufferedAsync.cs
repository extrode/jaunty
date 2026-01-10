using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(DbConnection connection)
    {
#if ASYNC_ENUMERABLE_SUPPORT
        public IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public IAsyncEnumerable<T> QueryPartialUnbufferedAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
#else
        public Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryPartialUnbufferedAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryStreamCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
#endif
    }
}
