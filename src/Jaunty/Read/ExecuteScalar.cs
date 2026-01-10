using System.Data;

using Jaunty.Core;

namespace Jaunty.Read;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        [Obsolete("Use QueryScalar<T> instead. This method will be removed in a future version.")]
        public T ExecuteScalar<T>(string sql)
        {
            return QueryScalarCore<T>(connection, sql, null, default);
        }

        [Obsolete("Use QueryScalar<T> instead. This method will be removed in a future version.")]
        public T ExecuteScalar<T>(string sql, object parameters)
        {
            return QueryScalarCore<T>(connection, sql, parameters, default);
        }

        [Obsolete("Use QueryScalar<T> instead. This method will be removed in a future version.")]
        public T ExecuteScalar<T>(string sql, CommandOptions<T> options)
        {
            return QueryScalarCore<T>(connection, sql, null, options);
        }

        [Obsolete("Use QueryScalar<T> instead. This method will be removed in a future version.")]
        public T ExecuteScalar<T>(string sql, object parameters, CommandOptions<T> options)
        {
            return QueryScalarCore<T>(connection, sql, parameters, options);
        }
    }
}
