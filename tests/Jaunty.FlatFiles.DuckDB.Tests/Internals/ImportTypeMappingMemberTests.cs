using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-250. <c>Normalize</c>'s <c>property</c> parameter - the per-property
/// <c>[EnumStorage]</c> override, the only reason the overload takes a <see cref="PropertyInfo"/> -
/// had no test: the suite drove enum storage exclusively through
/// <c>JauntyConfig.DefaultEnumStorage</c>, which passes no property. <c>MapForColumn</c> was never
/// named in <c>tests/</c> at all, so nothing asserted the <c>Column '&lt;name&gt;': </c> prefix that
/// is the whole point of the wrapper.
/// </summary>
/// <remarks>
/// Shares a collection with <c>ImportTypeMappingTests</c>: both mutate
/// <c>JauntyConfig.DefaultEnumStorage</c>, and this project parallelizes collections by default, so
/// without the shared collection the two races on one static.
/// </remarks>
[Collection("JauntyConfig.DefaultEnumStorage")]
public class ImportTypeMappingMemberTests
{
    private enum Priority
    {
        Low = 0,
        High = 1
    }

    private enum ByteBacked : byte
    {
        One = 1
    }

    private sealed class Ticket
    {
        [EnumStorage(EnumStorage.String)]
        public Priority AsString { get; set; }

        [EnumStorage(EnumStorage.Numeric)]
        public Priority AsNumeric { get; set; }

        [EnumStorage(EnumStorage.Numeric)]
        public ByteBacked NarrowNumeric { get; set; }

        [EnumStorage(EnumStorage.String)]
        public Priority? NullableAsString { get; set; }

        public Priority NoAttribute { get; set; }
    }

    private static PropertyInfo Prop(string name) => typeof(Ticket).GetProperty(name)!;

    [Fact]
    public void ThePropertyAttribute_OverridesTheGlobalDefault()
    {
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;

            Assert.Equal(typeof(string), ImportTypeMapping.Normalize(typeof(Priority), Prop("AsString")));

            JauntyConfig.DefaultEnumStorage = EnumStorage.String;

            Assert.Equal(typeof(int), ImportTypeMapping.Normalize(typeof(Priority), Prop("AsNumeric")));
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void ANumericOverride_UsesTheEnumsOwnUnderlyingType()
    {
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;

            Assert.Equal(typeof(byte), ImportTypeMapping.Normalize(typeof(ByteBacked), Prop("NarrowNumeric")));
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void ANullableEnum_IsUnwrappedBeforeTheAttributeIsRead()
    {
        Assert.Equal(typeof(string), ImportTypeMapping.Normalize(typeof(Priority?), Prop("NullableAsString")));
    }

    [Fact]
    public void WithoutTheAttribute_TheGlobalDefaultStillDecides()
    {
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;
            Assert.Equal(typeof(string), ImportTypeMapping.Normalize(typeof(Priority), Prop("NoAttribute")));

            JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
            Assert.Equal(typeof(int), ImportTypeMapping.Normalize(typeof(Priority), Prop("NoAttribute")));
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void ANullProperty_FallsBackToTheGlobalDefault()
    {
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;
            Assert.Equal(typeof(string), ImportTypeMapping.Normalize(typeof(Priority), null));
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void ANonEnum_IsUnaffectedByTheProperty()
    {
        Assert.Equal(typeof(int), ImportTypeMapping.Normalize(typeof(int?), Prop("AsString")));
        Assert.Equal(typeof(string), ImportTypeMapping.Normalize(typeof(string), Prop("AsNumeric")));
    }

    [Fact]
    public void MapForColumn_PassesTheMappedTypeThroughUnchanged()
    {
        Assert.Equal("INTEGER", ImportTypeMapping.MapForColumn(_ => "INTEGER", "Quantity", typeof(int)));
    }

    [Fact]
    public void MapForColumn_PrefixesTheColumnNameOntoTheFailure()
    {
        NotSupportedException inner = ImportTypeMapping.Unsupported(typeof(Version), "SQLite");

        var ex = Assert.Throws<NotSupportedException>(
            () => ImportTypeMapping.MapForColumn(_ => throw inner, "release_version", typeof(Version)));

        Assert.StartsWith("Column 'release_version': ", ex.Message, StringComparison.Ordinal);
        Assert.Contains("System.Version", ex.Message, StringComparison.Ordinal);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void MapForColumn_DoesNotSwallowOtherExceptions()
    {
        Assert.Throws<InvalidOperationException>(
            () => ImportTypeMapping.MapForColumn(_ => throw new InvalidOperationException("boom"), "c", typeof(int)));
    }
}
