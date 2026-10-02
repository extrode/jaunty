using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class ReaderValueConverterTemporalEdgeTests
{
    private static object TimeTz()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT TIMETZ '03:04:05+02'";
        return cmd.ExecuteScalar()!;
    }

    [Fact]
    public void ATimeTzColumn_ReadsIntoTimeOnlyAndTimeSpan()
    {
        object value = TimeTz();
        Assert.IsType<DateTimeOffset>(value);

        Assert.True(ReaderValueConverter.TryConvert(value, typeof(TimeOnly), true, out object? time, out _));
        Assert.Equal(new TimeOnly(3, 4, 5), time);

        Assert.True(ReaderValueConverter.TryConvert(value, typeof(TimeSpan?), true, out object? span, out _));
        Assert.Equal(new TimeSpan(3, 4, 5), span);
    }

    [Fact]
    public void ADateTimeOffset_ReadsItsOwnTimeOfDayNotTheUtcOne()
    {
        var value = new DateTimeOffset(2026, 10, 1, 23, 30, 0, TimeSpan.FromHours(-5));

        Assert.True(ReaderValueConverter.TryConvert(value, typeof(TimeOnly), true, out object? time, out _));
        Assert.Equal(new TimeOnly(23, 30), time);
        Assert.True(ReaderValueConverter.TryConvert(value, typeof(TimeSpan), true, out object? span, out _));
        Assert.Equal(new TimeSpan(23, 30, 0), span);
    }

    [Theory]
    [InlineData(1, 1, 1, 0, 0, 0)]
    [InlineData(9999, 12, 31, 23, 59, 59)]
    public void ATimestampAtTheEdgeOfTheRange_NeverThrowsFromTryConvert(int year, int month, int day, int hour, int minute, int second)
    {
        var value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(value);
        bool fits = (year == 1 ? offset <= TimeSpan.Zero : offset >= TimeSpan.Zero);

        bool converted = ReaderValueConverter.TryConvert(value, typeof(DateTimeOffset), true, out object? result, out string? reason);

        Assert.Equal(fits, converted);
        if (fits)
        {
            Assert.Equal(new DateTimeOffset(value), result);
        }
        else
        {
            Assert.Null(result);
            Assert.Contains("outside the range of DateTimeOffset", reason);
        }
    }
}
