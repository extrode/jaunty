using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public T ExecuteScalar<T>(string sql)
        {
            return QueryScalarCore<T>(connection, sql, null, default);
        }

        public T ExecuteScalar<T>(string sql, object parameters)
        {
            return QueryScalarCore<T>(connection, sql, parameters, default);
        }

        public T ExecuteScalar<T>(string sql, CommandOptions<T> options)
        {
            return QueryScalarCore<T>(connection, sql, null, options);
        }

        public T ExecuteScalar<T>(string sql, object parameters, CommandOptions<T> options)
        {
            return QueryScalarCore<T>(connection, sql, parameters, options);
        }
    }
}
