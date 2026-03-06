using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QuerySingle{T}(IDbConnection, string)"/>
    public static T QuerySingle<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QuerySingle<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QuerySingle{T}(IDbConnection, string, object)"/>
    public static T QuerySingle<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QuerySingle<T>(db.Connection, sql, parameters);
}
