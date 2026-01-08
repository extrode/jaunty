using System.Data;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public T QueryScalar<T>(string sql)
        {
            return QueryScalarCore<T>(connection, sql, null, default);
        }

        public T QueryScalar<T>(string sql, object parameters)
        {
            return QueryScalarCore<T>(connection, sql, parameters, default);
        }

        public T QueryScalar<T>(string sql, CommandOptions<T> options)
        {
            return QueryScalarCore<T>(connection, sql, null, options);
        }

        public T QueryScalar<T>(string sql, object parameters, CommandOptions<T> options)
        {
            return QueryScalarCore<T>(connection, sql, parameters, options);
        }
    }
}
