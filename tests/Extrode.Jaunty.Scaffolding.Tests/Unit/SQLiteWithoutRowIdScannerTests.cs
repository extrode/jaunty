using Extrode.Jaunty.Scaffolding.Providers.SQLite;

using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// Inputs where a mis-skipped quote, bracket or comment leaks an unbalanced <c>)</c> or a
/// "WITHOUT ROWID" into the options tail, where a lone <c>-</c> or <c>/</c> is an operator rather
/// than a comment opener, and where the statement ends mid-token.
/// </summary>
public class SQLiteWithoutRowIdScannerTests
{
    [Theory]
    [InlineData("CREATE TABLE t (x INT, \"a) WITHOUT ROWID\" TEXT)", false)]
    [InlineData("CREATE TABLE t (x TEXT DEFAULT ') WITHOUT ROWID')", false)]
    [InlineData("CREATE TABLE t (x INT, `a) WITHOUT ROWID` TEXT)", false)]
    [InlineData("CREATE TABLE t (x INT, [a) WITHOUT ROWID] TEXT)", false)]
    [InlineData("CREATE TABLE t (a INT CHECK (a IN (1)), \"without rowid\" TEXT)", false)]
    [InlineData("CREATE TABLE t (a INT CHECK (a > -1)) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT CHECK (a / 2 > 0)) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT -- ) WITHOUT ROWID\n, b INT)", false)]
    [InlineData("CREATE TABLE t (a INT /*/ ) WITHOUT ROWID */)", false)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT /*c*/ ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT /*/ x */ ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT -- c\n ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT - ROWID", true)]
    [InlineData("CREATE TABLE t (a INT /* c */) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT /*(*/) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT /* a*b ) WITHOUT ROWID */)", false)]
    [InlineData("CREATE TABLE t (a INT /* x/ ) WITHOUT ROWID */)", false)]
    [InlineData("CREATE TABLE t (a INT DEFAULT (random()), b INT) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT /*c*/ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT /* * x */ ROWID", true)]
    public void OnlyTheRealOptionsTailCounts(string createSql, bool expected)
        => Assert.Equal(expected, SQLiteSchemaReader.IsWithoutRowId(createSql));

    [Theory]
    [InlineData("CREATE TABLE \"t\"")]
    [InlineData("CREATE TABLE \"t")]
    [InlineData("CREATE TABLE t (a [unterminated")]
    [InlineData("CREATE TABLE t (a INT /* unterminated")]
    [InlineData("CREATE TABLE t (a INT -- unterminated")]
    [InlineData("CREATE TABLE t (a INT) WITHOUT /* unterminated")]
    [InlineData("CREATE TABLE t (a INT) -")]
    [InlineData("CREATE TABLE t (a INT")]
    [InlineData("[x) WITHOUT ROWID]")]
    [InlineData("CREATE TABLE t (a -")]
    [InlineData("CREATE TABLE t (a /")]
    [InlineData("CREATE TABLE t (a INT /* x*")]
    [InlineData("CREATE TABLE t (a) /")]
    [InlineData("CREATE TABLE t (a) /* x*")]
    public void AStatementEndingMidToken_IsNotWithoutRowId(string createSql)
        => Assert.False(SQLiteSchemaReader.IsWithoutRowId(createSql));
}
