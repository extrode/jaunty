using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public int Delete<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var source = GetSourceOrThrow<T>();
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        var (whereSql, whereParams) = ExpressionTranslator.Translate<T>(predicate);

        var sql = $"DELETE FROM \"{source.TableName}\" WHERE {whereSql}";
        var result = NonQueryExecutor.Execute(_connection, sql, whereParams);

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public int Delete<T>(Expression<Func<T, bool>> predicate, CommandOptions options) where T : class, new()
    {
        return Delete(predicate);
    }
}
