using System.Data;
using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the fourth JOIN clause in a 4-table join.
/// </summary>
internal sealed class JoinClause4Builder<T1, T2, T3, T4> : IJoinClause<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly JoinedQuery3Builder<T1, T2, T3> _parent;
    private readonly JoinType _joinType;
    private readonly string? _alias;
    private readonly EntityMetadata _metadata;

    public JoinClause4Builder(JoinedQuery3Builder<T1, T2, T3> parent, JoinType joinType, string? alias)
    {
        _parent = parent;
        _joinType = joinType;
        _alias = alias;
        _metadata = FluentMetadataCache.GetMetadata<T4>();
    }

    public IJoinedQuery4<T1, T2, T3, T4> On<TLeftKey, TRightKey>(
        Expression<Func<T1, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T1>(), leftProp, _parent._parent.FromAlias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> OnFromSecond<TLeftKey, TRightKey>(
        Expression<Func<T2, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T2>(), leftProp, _parent._parent.Joins[0].Alias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> OnFromThird<TLeftKey, TRightKey>(
        Expression<Func<T3, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T3>(), leftProp, _parent._parent.Joins[1].Alias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> On(string leftColumn, string rightColumn) =>
        CreateJoinedQuery4($"{leftColumn} = {rightColumn}");

    public IJoinedQuery4<T1, T2, T3, T4> On(string condition) =>
        CreateJoinedQuery4(condition);

    private JoinedQuery4Builder<T1, T2, T3, T4> CreateJoinedQuery4(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _metadata.TableName,
            _metadata.SchemaName,
            _alias,
            onCondition);

        _parent._parent.AddJoin(joinInfo);
        return new JoinedQuery4Builder<T1, T2, T3, T4>(_parent);
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string escaped = _parent._parent.Dialect.EscapeColumnName(columnName);
        string prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

/// <summary>
/// Query builder for 4-table joins.
/// </summary>
internal sealed class JoinedQuery4Builder<T1, T2, T3, T4> : IJoinedQuery4<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly JoinedQuery3Builder<T1, T2, T3> _parent;

    public JoinedQuery4Builder(JoinedQuery3Builder<T1, T2, T3> parent) =>
        _parent = parent;

    public IJoinedQuery4<T1, T2, T3, T4> Where(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor4<T1, T2, T3, T4>(
            _parent._parent.Dialect,
            _parent._parent.FromAlias,
            _parent._parent.Joins[0].Alias,
            _parent._parent.Joins[1].Alias,
            _parent._parent.Joins[2].Alias);

        string sql = visitor.Translate(predicate);
        _parent._parent.AddWhereCondition(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> Where(string condition)
    {
        _parent._parent.AddWhereCondition(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public List<T1> Select() =>
        _parent._parent.Select();

    public List<(T1, T2, T3, T4)> SelectAll()
    {
        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();
        EntityMetadata t4Metadata = FluentMetadataCache.GetMetadata<T4>();

        string[] t1Columns = _parent._parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent._parent.FromAlias, "t1_");
        string[] t2Columns = _parent._parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent._parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent._parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent._parent.Joins[1].Alias, "t3_");
        string[] t4Columns = _parent._parent.GetPrefixedColumnsWithAlias(t4Metadata, _parent._parent.Joins[2].Alias, "t4_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).Concat(t4Columns).ToArray();

        string sql = _parent._parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3, T4)>();

        using IDbCommand command = _parent._parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent._parent.BindParameters(command);

        bool wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
        if (wasClosed)
            _parent._parent.Connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                T4? t4 = JoinedQueryBuilder<T1, T2>.MapEntity<T4>(t4Metadata, reader, "t4_");
                results.Add((t1, t2, t3, t4));
            }
        }
        finally
        {
            if (wasClosed)
                _parent._parent.Connection.Close();
        }

        return results;
    }

    public string ToSql() =>
        _parent._parent.ToSql();
}
