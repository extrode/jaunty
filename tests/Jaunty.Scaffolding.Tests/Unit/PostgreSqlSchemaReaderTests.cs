using Jaunty.Scaffolding.Providers.PostgreSql;
using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

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
        Assert.Contains("tc.table_schema = @SchemaName", PostgreSqlSchemaReader.PrimaryKeysSql);
        Assert.Contains("tc.table_name = @TableName", PostgreSqlSchemaReader.PrimaryKeysSql);
    }

    [Fact]
    public void ForeignKeysSql_FiltersByBothSchemaAndTableName()
    {
        Assert.Contains("tc.table_schema = @SchemaName", PostgreSqlSchemaReader.ForeignKeysSql);
        Assert.Contains("tc.table_name = @TableName", PostgreSqlSchemaReader.ForeignKeysSql);
    }

    // R27 batch 15: constraint_column_usage has no ordinal, so a composite FK cross-produced
    // N*N rows. The referenced columns must come from the unique constraint's key_column_usage
    // matched on position_in_unique_constraint.
    [Fact]
    public void ForeignKeysSql_CorrelatesReferencedColumnsPositionally()
    {
        Assert.Contains("position_in_unique_constraint", PostgreSqlSchemaReader.ForeignKeysSql);
        Assert.Contains("referential_constraints", PostgreSqlSchemaReader.ForeignKeysSql);
        Assert.DoesNotContain("constraint_column_usage", PostgreSqlSchemaReader.ForeignKeysSql);
    }
}
