using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty.Read;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
        }

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
        }

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
        }

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryFirstOrDefaultCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
        }
    }
}
