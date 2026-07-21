using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// OrderBy operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public IJoinedQuery<TFrom, TJoin> OrderBy<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenBy<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? tableAlias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string prefix = tableAlias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{_dialect.EscapeColumnName(columnName)}";
    }
}
