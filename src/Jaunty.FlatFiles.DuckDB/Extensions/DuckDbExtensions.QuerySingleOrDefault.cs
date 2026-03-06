using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QuerySingleOrDefault{T}(IDbConnection, string)"/>
    public static T? QuerySingleOrDefault<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QuerySingleOrDefault<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QuerySingleOrDefault{T}(IDbConnection, string, object)"/>
    public static T? QuerySingleOrDefault<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QuerySingleOrDefault<T>(db.Connection, sql, parameters);
}
