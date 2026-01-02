using System.Data;
using System.Data.Common;

using Jaunty.Internal.Execution;

namespace Jaunty;

public static partial class Jaunty
{
    public static T QueryScalar<T>(this IDbConnection connection, string sql)
    {
        return QueryScalarCore<T>(connection, sql, null, default);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters)
    {
        return QueryScalarCore<T>(connection, sql, parameters, default);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, CommandOptions options)
    {
        return QueryScalarCore<T>(connection, sql, null, options);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options)
    {
        return QueryScalarCore<T>(connection, sql, parameters, options);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest)
    {
        return QueryScalarCore<T>(connection, sql, CombineParams(param1, param2, rest), default);
    }

    internal static T QueryScalarCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        return CommandExecutor.ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader =>
        {
            if (reader is DbDataReader dbReader)
                return !dbReader.Read() || dbReader.IsDBNull(0) ? default! : dbReader.GetFieldValue<T>(0);

            if (!reader.Read() || reader.IsDBNull(0)) return default!;

            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        });
    }
}
