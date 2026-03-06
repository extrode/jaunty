using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartialFirstOrDefault{T}(IDbConnection, string)"/>
    public static T? QueryPartialFirstOrDefault<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QueryPartialFirstOrDefault<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryPartialFirstOrDefault{T}(IDbConnection, string, object)"/>
    public static T? QueryPartialFirstOrDefault<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QueryPartialFirstOrDefault<T>(db.Connection, sql, parameters);
}
