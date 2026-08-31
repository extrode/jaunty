using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// Unit tests for GroupedJoinedResultMapper.ConvertColumnValue, which special-cases enum/Guid/char
/// targets before falling back to Convert.ChangeType (which throws for enums and can't parse
/// Guid/char from an arbitrary string) (AUD-R11).
/// </summary>
public class GroupedJoinedResultMapperTests
{
    private enum Status
    {
        Active = 0,
        Inactive = 1
    }

    [Fact]
    public void ConvertColumnValue_EnumFromString_ParsesIgnoringCase()
    {
        var result = GroupedJoinedResultMapper.ConvertColumnValue("inactive", typeof(Status));

        Assert.Equal(Status.Inactive, result);
    }

    [Fact]
    public void ConvertColumnValue_EnumFromUnderlyingNumericType_Converts()
    {
        var result = GroupedJoinedResultMapper.ConvertColumnValue(1L, typeof(Status));

        Assert.Equal(Status.Inactive, result);
    }

    [Fact]
    public void ConvertColumnValue_GuidFromString_Parses()
    {
        var guid = Guid.NewGuid();

        var result = GroupedJoinedResultMapper.ConvertColumnValue(guid.ToString(), typeof(Guid));

        Assert.Equal(guid, result);
    }

    [Fact]
    public void ConvertColumnValue_GuidAlreadyGuid_PassesThrough()
    {
        var guid = Guid.NewGuid();

        var result = GroupedJoinedResultMapper.ConvertColumnValue(guid, typeof(Guid));

        Assert.Equal(guid, result);
    }

    [Fact]
    public void ConvertColumnValue_CharFromString_TakesFirstCharacter()
    {
        var result = GroupedJoinedResultMapper.ConvertColumnValue("A", typeof(char));

        Assert.Equal('A', result);
    }

    [Fact]
    public void ConvertColumnValue_NonSpecialCaseType_FallsBackToConvertChangeType()
    {
        var result = GroupedJoinedResultMapper.ConvertColumnValue(42, typeof(long));

        Assert.Equal(42L, result);
    }
}
