using System.Data;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Tests.Helpers;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// What the KeyValuePair and ValueTuple mappers produce per row: NULL into a non-nullable element
/// names that element, nullable elements convert, and a plain struct is not taken for a tuple.
/// </summary>
[Collection("Type Handler Operations")]
public class SpecialTypeMapperValueTests
{
    private static object MapRow(Type type, IDataReader reader)
    {
        SpecialTypeMappers.Register();
        var mapper = (Func<IDataReader, object>)JauntyConfig.SpecialTypeMapperResolver!(type, reader);
        return mapper(reader);
    }

    [Fact]
    public void APlainStruct_IsNotMappedAsATuple()
    {
        SpecialTypeMappers.Register();

        Assert.Null(JauntyConfig.SpecialTypeMapperResolver!(typeof(PlainStruct), new MutableStubReader(["a"], [1])));
    }

    [Fact]
    public void ValueTupleWiderThanTheResultSet_NamesEveryElementType()
    {
        SpecialTypeMappers.Register();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JauntyConfig.SpecialTypeMapperResolver!(typeof(ValueTuple<int, string, int>), new MutableStubReader(["a", "b"], [1, "x"])));

        Assert.Equal("Type 'ValueTuple<Int32, String, Int32>' requires 3 columns, but query returned 2.", ex.Message);
    }

    [Fact]
    public void KeyValuePair_NullIntoANonNullableKey_NamesTheKey()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MapRow(typeof(KeyValuePair<int, string>), new MutableStubReader(["k", "v"], [null, "v"])));

        Assert.Equal("Cannot assign NULL to non-nullable element 'Key' of type 'Int32'.", ex.Message);
    }

    [Fact]
    public void KeyValuePair_NullIntoANonNullableValue_NamesTheValue()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MapRow(typeof(KeyValuePair<string, int>), new MutableStubReader(["k", "v"], ["k", null])));

        Assert.Equal("Cannot assign NULL to non-nullable element 'Value' of type 'Int32'.", ex.Message);
    }

    [Fact]
    public void KeyValuePair_NullableValue_ConvertsFromAWiderType()
    {
        var row = (KeyValuePair<string, int?>)MapRow(typeof(KeyValuePair<string, int?>), new MutableStubReader(["k", "v"], ["k", 5L]));

        Assert.Equal(5, row.Value);
    }

    [Fact]
    public void ValueTuple_NullIntoANonNullableElement_NamesItsPosition()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MapRow(typeof(ValueTuple<int, int, int>), new MutableStubReader(["a", "b", "c"], [1, null, 3])));

        Assert.Equal("Cannot assign NULL to non-nullable element 'Item2' of type 'Int32'.", ex.Message);
    }

    private struct PlainStruct
    {
        public int A { get; set; }
    }
}
