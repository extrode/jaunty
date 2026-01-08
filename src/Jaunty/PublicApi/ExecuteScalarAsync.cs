using System.Data;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        [Obsolete("Use QueryScalarAsync<T> instead. This method will be removed in a future version.")]
        public Task<T> ExecuteScalarAsync<T>(string sql, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, null, default, cancellationToken);
        }

        [Obsolete("Use QueryScalarAsync<T> instead. This method will be removed in a future version.")]
        public Task<T> ExecuteScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, parameters, default, cancellationToken);
        }

        [Obsolete("Use QueryScalarAsync<T> instead. This method will be removed in a future version.")]
        public Task<T> ExecuteScalarAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, null, options, cancellationToken);
        }

        [Obsolete("Use QueryScalarAsync<T> instead. This method will be removed in a future version.")]
        public Task<T> ExecuteScalarAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
        {
            return QueryScalarCoreAsync<T>(connection, sql, parameters, options, cancellationToken);
        }
    }
}
