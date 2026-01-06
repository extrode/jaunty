using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryFirstOrDefaultCore<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryFirstOrDefaultCore<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return QueryFirstOrDefaultCore<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static T? QueryFirstOrDefault<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return QueryFirstOrDefaultCore<T>(connection, sql, parameters, options, MappingMode.Strict);
    }
}
