using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public IEnumerable<T> QueryUnbuffered<T>(string sql) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Strict);
        }

        public IEnumerable<T> QueryUnbuffered<T>(string sql, object parameters) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Strict);
        }

        public IEnumerable<T> QueryUnbuffered<T>(string sql, CommandOptions<T> options) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Strict);
        }

        public IEnumerable<T> QueryUnbuffered<T>(string sql, object parameters, CommandOptions<T> options) where T : new()
        {
            return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Strict);
        }
    }
}
