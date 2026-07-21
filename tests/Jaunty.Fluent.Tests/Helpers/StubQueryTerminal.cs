using System.Linq.Expressions;

namespace Jaunty.Fluent.Tests.Helpers;

/// <summary>
/// Minimal IQueryTerminal&lt;T&gt; implementation for exercising how WhereInSubquery/
/// WhereNotInSubquery handle a custom (non-QueryBuilder) subquery implementation. Only
/// ToSql() is meaningful - the code under test only calls ToSql() before deciding whether
/// to proceed or throw, so every other member throws NotImplementedException.
/// </summary>
public class StubQueryTerminal<T> : IQueryTerminal<T> where T : new()
{
    private readonly string _sql;

    public StubQueryTerminal(string sql) => _sql = sql;

    public string ToSql() => _sql;
    public string ToSql(params string[] columns) => _sql;
    public string ToSql(params Expression<Func<T, object?>>[] columns) => _sql;
    public string ToSql<TResult>(Expression<Func<T, TResult>> selector) => _sql;

    public List<T> Select() => throw new NotImplementedException();
    public T SelectFirst() => throw new NotImplementedException();
    public T? SelectFirstOrDefault() => throw new NotImplementedException();
    public T SelectSingle() => throw new NotImplementedException();
    public T? SelectSingleOrDefault() => throw new NotImplementedException();
    public List<T> SelectPartial(params string[] columns) => throw new NotImplementedException();
    public T SelectPartialFirst(params string[] columns) => throw new NotImplementedException();
    public T? SelectPartialFirstOrDefault(params string[] columns) => throw new NotImplementedException();
    public T SelectPartialSingle(params string[] columns) => throw new NotImplementedException();
    public T? SelectPartialSingleOrDefault(params string[] columns) => throw new NotImplementedException();
    public List<T> SelectPartial(params Expression<Func<T, object?>>[] columns) => throw new NotImplementedException();
    public T SelectPartialFirst(params Expression<Func<T, object?>>[] columns) => throw new NotImplementedException();
    public T? SelectPartialFirstOrDefault(params Expression<Func<T, object?>>[] columns) => throw new NotImplementedException();
    public T SelectPartialSingle(params Expression<Func<T, object?>>[] columns) => throw new NotImplementedException();
    public T? SelectPartialSingleOrDefault(params Expression<Func<T, object?>>[] columns) => throw new NotImplementedException();

    public int Count() => throw new NotImplementedException();
    public long LongCount() => throw new NotImplementedException();
    public int Count<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public long LongCount<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public TResult Sum<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public double Avg<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public TResult Min<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public TResult Max<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public int SelectCount() => throw new NotImplementedException();
    public int SelectCount<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public TResult SelectSum<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public double SelectAvg<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public TResult SelectMin<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();
    public TResult SelectMax<TResult>(Expression<Func<T, TResult>> selector) => throw new NotImplementedException();

    public Task<List<T>> SelectAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T> SelectFirstAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T> SelectSingleAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T> SelectPartialFirstAsync(string[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T?> SelectPartialFirstOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T> SelectPartialSingleAsync(string[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T?> SelectPartialSingleOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T> SelectPartialFirstAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T?> SelectPartialFirstOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T> SelectPartialSingleAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<T?> SelectPartialSingleOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<long> LongCountAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<long> LongCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<double> AvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<int> SelectCountAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<int> SelectCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResult> SelectSumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<double> SelectAvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResult> SelectMinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TResult> SelectMaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public ISetOperationClause<T> Union(IQueryTerminal<T> other) => throw new NotImplementedException();
    public ISetOperationClause<T> UnionAll(IQueryTerminal<T> other) => throw new NotImplementedException();
    public ISetOperationClause<T> Except(IQueryTerminal<T> other) => throw new NotImplementedException();
    public ISetOperationClause<T> Intersect(IQueryTerminal<T> other) => throw new NotImplementedException();
}
