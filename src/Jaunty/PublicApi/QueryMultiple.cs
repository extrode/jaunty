using System.Data;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public GridReader QueryMultiple(string sql)
        {
            return ExecuteQueryMultiple(connection, sql, null, default);
        }

        public GridReader QueryMultiple(string sql, object parameters)
        {
            return ExecuteQueryMultiple(connection, sql, parameters, default);
        }

        public GridReader QueryMultiple(string sql, CommandOptions options)
        {
            return ExecuteQueryMultiple(connection, sql, null, options);
        }

        public GridReader QueryMultiple(string sql, object parameters, CommandOptions options)
        {
            return ExecuteQueryMultiple(connection, sql, parameters, options);
        }

        public void QueryMultiple(string sql, Action<GridReader> reader, object? parameters = null, CommandOptions options = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
            if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
            var gridReader = ExecuteQueryMultiple(connection, sql, parameters, options);
            reader(gridReader);
        }

        public TResult QueryMultiple<TResult>(string sql, Func<GridReader, TResult> reader, object? parameters = null, CommandOptions options = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
            if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
            var gridReader = ExecuteQueryMultiple(connection, sql, parameters, options);
            return reader(gridReader);
        }
    }
}
