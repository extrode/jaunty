using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty.Read;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public List<T> Query<T>(string sql) where T : new()
        {
            return QueryCore<T>(connection, sql, null, default, MappingMode.Strict);
        }

        public List<T> Query<T>(string sql, object parameters) where T : new()
        {
            return QueryCore<T>(connection, sql, parameters, default, MappingMode.Strict);
        }

        public List<T> Query<T>(string sql, CommandOptions<T> options) where T : new()
        {
            return QueryCore(connection, sql, null, options, MappingMode.Strict);
        }

        public List<T> Query<T>(string sql, object parameters, CommandOptions<T> options) where T : new()
        {
            return QueryCore(connection, sql, parameters, options, MappingMode.Strict);
        }
    }
}
