using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-030. SQL Server mapped <see cref="decimal"/> to DECIMAL(18,4). SQL Server rounds to the
/// declared scale without error, so an import that PostgreSQL's unconstrained NUMERIC stored
/// losslessly lost every digit past the fourth on SQL Server, silently. SQL Server has no
/// unconstrained DECIMAL, so a fixed precision has to be picked; (38,9) is the widest that cannot
/// overflow, since 38-9=29 integer digits covers <see cref="decimal.MaxValue"/> (7.9e28) in full.
/// </summary>
public class ImportDecimalPrecisionTests
{
    [Fact]
    public void SqlServerDeclaresTheWidestNonOverflowingDecimal() =>
        Assert.Equal("DECIMAL(38,9)", SqlServerImportDialect.Instance.MapClrTypeToSqlType(typeof(decimal)));

    /// <summary>
    /// The scale must cover more than the four places DECIMAL(18,4) allowed - that is the whole
    /// point of the change, stated as a number rather than as a literal so it survives a later
    /// widening.
    /// </summary>
    [Fact]
    public void TheScaleIsWiderThanTheFourPlacesThatUsedToRound()
    {
        string type = SqlServerImportDialect.Instance.MapClrTypeToSqlType(typeof(decimal));
        (int precision, int scale) = ParsePrecisionAndScale(type);

        Assert.True(scale > 4, $"scale {scale} still rounds at four decimal places");
        Assert.True(precision - scale >= 29, $"{precision - scale} integer digits cannot hold decimal.MaxValue");
        Assert.True(precision <= 38, $"precision {precision} exceeds the SQL Server maximum");
    }

    /// <summary>
    /// PostgreSQL's mapping is the comparison the finding was drawn against and must stay
    /// unconstrained - it has an unconstrained NUMERIC and loses nothing.
    /// </summary>
    [Fact]
    public void PostgreSqlStaysUnconstrained() =>
        Assert.Equal("NUMERIC", PostgreSqlImportDialect.Instance.MapClrTypeToSqlType(typeof(decimal)));

    /// <summary>
    /// The ulong mapping shares the DECIMAL syntax and is a separate decision (AUD-R34-029); it must
    /// not have been caught by the change.
    /// </summary>
    [Fact]
    public void TheULongMappingIsUnchanged() =>
        Assert.Equal("DECIMAL(20,0)", SqlServerImportDialect.Instance.MapClrTypeToSqlType(typeof(ulong)));

    private static (int Precision, int Scale) ParsePrecisionAndScale(string sqlType)
    {
        int open = sqlType.IndexOf('(');
        int comma = sqlType.IndexOf(',');
        int close = sqlType.IndexOf(')');

        return (
            int.Parse(sqlType[(open + 1)..comma], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(sqlType[(comma + 1)..close], System.Globalization.CultureInfo.InvariantCulture));
    }
}
