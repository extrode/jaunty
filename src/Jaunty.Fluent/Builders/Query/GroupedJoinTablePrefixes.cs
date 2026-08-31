using Jaunty.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Resolves the table-qualifying prefix a grouped-join column reference is emitted with.
/// </summary>
/// <remarks>
/// AUD-R35-015. The three <c>GroupedJoinedQueryBuilder</c> constructors built their
/// <c>tablePrefixes</c> array as <c>alias ?? metadata.TableName</c>, so with no alias every GROUP
/// BY key and every aggregate column came out as <c>raw_table.[escaped_column]</c> while
/// <c>JoinedQueryBuilder.BuildFromJoinWhereSql</c> named the same table through
/// <see cref="ISqlDialect.EscapeTableName"/> in the FROM and JOIN clauses. A table whose name needs
/// escaping - a reserved word, or a space, as in Northwind's <c>Order Details</c> - therefore
/// produced a syntax error on the grouped path and only on the grouped path, and dropped schema
/// qualification besides.
/// <para>
/// This is the <c>alias ?? metadata.TableName</c> defect round 17 fixed across
/// <c>JoinClauseBuilder</c>, <c>JoinedQueryBuilder.GetPrefixedColumns(WithAlias)</c>,
/// <c>JoinedQueryBuilderOrderBy</c>, <c>JoinedQueryBuilder3/4</c> and <c>JoinClause4Builder</c>;
/// the grouped-joined trio was never converted. Putting the resolution in one place means the next
/// builder to need it cannot reintroduce the raw form by copying a neighbour.
/// </para>
/// </remarks>
internal static class GroupedJoinTablePrefixes
{
    /// <summary>
    /// The alias when one was supplied, otherwise the dialect-escaped, schema-qualified table name.
    /// </summary>
    /// <param name="dialect">The dialect the statement is being built for.</param>
    /// <param name="alias">The alias the caller gave this table, or <c>null</c>.</param>
    /// <param name="metadata">The table's metadata, for its schema and table name.</param>
    public static string Resolve(ISqlDialect dialect, string? alias, EntityMetadata metadata) =>
        alias ?? dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
}
