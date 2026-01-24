using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{

    public static GridReader QueryMultiple(this IDbConnection connection, string sql)
    {
        return ExecuteQueryMultiple(connection, sql, null, default);
    }

    public static GridReader QueryMultiple(this IDbConnection connection, string sql, object parameters)
    {
        return ExecuteQueryMultiple(connection, sql, parameters, default);
    }

    public static GridReader QueryMultiple(this IDbConnection connection, string sql, CommandOptions options)
    {
        return ExecuteQueryMultiple(connection, sql, null, options);
    }

    public static GridReader QueryMultiple(this IDbConnection connection, string sql, object parameters, CommandOptions options)
    {
        return ExecuteQueryMultiple(connection, sql, parameters, options);
    }

    public static void QueryMultiple(this IDbConnection connection, string sql, Action<GridReader> reader, object? parameters = null, CommandOptions options = default)
    {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        using var gridReader = ExecuteQueryMultiple(connection, sql, parameters, options);
        reader(gridReader);
    }

    public static TResult QueryMultiple<TResult>(this IDbConnection connection, string sql, Func<GridReader, TResult> reader, object? parameters = null, CommandOptions options = default)
    {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        using var gridReader = ExecuteQueryMultiple(connection, sql, parameters, options);
        return reader(gridReader);
    }
}
