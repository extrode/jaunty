using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Enums;

namespace Jaunty.Fluent;

/// <summary>
/// Where clause operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public IJoinedQuery<TFrom, TJoin> Where(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _dialect,
            _fromAlias,
            _joins[0].Alias);

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        _parameters.AddRange(parameters);
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Where(string condition)
    {
        _conditions.Add(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Where(string column, object value)
    {
        string paramName = $"{_dialect.ParameterPrefix}{column.Replace(".", "_")}";
        _conditions.Add(WhereCondition.Column($"{column} = {paramName}", LogicalOperator.None));
        _parameters.Add(paramName, value);
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> And(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(_dialect, _fromAlias, _joins[0].Alias);
        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        _parameters.AddRange(parameters);
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Or(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(_dialect, _fromAlias, _joins[0].Alias);
        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        _parameters.AddRange(parameters);
        return this;
    }
}
