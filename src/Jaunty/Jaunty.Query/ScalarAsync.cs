using System.Data;

namespace Jaunty;

public static partial class Jaunty
{
    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, null, default, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, parameters, default, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, null, options, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, parameters, options, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest)
    {
        return QueryScalarCoreAsync<T>(connection, sql, CombineParams(param1, param2, rest), default, default);
    }
}
