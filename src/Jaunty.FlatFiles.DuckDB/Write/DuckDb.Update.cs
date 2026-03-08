using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public int Update<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(column);

        var source = GetSourceOrThrow<T>();
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        var columnName = ExpressionTranslator.ResolveColumnName(column);

        var setParam = new DuckDBParameter { Value = value ?? DBNull.Value };
        var (whereSql, whereParams) = ExpressionTranslator.Translate<T>(predicate, paramOffset: 1);

        var allParams = new List<DuckDBParameter>(whereParams.Count + 1) { setParam };
        allParams.AddRange(whereParams);

        var sql = $"UPDATE \"{source.TableName}\" SET \"{columnName}\" = $1 WHERE {whereSql}";
        var result = NonQueryExecutor.Execute(_connection, sql, allParams);

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public int Update<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CommandOptions options) where T : class, new()
    {
        return Update(predicate, column, value);
    }
}
