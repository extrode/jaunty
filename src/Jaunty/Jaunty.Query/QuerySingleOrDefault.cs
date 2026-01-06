using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QuerySingleOrDefaultCore<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QuerySingleOrDefaultCore<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql, CommandOptions options) where T : new()
    {
        return QuerySingleOrDefaultCore<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options) where T : new()
    {
        return QuerySingleOrDefaultCore<T>(connection, sql, parameters, options, MappingMode.Strict);
    }

    public static T? QuerySingleOrDefault<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return QuerySingleOrDefaultCore<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Strict);
    }
}
