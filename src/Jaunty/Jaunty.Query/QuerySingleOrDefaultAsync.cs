using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql) where T : new()
    {
        return await QuerySingleOrDefaultCoreAsync<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return await QuerySingleOrDefaultCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return await QuerySingleOrDefaultCoreAsync<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return await QuerySingleOrDefaultCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict);
    }
}
