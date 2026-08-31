using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// GROUP BY operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public IGroupedJoinedQuery<TFrom, TJoin, TKey> GroupBy<TKey>(Expression<Func<TFrom, TJoin, TKey>> keySelector)
    {
        return new GroupedJoinedQueryBuilder<TFrom, TJoin, TKey>(this, keySelector);
    }
}
