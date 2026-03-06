using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartial{T}(IDbConnection, string)"/>
    public static List<T> QueryPartial<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QueryPartial<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryPartial{T}(IDbConnection, string, object)"/>
    public static List<T> QueryPartial<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QueryPartial<T>(db.Connection, sql, parameters);
}
