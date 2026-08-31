using System.Data;
using System.Linq.Expressions;

using Jaunty.Core;

namespace Jaunty.Fluent;

/// <summary>
/// AUD-R35. <see cref="IPagedClause{T}"/> / <see cref="IPagedWhereClause{T}"/> are what
/// <c>Take</c> and <c>Skip</c> return, so that the write terminals stop compiling after paging.
/// Every member here forwards to the identical member on the un-paged interface and returns the
/// same builder - the shape of the query is unchanged, only its static type.
/// </summary>
internal sealed partial class QueryBuilder<T> : IPagedClause<T>, IPagedWhereClause<T>
    where T : new()
{
    private static NotSupportedException PagedWrite(string member) => new(
        $"{member} is not available after Take/Skip: the paging is not carried into the statement, " +
        "so it would affect every matching row rather than the paged subset. This is a compile " +
        "error in C#; reaching it means the interface was invoked dynamically or by reflection.");

    IPagedWhereClause<T> IPagedClause<T>.Where(string column, object? value)
    {
        ((IFromClause<T>)this).Where(column, value);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.Where(Expression<Func<T, bool>> predicate)
    {
        ((IFromClause<T>)this).Where(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereRaw(string rawSql)
    {
        ((IFromClause<T>)this).WhereRaw(rawSql);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereRaw(string rawSql, object parameters)
    {
        ((IFromClause<T>)this).WhereRaw(rawSql, parameters);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        ((IFromClause<T>)this).WhereIn<TValue>(selector, values);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        ((IFromClause<T>)this).WhereNotIn<TValue>(selector, values);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        ((IFromClause<T>)this).WhereBetween<TValue>(selector, from, to);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        ((IFromClause<T>)this).WhereNotBetween<TValue>(selector, from, to);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        ((IFromClause<T>)this).WhereExists<TSubquery>(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        ((IFromClause<T>)this).WhereNotExists<TSubquery>(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery)
    {
        ((IFromClause<T>)this).WhereInSubquery<TValue, TSubquery>(selector, subquerySelector, subquery);
        return this;
    }

    IPagedWhereClause<T> IPagedClause<T>.WhereNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery)
    {
        ((IFromClause<T>)this).WhereNotInSubquery<TValue, TSubquery>(selector, subquerySelector, subquery);
        return this;
    }

#pragma warning disable CS0618
    int IPagedClause<T>.DeleteAll()
        => throw PagedWrite("DeleteAll");
#pragma warning restore CS0618

#pragma warning disable CS0618
    int IPagedClause<T>.DeleteAll(CommandOptions options)
        => throw PagedWrite("DeleteAll");
#pragma warning restore CS0618

#pragma warning disable CS0618
    Task<int> IPagedClause<T>.DeleteAllAsync(CancellationToken cancellationToken)
        => throw PagedWrite("DeleteAllAsync");
#pragma warning restore CS0618

#pragma warning disable CS0618
    Task<int> IPagedClause<T>.DeleteAllAsync(CommandOptions options, CancellationToken cancellationToken)
        => throw PagedWrite("DeleteAllAsync");
#pragma warning restore CS0618

#pragma warning disable CS0618
    ISetClause<T> IPagedClause<T>.Set<TValue>(Expression<Func<T, TValue>> selector, TValue value)
        => throw PagedWrite("Set");
#pragma warning restore CS0618

#pragma warning disable CS0618
    ISetClause<T> IPagedClause<T>.Set(string column, object? value)
        => throw PagedWrite("Set");
#pragma warning restore CS0618

#pragma warning disable CS0618
    ISetClause<T> IPagedClause<T>.Set(object values)
        => throw PagedWrite("Set");
#pragma warning restore CS0618

    IPagedWhereClause<T> IPagedWhereClause<T>.And(string column, object? value)
    {
        ((IWhereClause<T>)this).And(column, value);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.And(Expression<Func<T, bool>> predicate)
    {
        ((IWhereClause<T>)this).And(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndRaw(string rawSql)
    {
        ((IWhereClause<T>)this).AndRaw(rawSql);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndRaw(string rawSql, object parameters)
    {
        ((IWhereClause<T>)this).AndRaw(rawSql, parameters);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.Or(string column, object? value)
    {
        ((IWhereClause<T>)this).Or(column, value);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.Or(Expression<Func<T, bool>> predicate)
    {
        ((IWhereClause<T>)this).Or(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrRaw(string rawSql)
    {
        ((IWhereClause<T>)this).OrRaw(rawSql);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrRaw(string rawSql, object parameters)
    {
        ((IWhereClause<T>)this).OrRaw(rawSql, parameters);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        ((IWhereClause<T>)this).AndIn<TValue>(selector, values);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        ((IWhereClause<T>)this).AndNotIn<TValue>(selector, values);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        ((IWhereClause<T>)this).OrIn<TValue>(selector, values);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        ((IWhereClause<T>)this).OrNotIn<TValue>(selector, values);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        ((IWhereClause<T>)this).AndBetween<TValue>(selector, from, to);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        ((IWhereClause<T>)this).AndNotBetween<TValue>(selector, from, to);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        ((IWhereClause<T>)this).OrBetween<TValue>(selector, from, to);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        ((IWhereClause<T>)this).OrNotBetween<TValue>(selector, from, to);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        ((IWhereClause<T>)this).AndExists<TSubquery>(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        ((IWhereClause<T>)this).AndNotExists<TSubquery>(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        ((IWhereClause<T>)this).OrExists<TSubquery>(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        ((IWhereClause<T>)this).OrNotExists<TSubquery>(predicate);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery)
    {
        ((IWhereClause<T>)this).AndInSubquery<TValue, TSubquery>(selector, subquerySelector, subquery);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.AndNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery)
    {
        ((IWhereClause<T>)this).AndNotInSubquery<TValue, TSubquery>(selector, subquerySelector, subquery);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery)
    {
        ((IWhereClause<T>)this).OrInSubquery<TValue, TSubquery>(selector, subquerySelector, subquery);
        return this;
    }

    IPagedWhereClause<T> IPagedWhereClause<T>.OrNotInSubquery<TValue, TSubquery>(Expression<Func<T, TValue>> selector, Expression<Func<TSubquery, TValue>> subquerySelector, IQueryTerminal<TSubquery> subquery)
    {
        ((IWhereClause<T>)this).OrNotInSubquery<TValue, TSubquery>(selector, subquerySelector, subquery);
        return this;
    }

#pragma warning disable CS0618
    int IPagedWhereClause<T>.Delete()
        => throw PagedWrite("Delete");
#pragma warning restore CS0618

#pragma warning disable CS0618
    int IPagedWhereClause<T>.Delete(CommandOptions options)
        => throw PagedWrite("Delete");
#pragma warning restore CS0618

#pragma warning disable CS0618
    Task<int> IPagedWhereClause<T>.DeleteAsync(CancellationToken cancellationToken)
        => throw PagedWrite("DeleteAsync");
#pragma warning restore CS0618

#pragma warning disable CS0618
    Task<int> IPagedWhereClause<T>.DeleteAsync(CommandOptions options, CancellationToken cancellationToken)
        => throw PagedWrite("DeleteAsync");
#pragma warning restore CS0618

}
