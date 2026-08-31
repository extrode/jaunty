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
        string columnName = GetColumnName<TFrom>(propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TJoin>(propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TFrom>(propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TJoin>(propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenBy<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TFrom>(propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TJoin>(propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TFrom>(propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnName<TJoin>(propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

        // AUD-R26-058: AUD-R25 replaced this linear scan plus per-reference re-escape with the
        // pre-escaped CachedDialectMetadata lookup across the where/exists/select visitors and the
        // arity-3 and arity-4 join visitors, and left this site on the old shape. EscapeColumnName
        // re-runs SqlIdentifierValidator's regex match and a keyword HashSet lookup on every column
        // reference of every query build; the cache does it once per (entity, dialect) pair.
    //
    // The helper is generic now rather than taking an EntityMetadata, because the cache is keyed by
    // (entity type, dialect) and every caller here already knows statically which of the two
    // entities it means - _fromMetadata is always TFrom and _joinMetadata always TJoin.
    private string GetColumnName<T>(string propertyName, string? tableAlias) where T : new()
    {
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
        CachedDialectMetadata cached = FluentMetadataCache.GetForDialect<T>(_dialect);

        string prefix = tableAlias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{cached.GetColumnName(propertyName)}";
    }
}
