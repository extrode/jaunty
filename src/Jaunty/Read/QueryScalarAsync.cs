using System.Data;

using Jaunty.Core;

namespace Jaunty.Read;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public Task<T> QueryScalarAsync<T>(string sql, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, null, default, cancellationToken);
        }

        public Task<T> QueryScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, parameters, default, cancellationToken);
        }

        public Task<T> QueryScalarAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, null, options, cancellationToken);
        }

        public Task<T> QueryScalarAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, parameters, options, cancellationToken);
        }
    }
}
