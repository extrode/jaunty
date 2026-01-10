using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty.Read;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public T QueryFirst<T>(string sql) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, null, default, MappingMode.Strict);
        }

        public T QueryFirst<T>(string sql, object parameters) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, parameters, default, MappingMode.Strict);
        }

        public T QueryFirst<T>(string sql, CommandOptions<T> options) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, null, options, MappingMode.Strict);
        }

        public T QueryFirst<T>(string sql, object parameters, CommandOptions<T> options) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, parameters, options, MappingMode.Strict);
        }
    }
}
