using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql, CommandOptions options) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }

    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Projection);
    }
}
