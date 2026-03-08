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
    public EntityMetadata Metadata { get; }

    public bool HasIdentityKey { get; }
    public bool HasPrimaryKey { get; }
    public bool HasSinglePrimaryKey { get; }
    public bool SupportsUpsert { get; }

    /// <summary>
    /// Pre-composed INSERT command text (INSERT + identity retrieval SQL), cached to avoid per-call string concatenation.
    /// </summary>
    public string InsertCommandText { get; }

    public CachedCrudSql(string insertSql, string updateSql, string deleteSql, string deleteByIdSql, string upsertSql, string lastInsertIdSql, EntityMetadata metadata, bool supportsUpsert)
    {
        InsertSql = insertSql;
        UpdateSql = updateSql;
        DeleteSql = deleteSql;
        DeleteByIdSql = deleteByIdSql;
        UpsertSql = upsertSql;
        LastInsertIdSql = lastInsertIdSql;
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

        var trimmed = lastInsertIdSql?.TrimStart();
        if (!string.IsNullOrEmpty(trimmed) &&
            trimmed!.StartsWith("RETURNING", StringComparison.OrdinalIgnoreCase))
        {
            return $"{insertSql} {lastInsertIdSql}";
        }

        return $"{insertSql}; {lastInsertIdSql}";
    }
}