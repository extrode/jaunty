using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var source = GetSourceOrThrow<T>();
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, cancellationToken).ConfigureAwait(false);

        var (whereSql, whereParams) = ExpressionTranslator.Translate<T>(predicate);

        var sql = $"DELETE FROM \"{source.TableName}\" WHERE {whereSql}";
        var result = await NonQueryExecutor.ExecuteAsync(_connection, sql, whereParams, cancellationToken).ConfigureAwait(false);

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await DeleteAsync(predicate, cancellationToken).ConfigureAwait(false);
    }
}
