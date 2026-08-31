using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R35-066. The fluent <c>Avg</c> family is declared to return <see cref="double"/>, but every
/// translator emitted a bare <c>AVG(&lt;column&gt;)</c>. On SQL Server <c>AVG</c> takes its result
/// type from its operand, so the average of an <c>int</c> column is truncated in the engine and
/// then widened by the mapper - <c>12.0</c> where the true average is <c>12.6</c>. SQLite (always
/// float), MySQL (decimal) and PostgreSQL (numeric) do not truncate, which is why a green suite
/// never showed it, and why they keep the bare form.
/// </summary>
public class FractionalAverageTests
{
    private static readonly SqlServerDialect SqlServer = new();
    private static readonly SQLiteDialect Sqlite = new();
    private static readonly MySqlDialect MySql = new();
    private static readonly PostgreSqlDialect Postgres = new();

    [Fact]
    public void SqlServer_CastsTheOperandToFloat()
    {
        Assert.Equal("AVG(CAST([qty] AS FLOAT))", FractionalAverage.Generate(SqlServer, "[qty]"));
    }

    [Fact]
    public void SqlServer_ImplementsTheInterface()
    {
        Assert.IsAssignableFrom<IFractionalAverageDialect>(SqlServer);
    }

    /// <summary>
    /// Casting the operand, not the result: <c>CAST(AVG(qty) AS FLOAT)</c> would widen a value the
    /// engine had already floored, which is the defect rather than the fix.
    /// </summary>
    [Fact]
    public void SqlServer_CastsInsideTheAggregate_NotOutsideIt()
    {
        string sql = FractionalAverage.Generate(SqlServer, "[qty]");

        Assert.StartsWith("AVG(", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CAST(AVG", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"qty\"")]
    [InlineData("`qty`")]
    [InlineData("qty")]
    public void ADialectThatDoesNotTruncate_GetsTheBareForm(string operand)
    {
        Assert.Equal($"AVG({operand})", FractionalAverage.Generate(Sqlite, operand));
        Assert.Equal($"AVG({operand})", FractionalAverage.Generate(MySql, operand));
        Assert.Equal($"AVG({operand})", FractionalAverage.Generate(Postgres, operand));
    }

    [Fact]
    public void TheThreeNonTruncatingDialects_DoNotImplementTheInterface()
    {
        Assert.IsNotAssignableFrom<IFractionalAverageDialect>(Sqlite);
        Assert.IsNotAssignableFrom<IFractionalAverageDialect>(MySql);
        Assert.IsNotAssignableFrom<IFractionalAverageDialect>(Postgres);
    }

    [Fact]
    public void ANullDialect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FractionalAverage.Generate(null!, "qty"));
    }

    /// <summary>
    /// An arbitrary expression, not only a bare column - the HAVING and GROUP BY translators both
    /// pass an already-escaped operand that may be qualified.
    /// </summary>
    [Fact]
    public void AQualifiedOperand_IsPassedThroughUntouched()
    {
        Assert.Equal("AVG(CAST([p].[qty] AS FLOAT))", FractionalAverage.Generate(SqlServer, "[p].[qty]"));
    }

    /// <summary>
    /// The bulk-copy wrapper forwards it. A wrapper that dropped it would make SQL Server look like
    /// a dialect that never truncates - the same drift hazard <c>SubstringToEndDialectTests</c>
    /// exists to catch for the other optional interface.
    /// </summary>
    [Fact]
    public void TheBulkCopyWrapper_ForwardsToTheWrappedDialect()
    {
        var wrapper = new global::Jaunty.Extensions.Reflection.Dialects.SqlServerDialectWithBulkCopy();

        Assert.IsAssignableFrom<IFractionalAverageDialect>(wrapper);
        Assert.Equal("AVG(CAST([qty] AS FLOAT))", FractionalAverage.Generate(wrapper, "[qty]"));
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("PostgreSql")]
    [InlineData("SQLite")]
    public void TheOtherBulkCopyWrappers_StillGetTheBareForm(string flavor)
    {
        ISqlDialect wrapper = flavor switch
        {
            "MySql" => new global::Jaunty.Extensions.Reflection.Dialects.MySqlDialectWithBulkCopy(),
            "PostgreSql" => new global::Jaunty.Extensions.Reflection.Dialects.PostgreSqlDialectWithBulkCopy(),
            _ => new global::Jaunty.Extensions.Reflection.Dialects.SQLiteDialectWithBulkCopy(),
        };

        Assert.Equal("AVG(qty)", FractionalAverage.Generate(wrapper, "qty"));
    }
}
