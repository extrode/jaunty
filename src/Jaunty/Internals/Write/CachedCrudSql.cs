using Jaunty.Internals.Entity;

namespace Jaunty.Internals.Write;

/// <summary>
/// Holds cached SQL statements and metadata for a specific entity type and dialect.
/// </summary>
internal sealed class CachedCrudSql
{
    public string InsertSql { get; }
    public string UpdateSql { get; }
    public string DeleteSql { get; }
    public string DeleteByIdSql { get; }
    public string UpsertSql { get; }
    public string LastInsertIdSql { get; }
    public string SelectByIdSql { get; }
    public string SelectAllSql { get; }
    public EntityMetadata Metadata { get; }

    public bool HasIdentityKey { get; }
    public bool HasPrimaryKey { get; }
    public bool HasSinglePrimaryKey { get; }
    public bool SupportsUpsert { get; }

    /// <summary>
    /// Pre-composed INSERT command text (INSERT + identity retrieval SQL), cached to avoid per-call string concatenation.
    /// </summary>
    public string InsertCommandText { get; }

    /// <summary>
    /// Describes a by-id call's single parameter for the interceptor pipeline and
    /// <see cref="Configuration.JauntyConfig.Logger"/>, under the name the command actually uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. The by-id read and delete paths reported a synthetic <c>new { Id = id }</c> while
    /// binding <c>"@" + primaryKey.ColumnName</c>. For any entity whose key column is not literally
    /// called <c>Id</c> - <c>ProductID</c>, <c>order_id</c>, <c>CustomerCode</c> - the audit record
    /// and the log line named a parameter that does not appear in the statement, while the one that
    /// does was not reported. The SQL handed alongside it is the real <c>SelectByIdSql</c>/
    /// <c>DeleteByIdSql</c>, so the record contradicted itself: <c>WHERE "ProductID" = @ProductID</c>
    /// beside a parameter called <c>Id</c>. Same "what interceptors are actually told" theme as
    /// AUD-R26-020 and -026.
    /// </para>
    /// <para>
    /// A dictionary rather than an anonymous type because the name is only known at runtime;
    /// <c>ParameterBinder</c> already treats <c>IDictionary&lt;string, object?&gt;</c> as a
    /// first-class parameter set, so an interceptor can hand this straight back to Jaunty.
    /// The column name carries no dialect prefix, matching the property-name convention an
    /// anonymous parameter object would have used.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DescribeIdParameter(object? id)
    {
        // Reporting must never be the thing that throws: the by-id callers all check
        // SelectByIdSql/DeleteByIdSql first, which implies a single key, but an empty key list
        // here should degrade to the old name rather than take down the call it is describing.
        string name = Metadata.PrimaryKeys.Count > 0 ? Metadata.PrimaryKeys[0].ColumnName : "Id";

        return new Dictionary<string, object?>(1, StringComparer.OrdinalIgnoreCase) { [name] = id };
    }

    public CachedCrudSql(string insertSql, string updateSql, string deleteSql, string deleteByIdSql, string upsertSql, string lastInsertIdSql, EntityMetadata metadata, bool supportsUpsert)
        : this(insertSql, updateSql, deleteSql, deleteByIdSql, upsertSql, lastInsertIdSql, string.Empty, string.Empty, metadata, supportsUpsert)
    {
    }

    public CachedCrudSql(string insertSql, string updateSql, string deleteSql, string deleteByIdSql, string upsertSql, string lastInsertIdSql, string selectByIdSql, string selectAllSql, EntityMetadata metadata, bool supportsUpsert)
    {
        InsertSql = insertSql;
        UpdateSql = updateSql;
        DeleteSql = deleteSql;
        DeleteByIdSql = deleteByIdSql;
        UpsertSql = upsertSql;
        LastInsertIdSql = lastInsertIdSql;
        SelectByIdSql = selectByIdSql;
        SelectAllSql = selectAllSql;
        Metadata = metadata;
        SupportsUpsert = supportsUpsert;

        HasPrimaryKey = metadata.PrimaryKeys.Count > 0;
        HasSinglePrimaryKey = metadata.PrimaryKeys.Count == 1;
        HasIdentityKey = HasSinglePrimaryKey && metadata.PrimaryKeys[0].IsIdentity;

        InsertCommandText = ComposeInsertCommandText(insertSql, lastInsertIdSql, HasIdentityKey);
    }

    private static string ComposeInsertCommandText(string insertSql, string lastInsertIdSql, bool hasIdentityKey)
    {
        if (!hasIdentityKey)
            return insertSql;

        string? trimmed = lastInsertIdSql?.TrimStart();

        if (string.IsNullOrEmpty(trimmed))
            return insertSql;

        return trimmed!.StartsWith("RETURNING", StringComparison.OrdinalIgnoreCase)
            ? $"{insertSql} {lastInsertIdSql}"
            : $"{insertSql}; {lastInsertIdSql}";
    }
}
