using System.Data;

using Jaunty.Interfaces;
using Jaunty.Internal.Execution;
using Jaunty.Readers;

namespace Jaunty;

public static partial class Jaunty
{
    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql)
    {
        return QueryInternal<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object parameters)
    {
        return QueryInternal<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, CommandOptions options)
    {
        return QueryInternal<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options)
    {
        return QueryInternal<T>(connection, sql, parameters, options, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest)
    {
        return QueryInternal<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Strict);
    }
}
