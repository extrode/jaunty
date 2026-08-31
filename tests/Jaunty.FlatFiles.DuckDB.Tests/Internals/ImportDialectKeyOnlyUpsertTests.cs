using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// R27 batch 13 (medium). Upsert on an entity whose only mapped column is the key produced a
/// trailing "DO UPDATE SET " (SQLite/PostgreSQL) or "WHEN MATCHED THEN UPDATE SET " (SQL Server
/// MERGE) with no assignments - invalid SQL on all three targets. DuckDbDialect already guarded
/// this by degrading to DO NOTHING; the import dialects now do the same (SQL Server omits the
/// WHEN MATCHED clause).
/// </summary>
public class ImportDialectKeyOnlyUpsertTests
{
    [Fact]
    public void SqliteImportDialect_Upsert_KeyOnlyEntity_EmitsDoNothing()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "items", ["id"], ["@p0"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.EndsWith("ON CONFLICT (\"id\") DO NOTHING", sql);
        Assert.DoesNotContain("DO UPDATE", sql);
    }

    [Fact]
    public void PostgreSqlImportDialect_Upsert_KeyOnlyEntity_EmitsDoNothing()
    {
        string sql = PostgreSqlImportDialect.Instance.GenerateInsertSql(
            "items", ["id"], ["@p0"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.EndsWith("ON CONFLICT (\"id\") DO NOTHING", sql);
        Assert.DoesNotContain("DO UPDATE", sql);
    }

    [Fact]
    public void SqlServerImportDialect_Upsert_KeyOnlyEntity_OmitsWhenMatchedClause()
    {
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            "items", ["id"], ["@p0"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.DoesNotContain("WHEN MATCHED", sql);
        Assert.Contains("WHEN NOT MATCHED THEN INSERT", sql);
    }

    [Fact]
    public void SqliteImportDialect_Upsert_WithNonKeyColumns_StillEmitsUpdate()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "items", ["id", "name"], ["@p0", "@p1"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.Contains("DO UPDATE SET \"name\" = excluded.\"name\"", sql);
    }

    [Fact]
    public void PostgreSqlImportDialect_Upsert_WithNonKeyColumns_StillEmitsUpdate()
    {
        string sql = PostgreSqlImportDialect.Instance.GenerateInsertSql(
            "items", ["id", "name"], ["@p0", "@p1"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.Contains("DO UPDATE SET \"name\" = EXCLUDED.\"name\"", sql);
    }

    [Fact]
    public void SqlServerImportDialect_Upsert_WithNonKeyColumns_StillEmitsWhenMatched()
    {
        string sql = SqlServerImportDialect.Instance.GenerateInsertSql(
            "items", ["id", "name"], ["@p0", "@p1"], ConflictStrategy.Upsert, keyColumnName: "id");

        Assert.Contains("WHEN MATCHED THEN UPDATE SET target.[name] = source.[name]", sql);
    }
}
