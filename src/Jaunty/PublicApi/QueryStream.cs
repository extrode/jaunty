using System.Data;

using Jaunty.InternalApi.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public IEnumerable<T> QueryStream<T>(string sql) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Strict);
        }

        public IEnumerable<T> QueryStream<T>(string sql, object parameters) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Strict);
        }

        public IEnumerable<T> QueryStream<T>(string sql, CommandOptions<T> options) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Strict);
        }

        public IEnumerable<T> QueryStream<T>(string sql, object parameters, CommandOptions<T> options) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Strict);
        }
    }
}
