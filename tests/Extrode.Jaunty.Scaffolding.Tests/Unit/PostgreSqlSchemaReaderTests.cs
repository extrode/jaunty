using Extrode.Jaunty.Scaffolding.Providers.PostgreSql;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// Regression guard for AUD-R9-005: the primary-key and foreign-key queries must filter by
/// both table_schema and table_name, otherwise same-named tables in different schemas bleed
/// each other's key columns into the returned schema.
/// </summary>
public class PostgreSqlSchemaReaderTests
{
    [Fact]
    public void PrimaryKeysSql_FiltersByBothSchemaAndTableName()
    {
        Assert.Contains("n.nspname = @SchemaName", PostgreSqlSchemaReader.PrimaryKeysSql);
        Assert.Contains("cl.relname = @TableName", PostgreSqlSchemaReader.PrimaryKeysSql);
    }

    [Fact]
    public void ForeignKeysSql_FiltersByBothSchemaAndTableName()
    {
        Assert.Contains("n.nspname = @SchemaName", PostgreSqlSchemaReader.ForeignKeysSql);
        Assert.Contains("cl.relname = @TableName", PostgreSqlSchemaReader.ForeignKeysSql);
    }

    // R27 batch 15: constraint_column_usage has no ordinal, so a composite FK cross-produced
    // N*N rows. AUD-R38-046: the referenced columns now come from confkey, unnested alongside
    // conkey so each pair shares a position.
    [Fact]
    public void ForeignKeysSql_CorrelatesReferencedColumnsPositionally()
    {
        Assert.Contains("unnest(con.conkey, con.confkey) WITH ORDINALITY", PostgreSqlSchemaReader.ForeignKeysSql);
        Assert.DoesNotContain("constraint_column_usage", PostgreSqlSchemaReader.ForeignKeysSql);
    }

    [Theory]
    [InlineData(PostgreSqlSchemaReader.PrimaryKeysSql)]
    [InlineData(PostgreSqlSchemaReader.ForeignKeysSql)]
    public void KeyQueries_ReadPgCatalogNotTheSelectFilteredInformationSchema(string sql)
    {
        Assert.Contains("pg_catalog.pg_constraint", sql);
        Assert.DoesNotContain("information_schema", sql);
    }
}
