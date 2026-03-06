using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryFirstOrDefault{T}(IDbConnection, string)"/>
    public static T? QueryFirstOrDefault<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QueryFirstOrDefault<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryFirstOrDefault{T}(IDbConnection, string, object)"/>
    public static T? QueryFirstOrDefault<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QueryFirstOrDefault<T>(db.Connection, sql, parameters);
}
