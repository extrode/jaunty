using Jaunty.Internals.Read;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R26-062 (round 26, batch 6, low/consistency). Jaunty had three terminal "raw ADO.NET value to
/// CLR type" converters - <c>DbValueConverter.ChangeType</c> (Jaunty.Extensions.Reflection),
/// <c>GroupedJoinedResultMapper.ConvertColumnValue</c> (Jaunty.Fluent) and
/// <c>ScalarConverter&lt;T&gt;.Convert</c> (Jaunty) - which agreed on culture and on enums and
/// disagreed on everything else. They now all delegate to <see cref="DbValueConversion"/>.
///
/// <para>
/// The finding named three divergences (Guid, char, nullable unwrapping). A fourth turned up while
/// consolidating and is the one most likely to have been hit in practice: only
/// <c>ScalarConverter</c> converted <c>DateTimeOffset</c>/<c>TimeSpan</c>/<c>DateOnly</c>/
/// <c>TimeOnly</c> from a string, so the same SQLite column that a scalar query read fine threw
/// <c>InvalidCastException</c> through a grouped or reflection-mapped read.
/// </para>
/// </summary>
public class DbValueConversionTests
{
    private enum Colour { Red = 1, Green = 2 }

    // ------------------------------------------------------------------
    // Divergence 1: Guid
    // ------------------------------------------------------------------

    [Fact]
    public void Guid_FromAGuid_PassesThrough()
    {
        var value = Guid.NewGuid();
        Assert.Equal(value, DbValueConversion.Convert(value, typeof(Guid)));
    }

    [Fact]
    public void Guid_FromAString_Parses()
    {
        var value = Guid.NewGuid();
        Assert.Equal(value, DbValueConversion.Convert(value.ToString(), typeof(Guid)));
    }

    /// <summary>
    /// The three converters produced three different exceptions for this one input:
    /// <c>InvalidCastException</c> from the Reflection one's hard <c>(string)value</c> cast,
    /// <c>FormatException</c> from the Fluent one stringifying it to <c>"System.Byte[]"</c> first,
    /// and <c>InvalidCastException</c> from <c>Convert.ChangeType</c> in the third. It is now one
    /// exception that names both types - and it is deliberately still an exception: the byte order
    /// of a binary GUID is provider-specific, so <c>new Guid(byte[])</c> would return a silently
    /// wrong value rather than fail.
    /// </summary>
    [Fact]
    public void Guid_FromAByteArray_ThrowsOneClearErrorNamingBothTypes()
    {
        byte[] bytes = Guid.NewGuid().ToByteArray();

        InvalidCastException ex = Assert.Throws<InvalidCastException>(
            () => DbValueConversion.Convert(bytes, typeof(Guid)));

        Assert.Contains("System.Byte[]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("System.Guid", ex.Message, StringComparison.Ordinal);

        // AUD-R35-049: the generated path used to answer new Guid(bytes) here instead. It now
        // refuses with this same message, and GeneratedReadFallbackTests pins these two
        // phrases from the other side - so the two converters cannot drift apart again
        // without one of the pair failing.
        Assert.Contains("byte order", ex.Message, StringComparison.Ordinal);
        Assert.Contains("will not guess it", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Guid_Nullable_UnwrapsTheTarget()
    {
        var value = Guid.NewGuid();
        Assert.Equal(value, DbValueConversion.Convert(value.ToString(), typeof(Guid?)));
    }

    // ------------------------------------------------------------------
    // Divergence 2: char
    // ------------------------------------------------------------------

    [Fact]
    public void Char_FromASingleCharacterString_Converts()
    {
        Assert.Equal('x', DbValueConversion.Convert("x", typeof(char)));
    }

    /// <summary>
    /// Only the Fluent converter mapped <c>""</c> to <c>'\0'</c>; the other two handed it to
    /// <c>Convert.ChangeType</c>, which throws. A CHAR(1) column holding the empty string is
    /// ordinary, so throwing is the wrong answer of the two.
    /// </summary>
    [Fact]
    public void Char_FromTheEmptyString_YieldsNul()
    {
        Assert.Equal('\0', DbValueConversion.Convert("", typeof(char)));
    }

    [Fact]
    public void Char_FromALongerString_TakesTheFirstCharacter()
    {
        Assert.Equal('a', DbValueConversion.Convert("abc", typeof(char)));
    }

    // ------------------------------------------------------------------
    // Divergence 3: Nullable<T> unwrapping
    // ------------------------------------------------------------------

    /// <summary>
    /// The Fluent converter required its caller to have unwrapped the target already; the other two
    /// did it themselves. Callers should not have to know which one they got.
    /// </summary>
    [Fact]
    public void Nullable_IntTarget_IsUnwrapped()
    {
        Assert.Equal(42, DbValueConversion.Convert("42", typeof(int?)));
    }

    [Fact]
    public void Nullable_DecimalTarget_IsUnwrapped()
    {
        Assert.Equal(1.5m, DbValueConversion.Convert("1.5", typeof(decimal?)));
    }

    // ------------------------------------------------------------------
    // Divergence 4: the date/time types, found while consolidating
    // ------------------------------------------------------------------

    [Fact]
    public void DateTimeOffset_FromAString_Converts()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero),
            DbValueConversion.Convert("2026-07-30T12:00:00+00:00", typeof(DateTimeOffset)));
    }

    [Fact]
    public void DateTimeOffset_FromADateTime_Converts()
    {
        var input = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero),
            DbValueConversion.Convert(input, typeof(DateTimeOffset)));
    }

    [Fact]
    public void TimeSpan_FromAString_Converts()
    {
        Assert.Equal(new TimeSpan(1, 2, 3), DbValueConversion.Convert("01:02:03", typeof(TimeSpan)));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void DateOnly_FromAString_Converts()
    {
        Assert.Equal(new DateOnly(2026, 7, 30), DbValueConversion.Convert("2026-07-30", typeof(DateOnly)));
    }

    [Fact]
    public void TimeOnly_FromAString_Converts()
    {
        Assert.Equal(new TimeOnly(13, 45), DbValueConversion.Convert("13:45", typeof(TimeOnly)));
    }

    // ------------------------------------------------------------------
    // AUD-R35-027: the source-generated converter (JauntyGenerator.cs) accepts DateTime for a
    // DateOnly target and TimeSpan/DateTime for a TimeOnly target. This one only ever accepted a
    // string, so the two metadata paths disagreed about every date and time column the mainstream
    // providers surface as a CLR type rather than as text - SqlClient's DATE (DateTime) and TIME
    // (TimeSpan), Npgsql's date, DuckDB's TIMESTAMP. Text-backed SQLite worked; nothing else did.
    // ------------------------------------------------------------------

    [Fact]
    public void DateOnly_FromADateTime_Converts()
    {
        Assert.Equal(
            new DateOnly(2026, 7, 30),
            DbValueConversion.Convert(new DateTime(2026, 7, 30, 13, 45, 0), typeof(DateOnly)));
    }

    [Fact]
    public void DateOnly_FromADateTime_DiscardsTheTime()
    {
        Assert.Equal(
            new DateOnly(2026, 7, 30),
            DbValueConversion.Convert(new DateTime(2026, 7, 30, 23, 59, 59), typeof(DateOnly)));
    }

    [Fact]
    public void NullableDateOnly_FromADateTime_Converts()
    {
        Assert.Equal(
            new DateOnly(2026, 7, 30),
            DbValueConversion.Convert(new DateTime(2026, 7, 30), typeof(DateOnly?)));
    }

    [Fact]
    public void TimeOnly_FromATimeSpan_Converts()
    {
        Assert.Equal(new TimeOnly(13, 45, 30), DbValueConversion.Convert(new TimeSpan(13, 45, 30), typeof(TimeOnly)));
    }

    [Fact]
    public void TimeOnly_FromADateTime_Converts()
    {
        Assert.Equal(
            new TimeOnly(13, 45, 30),
            DbValueConversion.Convert(new DateTime(2026, 7, 30, 13, 45, 30), typeof(TimeOnly)));
    }

    [Fact]
    public void TimeOnly_FromAnIntervalLongerThanADay_Throws()
    {
        Assert.Throws<InvalidCastException>(
            () => DbValueConversion.Convert(TimeSpan.FromHours(30), typeof(TimeOnly)));
    }

    [Fact]
    public void TimeOnly_FromANegativeInterval_Throws()
    {
        Assert.Throws<InvalidCastException>(
            () => DbValueConversion.Convert(TimeSpan.FromHours(-1), typeof(TimeOnly)));
    }
#endif

    // ------------------------------------------------------------------
    // What all three already agreed on, and must keep agreeing on
    // ------------------------------------------------------------------

    [Fact]
    public void Enum_FromItsName_Parses()
    {
        Assert.Equal(Colour.Green, DbValueConversion.Convert("Green", typeof(Colour)));
    }

    [Fact]
    public void Enum_FromItsNameIgnoringCase_Parses()
    {
        Assert.Equal(Colour.Green, DbValueConversion.Convert("green", typeof(Colour)));
    }

    [Fact]
    public void Enum_FromItsUnderlyingValue_Converts()
    {
        Assert.Equal(Colour.Red, DbValueConversion.Convert(1, typeof(Colour)));
    }

    [Fact]
    public void Enum_Nullable_UnwrapsTheTarget()
    {
        Assert.Equal(Colour.Red, DbValueConversion.Convert("Red", typeof(Colour?)));
    }

    /// <summary>
    /// The reason all three pinned <c>InvariantCulture</c>, and the reason the consolidated one must
    /// keep doing so: under a comma-decimal culture <c>Convert.ChangeType("1.5", typeof(decimal))</c>
    /// does not throw - it reads the period as a group separator and returns 15.
    /// </summary>
    [Fact]
    public void Decimal_FromAStringUnderACommaDecimalCulture_IsNotScaledByTheLocale()
    {
        System.Globalization.CultureInfo original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(1.5m, DbValueConversion.Convert("1.5", typeof(decimal)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    // ------------------------------------------------------------------
    // The three entry points now agree, which is the whole point
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>ScalarConverter&lt;T&gt;</c> keeps its own fast path and nullable unwrapping and defers
    /// the rest, so it must produce the same answers as calling <see cref="DbValueConversion"/>
    /// directly - including for the cases it did not previously handle.
    /// </summary>
    [Fact]
    public void ScalarConverter_AgreesWithTheSharedConverter()
    {
        Assert.Equal('\0', ScalarConverter<char>.Convert(""));
        Assert.Equal(Colour.Green, ScalarConverter<Colour>.Convert("Green"));
        Assert.Equal(new TimeSpan(1, 2, 3), ScalarConverter<TimeSpan>.Convert("01:02:03"));
        Assert.Throws<InvalidCastException>(() => ScalarConverter<Guid>.Convert(Guid.NewGuid().ToByteArray()));
    }
}
