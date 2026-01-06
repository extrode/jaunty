using System.Data;

using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

namespace Jaunty;

public static partial class Jaunty
{
    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
}
