using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryScalar{T}(IDbConnection, string)"/>
    public static T QueryScalar<T>(this DuckDb db, string sql)
        => Jaunty.QueryScalar<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryScalar{T}(IDbConnection, string, object)"/>
    public static T QueryScalar<T>(this DuckDb db, string sql, object parameters)
        => Jaunty.QueryScalar<T>(db.Connection, sql, parameters);
}
