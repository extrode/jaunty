using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public T QueryPartialFirst<T>(string sql) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, null, default, MappingMode.Projection);
        }

        public T QueryPartialFirst<T>(string sql, object parameters) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, parameters, default, MappingMode.Projection);
        }

        public T QueryPartialFirst<T>(string sql, CommandOptions<T> options) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, null, options, MappingMode.Projection);
        }

        public T QueryPartialFirst<T>(string sql, object parameters, CommandOptions<T> options) where T : new()
        {
            return QueryFirstCore<T>(connection, sql, parameters, options, MappingMode.Projection);
        }
    }
}
