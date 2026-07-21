using Jaunty.Internals.Read;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression tests for ScalarConverter&lt;T&gt;: enum-as-string conversion, DBNull
/// defensiveness, and Guid/DateTimeOffset conversion from string (gaps that
/// System.Convert.ChangeType does not cover on its own).
/// </summary>
public class ScalarConverterTests
{
    private enum Status
    {
        Inactive = 0,
        Active = 1,
        Archived = 2
    }

    [Fact]
    public void Convert_StringStoredEnum_ParsesByName()
    {
        var result = ScalarConverter<Status>.Convert("Active");

        Assert.Equal(Status.Active, result);
    }

    [Fact]
    public void Convert_StringStoredEnum_IsCaseInsensitive()
    {
        var result = ScalarConverter<Status>.Convert("active");

        Assert.Equal(Status.Active, result);
    }

    [Fact]
    public void Convert_NumericStoredEnum_StillConvertsFromInt()
    {
        var result = ScalarConverter<Status>.Convert(2);

        Assert.Equal(Status.Archived, result);
    }

    [Fact]
    public void Convert_DBNull_ReturnsDefault()
    {
        var result = ScalarConverter<int>.Convert(DBNull.Value);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Convert_DBNull_NullableTarget_ReturnsNull()
    {
        var result = ScalarConverter<int?>.Convert(DBNull.Value);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_GuidFromString_Parses()
    {
        var guid = Guid.NewGuid();

        var result = ScalarConverter<Guid>.Convert(guid.ToString());

        Assert.Equal(guid, result);
    }

    [Fact]
    public void Convert_NullableGuidFromString_Parses()
    {
        var guid = Guid.NewGuid();

        var result = ScalarConverter<Guid?>.Convert(guid.ToString());

        Assert.Equal(guid, result);
    }

    [Fact]
    public void Convert_DateTimeOffsetFromString_Parses()
    {
        var dto = new DateTimeOffset(2026, 7, 18, 12, 30, 0, TimeSpan.FromHours(2));

        var result = ScalarConverter<DateTimeOffset>.Convert(dto.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(dto, result);
    }

    [Fact]
    public void Convert_NullableDateTimeOffsetFromString_Parses()
    {
        var dto = new DateTimeOffset(2026, 7, 18, 12, 30, 0, TimeSpan.FromHours(2));

        var result = ScalarConverter<DateTimeOffset?>.Convert(dto.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(dto, result);
    }

    // AUD-R6: TimeSpan doesn't implement IConvertible, so System.Convert.ChangeType cannot
    // produce it from a string - a provider (e.g. SQLite) returning a "time" column as text
    // previously threw InvalidCastException here instead of parsing correctly.
    [Fact]
    public void Convert_TimeSpanFromString_Parses()
    {
        var ts = new TimeSpan(1, 2, 3, 4);

        var result = ScalarConverter<TimeSpan>.Convert(ts.ToString("c", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(ts, result);
    }

    [Fact]
    public void Convert_NullableTimeSpanFromString_Parses()
    {
        var ts = new TimeSpan(1, 2, 3, 4);

        var result = ScalarConverter<TimeSpan?>.Convert(ts.ToString("c", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(ts, result);
    }

    // AUD-R7: the fallback System.Convert.ChangeType call didn't pass CultureInfo.InvariantCulture,
    // so decimal/numeric conversion depended on the executing thread's current culture. Under a
    // comma-decimal culture (e.g. de-DE), a "." in a string-typed decimal value could be
    // misinterpreted as a thousands separator instead of a decimal point, silently corrupting
    // the value instead of parsing it as 1.5.
    [Fact]
    public void Convert_DecimalFromString_IsCultureInvariant()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var result = ScalarConverter<decimal>.Convert("1.5");

            Assert.Equal(1.5m, result);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

#if NET8_0_OR_GREATER
    // AUD-R14: DateOnly/TimeOnly don't implement IConvertible either, so like Guid/DateTimeOffset/
    // TimeSpan above, System.Convert.ChangeType cannot produce them from a string - a provider
    // (e.g. SQLite) returning a "date"/"time" column as text previously threw
    // InvalidCastException here instead of parsing correctly.
    [Fact]
    public void Convert_DateOnlyFromString_Parses()
    {
        var date = new DateOnly(2026, 7, 20);

        var result = ScalarConverter<DateOnly>.Convert(date.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(date, result);
    }

    [Fact]
    public void Convert_NullableDateOnlyFromString_Parses()
    {
        var date = new DateOnly(2026, 7, 20);

        var result = ScalarConverter<DateOnly?>.Convert(date.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(date, result);
    }

    [Fact]
    public void Convert_TimeOnlyFromString_Parses()
    {
        var time = new TimeOnly(13, 45, 30);

        var result = ScalarConverter<TimeOnly>.Convert(time.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(time, result);
    }

    [Fact]
    public void Convert_NullableTimeOnlyFromString_Parses()
    {
        var time = new TimeOnly(13, 45, 30);

        var result = ScalarConverter<TimeOnly?>.Convert(time.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(time, result);
    }
#endif

}
