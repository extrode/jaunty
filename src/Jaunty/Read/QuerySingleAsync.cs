using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public Task<T> QuerySingleAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public Task<T> QuerySingleAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public Task<T> QuerySingleAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public Task<T> QuerySingleAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QuerySingleCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
    }
}
