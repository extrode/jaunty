using System.Data;

using Jaunty.InternalApi.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public Task<T> QueryPartialSingleAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
        }

        public Task<T> QueryPartialSingleAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
        }

        public Task<T> QueryPartialSingleAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
        }

        public Task<T> QueryPartialSingleAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
        }
    }
}