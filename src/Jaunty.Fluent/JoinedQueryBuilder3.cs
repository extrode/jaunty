using System.Data;
using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the third JOIN clause in a 3-table join.
/// </summary>
internal sealed class JoinClause3Builder<T1, T2, T3> : IJoinClause<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    private readonly JoinedQueryBuilder<T1, T2> _parent;
    private readonly JoinType _joinType;
    private readonly string? _alias;
    private readonly EntityMetadata _metadata;

    public JoinClause3Builder(JoinedQueryBuilder<T1, T2> parent, JoinType joinType, string? alias)
    {
        _parent = parent;
        _joinType = joinType;
        _alias = alias;
        _metadata = FluentMetadataCache.GetMetadata<T3>();
    }

    public IJoinedQuery3<T1, T2, T3> On<TLeftKey, TRightKey>(Expression<Func<T1, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T1>(), leftProp, _parent.FromAlias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> OnFromSecond<TLeftKey, TRightKey>(Expression<Func<T2, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T2>(), leftProp, _parent.Joins[0].Alias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> On(string leftColumn, string rightColumn)
        => CreateJoinedQuery3($"{leftColumn} = {rightColumn}");

    public IJoinedQuery3<T1, T2, T3> On(string condition)
        => CreateJoinedQuery3(condition);

    private JoinedQuery3Builder<T1, T2, T3> CreateJoinedQuery3(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _metadata.TableName,
            _metadata.SchemaName,
            _alias,
            onCondition);

        _parent.AddJoin(joinInfo);
        return new JoinedQuery3Builder<T1, T2, T3>(_parent);
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string escaped = _parent.Dialect.EscapeColumnName(columnName);
        string prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

/// <summary>
/// Query builder for 3-table joins.
/// </summary>
internal sealed class JoinedQuery3Builder<T1, T2, T3> : IJoinedQuery3<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    internal readonly JoinedQueryBuilder<T1, T2> _parent;

    public JoinedQuery3Builder(JoinedQueryBuilder<T1, T2> parent) => _parent = parent;

    public IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor3<T1, T2, T3>(
            _parent.Dialect,
            _parent.FromAlias,
            _parent.Joins[0].Alias,
            _parent.Joins[1].Alias);

        string sql = visitor.Translate(predicate);
        _parent.AddWhereCondition(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> Where(string condition)
    {
        _parent.AddWhereCondition(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public List<T1> Select() => _parent.Select();

    public List<(T1, T2, T3)> SelectAll()
    {
        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();

        string[] t1Columns = _parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent.FromAlias, "t1_");
        string[] t2Columns = _parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent.Joins[1].Alias, "t3_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).ToArray();

        string sql = _parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3)>();

        using IDbCommand command = _parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed)
            _parent.Connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                results.Add((t1, t2, t3));
            }
        }
        finally
        {
            if (wasClosed)
                _parent.Connection.Close();
        }

        return results;
    }

    public string ToSql() => _parent.ToSql();
}
