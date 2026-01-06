using System.Data;

namespace Jaunty;

public static partial class Jaunty
{
    public static T QueryScalar<T>(this IDbConnection connection, string sql)
    {
        return QueryScalarCore<T>(connection, sql, null, default);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters)
    {
        return QueryScalarCore<T>(connection, sql, parameters, default);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, CommandOptions<T> options)
    {
        return QueryScalarCore<T>(connection, sql, null, options);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options)
    {
        return QueryScalarCore<T>(connection, sql, parameters, options);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest)
    {
        return QueryScalarCore<T>(connection, sql, CombineParams(param1, param2, rest), default);
    }
}
