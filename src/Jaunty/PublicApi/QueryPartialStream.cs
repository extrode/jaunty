using System.Data;

using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

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

    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    public static IEnumerable<T> QueryPartialStream<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }
}
