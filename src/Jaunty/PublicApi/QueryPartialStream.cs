using System.Data;

using Jaunty.InternalApi.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public IEnumerable<T> QueryPartialStream<T>(string sql) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Projection);
        }

        public IEnumerable<T> QueryPartialStream<T>(string sql, object parameters) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Projection);
        }

        public IEnumerable<T> QueryPartialStream<T>(string sql, CommandOptions<T> options) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Projection);
        }

        public IEnumerable<T> QueryPartialStream<T>(string sql, object parameters, CommandOptions<T> options) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Projection);
        }
    }
}
