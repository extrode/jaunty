using System.Numerics;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R38-022: DuckDB's SUM() returns HUGEINT, which DuckDB.NET reads as
/// <see cref="BigInteger"/>, and the converter could not turn that into any numeric property.
/// </summary>
public class HugeIntConversionTests : IDisposable
{
    public class RegionTotal
    {
        public string Region { get; set; } = string.Empty;
        public long Total { get; set; }
        public int IntTotal { get; set; }
        public decimal DecimalTotal { get; set; }
        public double DoubleTotal { get; set; }
        public long? NullableTotal { get; set; }
    }

    public class Row
    {
        public string Region { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r38_hugeint_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public HugeIntConversionTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "rows.csv");
        File.WriteAllText(csvPath, "Region,Quantity\nnorth,2\nnorth,3\nsouth,7\n");

        var options = new FlatFileOptions();
        options.AddCsv<Row>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static (bool Ok, object? Converted, string? Reason) Convert(object value, Type target)
    {
        bool ok = ReaderValueConverter.TryConvert(value, target, convertEnums: true, out object? converted, out string? reason);
        return (ok, converted, reason);
    }

    [Fact]
    public void SumAggregate_MaterialisesIntoEveryNumericPropertyType()
    {
        List<RegionTotal> totals = _db.Query<RegionTotal>(
            "SELECT Region, SUM(Quantity) AS Total, SUM(Quantity) AS IntTotal, SUM(Quantity) AS DecimalTotal, " +
            "SUM(Quantity) AS DoubleTotal, SUM(Quantity) AS NullableTotal FROM \"row\" GROUP BY Region ORDER BY Region");

        Assert.Equal(2, totals.Count);
        Assert.Equal(5L, totals[0].Total);
        Assert.Equal(5, totals[0].IntTotal);
        Assert.Equal(5m, totals[0].DecimalTotal);
        Assert.Equal(5d, totals[0].DoubleTotal);
        Assert.Equal(7L, totals[1].NullableTotal);
    }

    [Theory]
    [InlineData(typeof(sbyte), (sbyte)42)]
    [InlineData(typeof(byte), (byte)42)]
    [InlineData(typeof(short), (short)42)]
    [InlineData(typeof(ushort), (ushort)42)]
    [InlineData(typeof(int), 42)]
    [InlineData(typeof(uint), 42u)]
    [InlineData(typeof(long), 42L)]
    [InlineData(typeof(ulong), 42ul)]
    [InlineData(typeof(double), 42d)]
    [InlineData(typeof(float), 42f)]
    [InlineData(typeof(string), "42")]
    [InlineData(typeof(long?), 42L)]
    public void BigInteger_ConvertsToTheTarget(Type target, object expected)
    {
        Assert.Equal((true, expected, null), Convert(new BigInteger(42), target));
    }

    [Fact]
    public void BigInteger_ConvertsToDecimal()
    {
        Assert.Equal((true, (object)42m, (string?)null), Convert(new BigInteger(42), typeof(decimal)));
    }

    [Fact]
    public void BigInteger_BeyondTheTargetsRange_ReportsTheOverflow()
    {
        var big = BigInteger.Parse("170141183460469231731687303715884105727");

        Assert.Equal(
            (false, null, "the source produced BigInteger 170141183460469231731687303715884105727, which is outside the range of Int64."),
            Convert(big, typeof(long)));
    }

    [Fact]
    public void NegativeBigInteger_ForAnUnsignedTarget_ReportsTheOverflow()
    {
        var (ok, _, reason) = Convert(new BigInteger(-1), typeof(uint));

        Assert.False(ok);
        Assert.EndsWith("which is outside the range of UInt32.", reason);
    }

    [Fact]
    public void BigInteger_ForANonNumericTarget_IsStillAMismatch()
    {
        Assert.False(Convert(new BigInteger(1), typeof(DateTime)).Ok);
        Assert.False(Convert(new BigInteger(1), typeof(char)).Ok);
    }
}
