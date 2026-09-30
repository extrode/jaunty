using Extrode.Jaunty.Dialects;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

public class SqlServerDialectSurvivorTests
{
    private readonly SqlServerDialect _dialect = new();

    private const string Injected = "ORDER BY (SELECT NULL)";

    private bool IsOrdered(string baseSql)
        => !_dialect.GetPagingSql(baseSql, 10, 20).Contains(Injected, StringComparison.Ordinal);

    [Theory]
    [InlineData("SELECT 1 -")]
    [InlineData("SELECT 1 /")]
    [InlineData("SELECT 1 -- ORDER BY x")]
    [InlineData("SELECT 1 /* ORDER BY x")]
    [InlineData("SELECT 1 /* ORDER BY x *")]
    [InlineData("/* ORDER BY x */ SELECT 1")]
    [InlineData("SELECT 'ORDER BY x'")]
    [InlineData("SELECT 'abc ORDER BY")]
    [InlineData("SELECT \"ORDER BY\"")]
    [InlineData("SELECT [ORDER BY]")]
    [InlineData("SELECT [ORDER BY")]
    [InlineData("SELECT (SELECT 1 ORDER BY x) y")]
    [InlineData("AAAAA BY x")]
    [InlineData("ORDER XX y")]
    [InlineData("ORDERBY x")]
    [InlineData("ORDERS BY x")]
    [InlineData("_ORDER BY x")]
    [InlineData("xORDER BY x")]
    [InlineData("ORDER")]
    [InlineData("ORDER ")]
    [InlineData("ORDER B")]
    [InlineData("ORDER BYTES")]
    [InlineData("ORDER BY_x")]
    [InlineData("ORDE")]
    [InlineData("")]
    public void SqlWithoutATopLevelOrderBy_IsPagedWithTheInjectedOrder(string baseSql)
        => Assert.False(IsOrdered(baseSql));

    [Theory]
    [InlineData("ORDER BY id")]
    [InlineData("ORDER BY")]
    [InlineData("SELECT 1 ORDER BY")]
    [InlineData("SELECT 1 ORDER BY(x)")]
    [InlineData("SELECT 1 -- c\nORDER BY id")]
    [InlineData("SELECT 1 -- c\rORDER BY id")]
    [InlineData("SELECT 1 -- c\r\nORDER BY id")]
    [InlineData("SELECT 1 --\nORDER BY id")]
    [InlineData("SELECT 1 /* c */ORDER BY id")]
    [InlineData("SELECT 1 /**/ORDER BY id")]
    [InlineData("SELECT 1 /* c *//* d */ ORDER BY id")]
    [InlineData("SELECT 'it''s' ORDER BY id")]
    [InlineData("SELECT 'a' ORDER BY id")]
    [InlineData("SELECT [a] ORDER BY id")]
    [InlineData("SELECT [a]]b] ORDER BY id")]
    [InlineData("SELECT \"a\" ORDER BY id")]
    [InlineData("SELECT (1)) ORDER BY id")]
    [InlineData("SELECT (1) ORDER BY id")]
    [InlineData("SELECT ((1)) ORDER BY id")]
    [InlineData("SELECT 1/2 ORDER BY id")]
    [InlineData("SELECT 1-2 ORDER BY id")]
    [InlineData("SELECT a*b FROM t ORDER BY id")]
    [InlineData("select 1 order by id")]
    [InlineData("SELECT 1 ORDER\t\r\n BY id")]
    public void SqlWithATopLevelOrderBy_IsPagedWithoutAnInjectedOrder(string baseSql)
        => Assert.True(IsOrdered(baseSql));

    [Fact]
    public void TheDialect_ExposesItsPrefixAndMultiRowInsert()
    {
        Assert.Equal("@", _dialect.ParameterPrefix);
        Assert.True(_dialect.SupportsMultiRowInsert);
        Assert.Null(_dialect.CreateBulkCopyProvider());
    }

    [Fact]
    public void InvalidIdentifiers_AreRejectedByName()
    {
        Assert.Equal("tableName", Assert.Throws<ArgumentException>(() => _dialect.EscapeTableName(null, "a b")).ParamName);
        Assert.Equal("schemaName", Assert.Throws<ArgumentException>(() => _dialect.EscapeTableName("a b", "t")).ParamName);
        Assert.Equal("columnName", Assert.Throws<ArgumentException>(() => _dialect.EscapeColumnName("a b")).ParamName);
    }

    [Fact]
    public void AnEmptyPartitionList_ContributesNothingToTheOverClause()
        => Assert.Equal(" OVER (ORDER BY a)", _dialect.GenerateOverClause([], [("a", false)]));

    [Fact]
    public void APartitionAndAnOrder_AreSeparatedByASingleSpace()
        => Assert.Equal(" OVER (PARTITION BY p, q ORDER BY a, b DESC)", _dialect.GenerateOverClause(["p", "q"], [("a", false), ("b", true)]));

    [Fact]
    public void AnEmptyOrderList_ContributesNothingToTheOverClause()
        => Assert.Equal(" OVER (PARTITION BY p)", _dialect.GenerateOverClause(["p"], []));

    [Fact]
    public void Upsert_CarriesEveryKeyMissingFromTheInsertList()
        => Assert.Equal(
            "MERGE INTO t WITH (HOLDLOCK) AS target USING (VALUES (@a, @k1, @k2)) AS source (a, k1, k2) ON target.k1 = source.k1 AND target.k2 = source.k2 WHEN MATCHED THEN UPDATE SET target.a = source.a WHEN NOT MATCHED THEN INSERT (a) VALUES (source.a);",
            _dialect.GenerateUpsertSql("t", ["a"], ["@a"], ["a"], ["@a"], ["k1", "k2"], ["@k1", "@k2"]));
}
