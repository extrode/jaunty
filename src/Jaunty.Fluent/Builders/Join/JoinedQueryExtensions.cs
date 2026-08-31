using System.Linq.Expressions;

using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Extension methods for multi-table joins.
/// </summary>
public static class JoinedQueryExtensions
{
    /// <summary>
    /// Adds a 4th table INNER JOIN to a 3-table join.
    /// </summary>
    public static IJoinClause<T1, T2, T3, T4> InnerJoin<T1, T2, T3, T4>(
        this IJoinedQuery3<T1, T2, T3> query,
        string? alias = null)
        where T1 : new()
        where T2 : new()
        where T3 : new()
        where T4 : new()
    {
        if (query is JoinedQuery3Builder<T1, T2, T3> builder)
            return new JoinClause4Builder<T1, T2, T3, T4>(builder, JoinType.Inner, alias);

        throw new NotSupportedException("Invalid query builder type.");
    }

    /// <summary>
    /// Adds a 4th table LEFT JOIN to a 3-table join.
    /// </summary>
    public static IJoinClause<T1, T2, T3, T4> LeftJoin<T1, T2, T3, T4>(
        this IJoinedQuery3<T1, T2, T3> query,
        string? alias = null)
        where T1 : new()
        where T2 : new()
        where T3 : new()
        where T4 : new()
    {
        if (query is JoinedQuery3Builder<T1, T2, T3> builder)
            return new JoinClause4Builder<T1, T2, T3, T4>(builder, JoinType.Left, alias);

        throw new NotSupportedException("Invalid query builder type.");
    }

    /// <summary>
    /// Adds a 4th table RIGHT JOIN to a 3-table join.
    /// </summary>
    public static IJoinClause<T1, T2, T3, T4> RightJoin<T1, T2, T3, T4>(
        this IJoinedQuery3<T1, T2, T3> query,
        string? alias = null)
        where T1 : new()
        where T2 : new()
        where T3 : new()
        where T4 : new()
    {
        if (query is JoinedQuery3Builder<T1, T2, T3> builder)
            return new JoinClause4Builder<T1, T2, T3, T4>(builder, JoinType.Right, alias);

        throw new NotSupportedException("Invalid query builder type.");
    }
}
