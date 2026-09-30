using Extrode.Jaunty.Dialects;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

public class DialectSurvivorTests
{
    [Fact]
    public void SQLite_UsesTheAtPrefixAndMultiRowInsert()
    {
        var dialect = new SQLiteDialect();

        Assert.Equal("@", dialect.ParameterPrefix);
        Assert.True(dialect.SupportsMultiRowInsert);
        Assert.Null(dialect.CreateBulkCopyProvider());
    }

    [Fact]
    public void MySql_CreatesNoBulkCopyProvider()
        => Assert.Null(new MySqlDialect().CreateBulkCopyProvider());

    [Fact]
    public void PostgreSql_CreatesNoBulkCopyProvider()
        => Assert.Null(new PostgreSqlDialect().CreateBulkCopyProvider());

    public static TheoryData<ISqlDialect> Dialects => new() { new SQLiteDialect(), new MySqlDialect(), new PostgreSqlDialect() };

    [Theory]
    [MemberData(nameof(Dialects))]
    public void AnEmptyPartitionList_ContributesNothingToTheOverClause(ISqlDialect dialect)
        => Assert.Equal(" OVER (ORDER BY a)", dialect.GenerateOverClause([], [("a", false)]));

    [Theory]
    [MemberData(nameof(Dialects))]
    public void APartitionAndAnOrder_AreSeparatedBySingleSpace(ISqlDialect dialect)
        => Assert.Equal(" OVER (PARTITION BY p, q ORDER BY a, b DESC)", dialect.GenerateOverClause(["p", "q"], [("a", false), ("b", true)]));

    [Theory]
    [InlineData("MySql")]
    [InlineData("PostgreSql")]
    public void InvalidIdentifiers_AreRejectedByName(string flavor)
    {
        ISqlDialect dialect = flavor == "MySql" ? new MySqlDialect() : new PostgreSqlDialect();

        Assert.Equal("tableName", Assert.Throws<ArgumentException>(() => dialect.EscapeTableName(null, "a b")).ParamName);
        Assert.Equal("schemaName", Assert.Throws<ArgumentException>(() => dialect.EscapeTableName("a b", "t")).ParamName);
        Assert.Equal("columnName", Assert.Throws<ArgumentException>(() => dialect.EscapeColumnName("a b")).ParamName);
    }

    [Fact]
    public void MySql_UpsertAssignsEveryUpdateColumnSeparatedByCommas()
        => Assert.Equal(
            "INSERT INTO t (a, b) VALUES (@a, @b) ON DUPLICATE KEY UPDATE a = VALUES(a), b = VALUES(b)",
            new MySqlDialect().GenerateUpsertSql("t", ["a", "b"], ["@a", "@b"], ["a", "b"], ["@a", "@b"], ["id"], ["@id"]));
}
