using System.Reflection;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// The full failure sentences <c>ReaderValueConverter</c> composes, the <c>TimeOnly</c> interval
/// boundaries, and the enum path's range, cast and convert-off failures.
/// </summary>
public class ReaderValueConverterExactReasonTests
{
    public enum Tiny : byte { A, B }

    private sealed class Row
    {
        public int? MaybeNumber { get; set; }
    }

    private static (bool Ok, object? Converted, string? Reason) Convert(object value, Type target, bool convertEnums = true)
    {
        bool ok = ReaderValueConverter.TryConvert(value, target, convertEnums, out object? converted, out string? reason);
        return (ok, converted, reason);
    }

    [Fact]
    public void AMultiCharacterString_ForAChar()
    {
        Assert.Equal(
            (false, null, "the source produced the 2-character string 'ab', and a char property holds exactly one character."),
            Convert("ab", typeof(char)));
    }

    [Fact]
    public void ZeroInterval_IsMidnight()
    {
        Assert.Equal((true, TimeOnly.MinValue, null), Convert(TimeSpan.Zero, typeof(TimeOnly)));
    }

    [Fact]
    public void AFullDayInterval_IsRejected()
    {
        Assert.Equal(
            (false, null, "the source produced the interval 1.00:00:00, and a TimeOnly property only holds a time of day - at least zero and under 24 hours."),
            Convert(TimeSpan.FromDays(1), typeof(TimeOnly)));
    }

    [Fact]
    public void ANegativeInterval_IsRejected()
    {
        Assert.False(Convert(TimeSpan.FromSeconds(-1), typeof(TimeOnly)).Ok);
    }

    [Fact]
    public void AnEnum_WithConversionOff_IsLeftToTheCaller()
    {
        Assert.Equal(
            (false, null, "enum conversion is off on this path, so DayOfWeek is left to the caller."),
            Convert(1L, typeof(DayOfWeek), convertEnums: false));
    }

    [Fact]
    public void AnUnknownEnumName_ListsEveryMember()
    {
        Assert.Equal(
            (false, null, "the source produced 'Funday', which is not a member of Tiny (A, B) nor its numeric form."),
            Convert("Funday", typeof(Tiny)));
    }

    [Fact]
    public void AnEnumValueOutsideItsUnderlyingType_IsARangeFailure()
    {
        Assert.Equal(
            (false, null, "the source produced Int64 300, which is outside the range of Tiny."),
            Convert(300L, typeof(Tiny)));
    }

    [Fact]
    public void AnEnumFromAnUnconvertibleValue_HasNoReason()
    {
        Assert.Equal((false, null, null), Convert(Guid.Empty, typeof(Tiny)));
    }

    [Fact]
    public void AnUnparsableDate_NamesTheStringAndTheType()
    {
        Assert.Equal(
            (false, null, "the source produced the string 'nope', which is not a DateOnly in any format the invariant culture recognises."),
            Convert("nope", typeof(DateOnly)));
    }

    [Fact]
    public void ATypeMismatchIntoANullableProperty_ShowsTheQuestionMark()
    {
        PropertyInfo property = typeof(Row).GetProperty(nameof(Row.MaybeNumber))!;
        var mapping = new ColumnMapping
        {
            ColumnName = "n",
            Property = property,
            PropertyType = property.PropertyType,
            Getter = entity => property.GetValue(entity),
            Setter = (entity, value) => property.SetValue(entity, value),
            IsDateTime = false
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ReaderValueConverter.ConvertOrThrow(Guid.Empty, mapping, typeof(Row)));

        Assert.Equal("Cannot read column 'n' into Row.MaybeNumber: the source produced Guid but the property is Int32?.", ex.Message);
    }
}
