using System.Text;

namespace Extrode.Jaunty.Dialects;

/// <summary>
/// Upsert SQL for an entity whose key the database generates, for dialects whose native upsert
/// detects the row by a conflict on an inserted key (<c>ON CONFLICT</c>, <c>ON DUPLICATE KEY</c>).
/// </summary>
/// <remarks>
/// CrudSqlCache leaves an identity key out of the INSERT column list, so the native form never
/// conflicts on it and every call inserts a new row. This form matches SQL Server's MERGE instead:
/// it updates the row with the key if one exists, and otherwise inserts a row whose key the database
/// assigns. The two statements run in one command, so their affected-row counts add up to 1 either way.
/// </remarks>
internal static class GeneratedKeyUpsertSql
{
    /// <summary>
    /// True when a key column is missing from the INSERT columns, which is how
    /// <see cref="ISqlDialect.GenerateUpsertSql"/> receives a database-generated key.
    /// </summary>
    internal static bool Applies(string[] insertColumns, string[] keyColumns)
    {
        for (int i = 0; i < keyColumns.Length; i++)
        {
            if (Array.IndexOf(insertColumns, keyColumns[i]) < 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds <c>UPDATE ... WHERE key; INSERT ... SELECT ... WHERE NOT EXISTS (key)</c>. The UPDATE
    /// is left out when there is nothing to update. <c>fromDual</c> is appended after the SELECT
    /// list for dialects that need a FROM before WHERE (MySQL's <c>FROM DUAL</c>), and is empty for
    /// dialects that accept a FROM-less SELECT with a WHERE. The other parameters are those of
    /// <see cref="ISqlDialect.GenerateUpsertSql"/>.
    /// </summary>
    internal static string Build(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns,
        string[] keyParams,
        string fromDual)
    {
        var sb = new StringBuilder(256);

        if (updateColumns.Length > 0)
        {
            sb.Append("UPDATE ").Append(tableName).Append(" SET ");
            for (int i = 0; i < updateColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(updateColumns[i]).Append(" = ").Append(updateParams[i]);
            }

            AppendKeyMatch(sb, keyColumns, keyParams);
            sb.Append("; ");
        }

        sb.Append("INSERT INTO ").Append(tableName).Append(" (");
        for (int i = 0; i < insertColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertColumns[i]);
        }

        sb.Append(") SELECT ");
        for (int i = 0; i < insertParams.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertParams[i]);
        }

        sb.Append(fromDual).Append(" WHERE NOT EXISTS (SELECT 1 FROM ").Append(tableName);
        AppendKeyMatch(sb, keyColumns, keyParams);
        sb.Append(')');

        return sb.ToString();
    }

    private static void AppendKeyMatch(StringBuilder sb, string[] keyColumns, string[] keyParams)
    {
        sb.Append(" WHERE ");
        for (int i = 0; i < keyColumns.Length; i++)
        {
            if (i > 0) sb.Append(" AND ");
            sb.Append(keyColumns[i]).Append(" = ").Append(keyParams[i]);
        }
    }
}
