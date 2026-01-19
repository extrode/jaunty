using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents the WHERE clause - allows chaining additional conditions.
/// </summary>
public interface IWhereClause<T> : IQueryTerminal<T> where T : new()
{
    // AND conditions
    IWhereClause<T> And(string column, object? value);
    IWhereClause<T> And(Expression<Func<T, bool>> predicate);
    IWhereClause<T> AndRaw(string rawSql);
    IWhereClause<T> AndRaw(string rawSql, object parameters);

    // OR conditions
    IWhereClause<T> Or(string column, object? value);
    IWhereClause<T> Or(Expression<Func<T, bool>> predicate);
    IWhereClause<T> OrRaw(string rawSql);
    IWhereClause<T> OrRaw(string rawSql, object parameters);

    // ORDER BY - expression-based
    IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector);
    IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector);

    // ORDER BY - string-based
    IOrderByClause<T> OrderBy(string column);
    IOrderByClause<T> OrderByDescending(string column);

    // TOP/LIMIT
    IWhereClause<T> Take(int count);
    IWhereClause<T> Skip(int count);
}
