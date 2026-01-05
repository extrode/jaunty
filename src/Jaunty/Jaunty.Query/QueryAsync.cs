using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static async Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return await QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, null, cancellationToken);
    }

    public static async Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return await QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, null, cancellationToken);
    }

    public static async Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return await QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, null, cancellationToken);
    }

    public static async Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return await QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, null, cancellationToken);
    }
}