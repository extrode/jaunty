using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken: cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken: cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken: cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken: cancellationToken);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return await QuerySingleCoreAsync<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Strict, null, default);
    }
}
