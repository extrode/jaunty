using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty.Read;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public Task<T?> QueryPartialFirstOrDefaultAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public Task<T?> QueryPartialFirstOrDefaultAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public Task<T?> QueryPartialFirstOrDefaultAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public Task<T?> QueryPartialFirstOrDefaultAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
    }
}
