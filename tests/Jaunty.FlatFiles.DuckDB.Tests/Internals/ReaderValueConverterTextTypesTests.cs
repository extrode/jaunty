using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-027. <c>TryConvert</c> had a string-parsing branch for <see cref="DateTimeOffset"/> only.
/// <see cref="Guid"/>, <see cref="TimeSpan"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/> and
/// <see cref="char"/> implement no <see cref="IConvertible"/> path from text, so they fell through to
/// <c>Convert.ChangeType</c> and threw - and text is the only representation a CSV or JSON column
/// has. Core's <c>DbValueConversion</c> has carried these branches since AUD-R29.
/// </summary>
public class ReaderValueConverterTextTypesTests
{
    private static object? Convert(object value, Type target)
    {
        Assert.True(ReaderValueConverter.TryConvert(value, target, convertEnums: true, out object? converted));
        return converted;
    }

    private static bool Fails(object value, Type target) =>
        !ReaderValueConverter.TryConvert(value, target, convertEnums: true, out _);

    [Fact]
    public void GuidFromText()
    {
        var expected = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        Assert.Equal(expected, Convert("6f9619ff-8b86-d011-b42d-00c04fc964ff", typeof(Guid)));
    }

    [Fact]
    public void NullableGuidFromText() =>
        Assert.Equal(Guid.Empty, Convert("00000000-0000-0000-0000-000000000000", typeof(Guid?)));

    [Fact]
    public void GuidFromUnparseableText() => Assert.True(Fails("not-a-guid", typeof(Guid)));

    [Fact]
    public void TimeSpanFromText() =>
        Assert.Equal(new TimeSpan(1, 2, 30, 15), Convert("1.02:30:15", typeof(TimeSpan)));

    [Fact]
    public void TimeSpanFromUnparseableText() => Assert.True(Fails("half past", typeof(TimeSpan)));

    [Fact]
    public void CharFromSingleCharacterText() => Assert.Equal('A', Convert("A", typeof(char)));

    [Fact]
    public void CharFromLongerText() => Assert.True(Fails("AB", typeof(char)));

    [Fact]
    public void CharFromEmptyText() => Assert.True(Fails("", typeof(char)));

    [Fact]
    public void DateOnlyFromText() =>
        Assert.Equal(new DateOnly(2026, 8, 2), Convert("2026-08-02", typeof(DateOnly)));

    [Fact]
    public void DateOnlyFromTimestamp() =>
        Assert.Equal(new DateOnly(2026, 8, 2), Convert(new DateTime(2026, 8, 2, 13, 45, 0), typeof(DateOnly)));

    [Fact]
    public void DateOnlyFromUnparseableText() => Assert.True(Fails("last tuesday", typeof(DateOnly)));

    [Fact]
    public void TimeOnlyFromText() =>
        Assert.Equal(new TimeOnly(13, 45, 30), Convert("13:45:30", typeof(TimeOnly)));

    [Fact]
    public void TimeOnlyFromInterval() =>
        Assert.Equal(new TimeOnly(13, 45, 30), Convert(new TimeSpan(13, 45, 30), typeof(TimeOnly)));

    [Fact]
    public void TimeOnlyFromTimestamp() =>
        Assert.Equal(new TimeOnly(13, 45, 0), Convert(new DateTime(2026, 8, 2, 13, 45, 0), typeof(TimeOnly)));

    [Fact]
    public void TimeOnlyFromAnIntervalLongerThanADay() =>
        Assert.True(Fails(TimeSpan.FromHours(30), typeof(TimeOnly)));

    [Fact]
    public void TimeOnlyFromANegativeInterval() =>
        Assert.True(Fails(TimeSpan.FromHours(-1), typeof(TimeOnly)));

    [Fact]
    public void TimeOnlyFromUnparseableText() => Assert.True(Fails("noonish", typeof(TimeOnly)));

    /// <summary>
    /// The branch that already worked, kept as the control: whatever the new branches do, the
    /// DateTimeOffset paths AUD-R29 added must still convert.
    /// </summary>
    [Fact]
    public void DateTimeOffsetFromTextStillWorks() =>
        Assert.Equal(
            new DateTimeOffset(2026, 8, 2, 13, 45, 0, TimeSpan.Zero),
            Convert("2026-08-02T13:45:00+00:00", typeof(DateTimeOffset)));

    /// <summary>
    /// And the IConvertible fallback is untouched - the new branches sit above it, not in place of it.
    /// </summary>
    [Fact]
    public void DecimalFromTextStillWorks() => Assert.Equal(1.5m, Convert("1.5", typeof(decimal)));
}
