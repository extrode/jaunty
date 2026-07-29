using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R26: MySQL's UNSIGNED modifier appears only in COLUMN_TYPE, and every integer type was
/// mapped to its signed C# counterpart regardless. The upper half of each unsigned range is then
/// unrepresentable, and the property does not match what MySqlConnector actually returns
/// (byte/ushort/uint/ulong).
/// </summary>
public class MySqlUnsignedTypeTests
{
    private static CSharpTypeInfo Map(string dataType, string? columnType) =>
        new MySqlTypeMapper().MapToCSharpType(new ColumnSchema
        {
            ColumnName = "value",
            DataType = dataType,
            ColumnType = columnType,
            IsNullable = false,
            IsPrimaryKey = false,
            IsIdentity = false,
            IsComputed = false,
            OrdinalPosition = 1
        });

    [Theory]
    [InlineData("tinyint", "tinyint(3) unsigned", "byte")]
    [InlineData("smallint", "smallint(5) unsigned", "ushort")]
    [InlineData("mediumint", "mediumint(8) unsigned", "uint")]
    [InlineData("int", "int(10) unsigned", "uint")]
    [InlineData("integer", "int(10) unsigned", "uint")]
    [InlineData("bigint", "bigint(20) unsigned", "ulong")]
    public void UnsignedIntegers_MapToTheUnsignedCSharpType(string dataType, string columnType, string expected)
    {
        CSharpTypeInfo info = Map(dataType, columnType);

        Assert.Equal(expected, info.TypeName);
        Assert.True(info.IsValueType);
    }

    [Theory]
    [InlineData("tinyint", "tinyint(4)", "sbyte")]
    [InlineData("smallint", "smallint(6)", "short")]
    [InlineData("mediumint", "mediumint(9)", "int")]
    [InlineData("int", "int(11)", "int")]
    [InlineData("bigint", "bigint(20)", "long")]
    public void SignedIntegers_AreUnchanged(string dataType, string columnType, string expected)
        => Assert.Equal(expected, Map(dataType, columnType).TypeName);

    /// <summary>
    /// ZEROFILL implies UNSIGNED in MySQL, and COLUMN_TYPE spells both.
    /// </summary>
    [Fact]
    public void ZerofillColumns_AreStillRecognisedAsUnsigned()
        => Assert.Equal("ulong", Map("bigint", "bigint(20) unsigned zerofill").TypeName);

    /// <summary>
    /// The tinyint(1) boolean convention must win over the unsigned widening - "tinyint(1)
    /// unsigned" is still a flag column, not a byte.
    /// </summary>
    [Fact]
    public void SingleWidthTinyInt_IsStillBool_EvenWhenUnsigned()
        => Assert.Equal("bool", Map("tinyint", "tinyint(1) unsigned").TypeName);

    [Fact]
    public void SingleWidthTinyInt_IsStillBool_WhenSigned()
        => Assert.Equal("bool", Map("tinyint", "tinyint(1)").TypeName);

    /// <summary>
    /// A reader that does not populate ColumnType must keep the signed mapping rather than
    /// guessing - the modifier is genuinely unknown in that case.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutColumnType_TheSignedMappingIsKept(string? columnType)
        => Assert.Equal("long", Map("bigint", columnType).TypeName);

    /// <summary>
    /// "unsigned" is matched as a whole word, so it cannot be picked up from a longer token.
    /// </summary>
    [Theory]
    [InlineData("bigint(20) notunsigned")]
    [InlineData("bigint(20) unsignedx")]
    public void UnsignedIsMatchedAsAWholeWord(string columnType)
        => Assert.Equal("long", Map("bigint", columnType).TypeName);
}
