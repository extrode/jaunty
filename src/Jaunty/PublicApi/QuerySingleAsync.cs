using System.Data;

using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

namespace Jaunty;

public static partial class Jaunty
{
    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
}
