using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static IEnumerable<T> QueryUnbuffered<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static IEnumerable<T> QueryUnbuffered<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static IEnumerable<T> QueryUnbuffered<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static IEnumerable<T> QueryUnbuffered<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Strict);
    }
}
