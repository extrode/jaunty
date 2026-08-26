using System.Reflection;

using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-252 and AUD-R35-253. <c>ConvertOrThrow</c> attributed every failure to a type mismatch -
/// "the source produced X but the property is Y" - but <c>TryConvert</c> also fails on
/// <see cref="OverflowException"/> and <see cref="FormatException"/>, where the types are perfectly
/// compatible and this particular value is not. The real diagnostic was discarded by the catch
/// filter before the message was composed. Also pins the <c>DateTimeOffset</c> string branch and its
/// unparsable-string failure path, which had no test.
/// </summary>
public class ReaderValueConverterFailureReasonTests
{
    private sealed class Row
    {
        public byte Small { get; set; }
        public int Number { get; set; }
        public Guid Key { get; set; }
        public DateTimeOffset When { get; set; }
        public TimeOnly At { get; set; }
        public char Initial { get; set; }
        public DayOfWeek Day { get; set; }
    }

    private static ColumnMapping Mapping(string propertyName, string columnName)
    {
        PropertyInfo property = typeof(Row).GetProperty(propertyName)!;

        return new ColumnMapping
        {
            ColumnName = columnName,
            Property = property,
            PropertyType = property.PropertyType,
            Getter = entity => property.GetValue(entity),
            Setter = (entity, value) => property.SetValue(entity, value),
            IsDateTime = false
        };
    }

    private static string Message(object value, string propertyName, string columnName = "col")
        => Assert.Throws<InvalidOperationException>(
            () => ReaderValueConverter.ConvertOrThrow(value, Mapping(propertyName, columnName), typeof(Row))).Message;

    [Fact]
    public void AnOutOfRangeNumber_IsReportedAsARangeFailure()
    {
        string message = Message(99999L, nameof(Row.Small), "quantity");

        Assert.Contains("outside the range of Byte", message, StringComparison.Ordinal);
        Assert.Contains("99999", message, StringComparison.Ordinal);
        Assert.Contains("quantity", message, StringComparison.Ordinal);
        Assert.DoesNotContain("but the property is", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnparsableNumericString_IsReportedAsAFormatFailure()
    {
        string message = Message("twelve", nameof(Row.Number));

        Assert.Contains("cannot parse", message, StringComparison.Ordinal);
        Assert.Contains("twelve", message, StringComparison.Ordinal);
        Assert.DoesNotContain("but the property is", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGenuineTypeMismatch_StillReadsAsOne()
    {
        string message = Message(new object(), nameof(Row.Number));

        Assert.Contains("but the property is Int32", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnparsableGuid_NamesTheStringAndTheTarget()
    {
        string message = Message("not-a-guid", nameof(Row.Key));

        Assert.Contains("not-a-guid", message, StringComparison.Ordinal);
        Assert.Contains("Guid", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMultiCharacterString_SaysWhatACharHolds()
    {
        string message = Message("ab", nameof(Row.Initial));

        Assert.Contains("exactly one character", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIntervalOutsideADay_SaysSo()
    {
        string message = Message(TimeSpan.FromHours(25), nameof(Row.At));

        Assert.Contains("under 24 hours", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownEnumMember_ListsTheMembers()
    {
        string message = Message("Fooday", nameof(Row.Day));

        Assert.Contains("Fooday", message, StringComparison.Ordinal);
        Assert.Contains("Monday", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheColumnAndPropertyAreNamedWhateverTheReason()
    {
        string message = Message(99999L, nameof(Row.Small), "stock_level");

        Assert.Contains("stock_level", message, StringComparison.Ordinal);
        Assert.Contains("Row.Small", message, StringComparison.Ordinal);
    }

    // AUD-R35-253: the DateTimeOffset string branch, carried unfixed since round 34.

    [Fact]
    public void ADateTimeOffsetString_IsParsed()
    {
        Assert.True(ReaderValueConverter.TryConvert(
            "2026-08-04T10:30:00+02:00", typeof(DateTimeOffset), convertEnums: false, out object? converted));

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 10, 30, 0, TimeSpan.FromHours(2)), converted);
    }

    [Fact]
    public void ADateTimeOffsetStringWithNoOffset_StillParses()
    {
        Assert.True(ReaderValueConverter.TryConvert(
            "2026-08-04T10:30:00", typeof(DateTimeOffset), convertEnums: false, out object? converted));

        var parsed = Assert.IsType<DateTimeOffset>(converted);
        Assert.Equal(new DateTime(2026, 8, 4, 10, 30, 0), parsed.DateTime);
    }

    [Fact]
    public void ANullableDateTimeOffsetProperty_TakesTheStringToo()
    {
        Assert.True(ReaderValueConverter.TryConvert(
            "2026-08-04T00:00:00Z", typeof(DateTimeOffset?), convertEnums: false, out object? converted));

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero), converted);
    }

    [Fact]
    public void AnUnparsableDateTimeOffsetString_Fails()
    {
        Assert.False(ReaderValueConverter.TryConvert(
            "the fourth of never", typeof(DateTimeOffset), convertEnums: false, out object? converted));

        Assert.Null(converted);
    }

    [Fact]
    public void AnUnparsableDateTimeOffsetString_ReportsTheStringNotATypeMismatch()
    {
        string message = Message("the fourth of never", nameof(Row.When));

        Assert.Contains("the fourth of never", message, StringComparison.Ordinal);
    }
}
