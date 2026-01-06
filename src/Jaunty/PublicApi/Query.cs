using System.Data;

using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

namespace Jaunty;

public static partial class Jaunty
{
    public static List<T> Query<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryCore<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static List<T> Query<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return QueryCore<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, options, MappingMode.Strict);
    }
}