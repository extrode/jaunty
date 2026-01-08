using System.Data;

using Jaunty.InternalApi.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public Task<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public Task<List<T>> QueryAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public Task<List<T>> QueryAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public Task<List<T>> QueryAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
    }
}
