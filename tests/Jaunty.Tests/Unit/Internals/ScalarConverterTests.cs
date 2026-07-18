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
}
