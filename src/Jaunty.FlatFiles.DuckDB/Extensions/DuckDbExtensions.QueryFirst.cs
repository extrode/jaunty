using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryFirst{T}(IDbConnection, string)"/>
    public static T QueryFirst<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QueryFirst<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryFirst{T}(IDbConnection, string, object)"/>
    public static T QueryFirst<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QueryFirst<T>(db.Connection, sql, parameters);
}
