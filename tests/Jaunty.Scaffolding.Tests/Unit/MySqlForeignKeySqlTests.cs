using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Providers.PostgreSql;
using Jaunty.Scaffolding.Providers.SqlServer;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R35-045/046. MySQL was the one reader whose foreign-key query neither ordered its rows nor
/// reported the referenced schema. No MySQL server is required for a CI run, so - as with the
/// existing PostgreSqlSchemaReaderTests - the query text itself is what is asserted.
/// </summary>
public class MySqlForeignKeySqlTests
{
    /// <summary>
    /// The columns of a composite foreign key pair positionally with the referenced ones. Without
    /// an ORDER BY the server may return them in any order and the pairing is luck.
    /// </summary>
    [Fact]
    public void ForeignKeysSql_OrdersByOrdinalPosition()
    {
        Assert.Contains("ORDER BY", MySqlSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
        Assert.Contains("ORDINAL_POSITION", MySqlSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ordering must group a table's constraints, not interleave them.
    /// </summary>
    [Fact]
    public void ForeignKeysSql_OrdersConstraintsTogether()
    {
        int order = MySqlSchemaReader.ForeignKeysSql.IndexOf("ORDER BY", StringComparison.Ordinal);

        Assert.True(order >= 0);
        Assert.Contains(
            "CONSTRAINT_NAME",
            MySqlSchemaReader.ForeignKeysSql[order..],
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The referenced schema was the literal '', so a key pointing into another database resolved
    /// against the current one.
    /// </summary>
    [Fact]
    public void ForeignKeysSql_ReportsTheReferencedSchema()
    {
        Assert.Contains("REFERENCED_TABLE_SCHEMA", MySqlSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
    }

    /// <summary>
    /// A same-database key still reports '', because that is what this reader reports as every
    /// table's own SchemaName - a key carrying a schema no table carries would match nothing.
    /// </summary>
    [Fact]
    public void ForeignKeysSql_CollapsesTheCurrentDatabaseToEmpty()
    {
        Assert.Contains("DATABASE()", MySqlSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
        Assert.Contains(
            "IF(REFERENCED_TABLE_SCHEMA = DATABASE(), '', REFERENCED_TABLE_SCHEMA)",
            MySqlSchemaReader.ForeignKeysSql,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The property that made this a divergence rather than an isolated omission: the other
    /// readers already ordered their foreign-key rows.
    /// </summary>
    [Fact]
    public void EveryReaderOrdersItsForeignKeyRows()
    {
        Assert.Contains("ORDER BY", PostgreSqlSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", SqlServerSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", MySqlSchemaReader.ForeignKeysSql, StringComparison.Ordinal);
    }
}
