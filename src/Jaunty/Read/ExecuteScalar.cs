using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    public static T ExecuteScalar<T>(this IDbConnection connection, string sql)
    {
        return QueryScalarCore<T>(connection, sql, null, default);
    }

    public static T ExecuteScalar<T>(this IDbConnection connection, string sql, object parameters)
    {
        return QueryScalarCore<T>(connection, sql, parameters, default);
    }

    public static T ExecuteScalar<T>(this IDbConnection connection, string sql, CommandOptions<T> options)
    {
        return QueryScalarCore<T>(connection, sql, null, options);
    }

    public static T ExecuteScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options)
    {
        return QueryScalarCore<T>(connection, sql, parameters, options);
    }
}
