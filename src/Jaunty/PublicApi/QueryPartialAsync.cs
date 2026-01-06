using System.Data;

using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

namespace Jaunty;

public static partial class Jaunty
{
    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }
}
