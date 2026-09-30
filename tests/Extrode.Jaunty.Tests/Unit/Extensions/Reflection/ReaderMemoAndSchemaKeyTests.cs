using System.Data;
using System.Reflection;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Internals;
using Extrode.Jaunty.Tests.Helpers;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

[Collection("Type Handler Operations")]
public class ReaderMemoAndSchemaKeyTests : IDisposable
{
    public void Dispose()
    {
        JauntyConfig.ColumnNameResolver = null;
        GC.SuppressFinalize(this);
    }

    public class MemoEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class MemoPairLeft
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class MemoPairRight
    {
        public int Id { get; set; }
        public string? Code { get; set; }
    }

    public class E1 { public int A { get; set; } }
    public class E2 { public int B { get; set; } }
    public class E3 { public int C { get; set; } }
    public class E4 { public int D { get; set; } }
    public class E5 { public int F { get; set; } }
    public class E6 { public int G { get; set; } }
    public class E7 { public int H { get; set; } }

    private static int Flood => BoundedCacheLimits.SchemaCacheMaxEntries * 2;

    private static MutableStubReader ShapeWithDuplicateIds(int copies)
    {
        string?[] names = Enumerable.Repeat<string?>("Id", copies).Append("Name").ToArray();
        object?[] values = Enumerable.Repeat<object?>(1, copies).Append("n").ToArray();
        return new MutableStubReader(names, values);
    }

    [Fact]
    public void ASettersMemo_OutlivesItsEntryInTheSettersCache()
    {
        var reader = new MutableStubReader(["Id", "Name"], [1, "n"]);
        PropertySetter<MemoEntity>[] first = MetadataCache<MemoEntity>.GetSetters(reader, MappingMode.Projection);

        for (int copies = 2; copies < Flood; copies++)
            MetadataCache<MemoEntity>.GetSetters(ShapeWithDuplicateIds(copies), MappingMode.Projection);

        Assert.Same(first, MetadataCache<MemoEntity>.GetSetters(reader, MappingMode.Projection));
    }

    [Fact]
    public void APairMapperMemo_OutlivesItsEntryInTheSchemaCache()
    {
        var reader = new MutableStubReader(["Id", "Name", "Code"], [1, "n", "c"]);
        MultiEntityMapper<MemoPairLeft, MemoPairRight> first = MultiEntityMapper<MemoPairLeft, MemoPairRight>.Get(reader);

        for (int copies = 2; copies < Flood; copies++)
            MultiEntityMapper<MemoPairLeft, MemoPairRight>.Get(new MutableStubReader(
                Enumerable.Repeat<string?>("Id", copies).Append("Name").Append("Code").ToArray(),
                Enumerable.Repeat<object?>(1, copies).Append("n").Append("c").ToArray()));

        Assert.Same(first, MultiEntityMapper<MemoPairLeft, MemoPairRight>.Get(reader));
    }

    private static string KeyOf(Type mapper, IDataReader reader) =>
        (string)mapper.GetMethod("BuildSchemaKey", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [reader])!;

    public static TheoryData<int> Arities => new() { 2, 3, 4, 5, 6, 7 };

    private static Type MapperOfArity(int arity) => arity switch
    {
        2 => typeof(MultiEntityMapper<MemoPairLeft, MemoPairRight>),
        3 => typeof(MultiEntityMapper<E1, E2, E3>),
        4 => typeof(MultiEntityMapper<E1, E2, E3, E4>),
        5 => typeof(MultiEntityMapper<E1, E2, E3, E4, E5>),
        6 => typeof(MultiEntityMapper<E1, E2, E3, E4, E5, E6>),
        _ => typeof(MultiEntityMapper<E1, E2, E3, E4, E5, E6, E7>),
    };

    [Theory]
    [MemberData(nameof(Arities))]
    public void TheSchemaKey_JoinsGenerationCountAndNamesWithTheirSeparators(int arity)
    {
        var reader = new MutableStubReader(["Id", "Name", "Code"], [1, "n", "c"]);

        string key = KeyOf(MapperOfArity(arity), reader);

        Assert.Equal($"{ConfigurationGeneration.Current}|3\u001FId\u001FName\u001FCode", key);
    }

    [Theory]
    [MemberData(nameof(Arities))]
    public void TheSchemaKey_TreatsAMissingNameAsEmpty(int arity)
    {
        var reader = new MutableStubReader(["Id", null], [1, 2]);

        string key = KeyOf(MapperOfArity(arity), reader);

        Assert.Equal($"{ConfigurationGeneration.Current}|2\u001FId\u001F", key);
    }

    private const string Marker = "Stryker was here!";

    public class MarkedName
    {
        public int Id { get; set; }

        [Extrode.Jaunty.Attributes.Column(Marker)]
        public string? Name { get; set; }
    }

    public class MarkedId
    {
        [Extrode.Jaunty.Attributes.Column(Marker)]
        public int Id { get; set; }
    }

    [Fact]
    public void AReaderEntryForAMissingName_DoesNotRememberAPlaceholderAsAName()
    {
        MetadataCache<MarkedName>.GetSetters(new MutableStubReader(["", "Id"], [1, 2]), MappingMode.Projection);
        var reader = new MutableStubReader([null, "Id"], [1, 2]);
        Assert.Single(MetadataCache<MarkedName>.GetSetters(reader, MappingMode.Projection));

        reader.Reshape([Marker, "Id"], ["named", 1]);

        Assert.Equal(2, MetadataCache<MarkedName>.GetSetters(reader, MappingMode.Projection).Length);
    }

    [Fact]
    public void ASignatureForAMissingName_DoesNotCollideWithAPlaceholderNamedColumn()
    {
        MetadataCache<MarkedId>.GetSetters(new MutableStubReader([Marker], [1]), MappingMode.Projection);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            MetadataCache<MarkedId>.GetSetters(new MutableStubReader([null], [1]), MappingMode.Projection));

        Assert.Equal("Column 0 has no name", ex.Message);
    }

    [Fact]
    public void APairMapperEntryForAMissingName_DoesNotRememberAPlaceholderAsAName()
    {
        MultiEntityMapper<MarkedName, MemoPairRight>.Get(new MutableStubReader(["", "Id"], [1, 2]));
        var reader = new MutableStubReader([null, "Id"], [1, 2]);
        MultiEntityMapper<MarkedName, MemoPairRight> first = MultiEntityMapper<MarkedName, MemoPairRight>.Get(reader);

        reader.Reshape([Marker, "Id"], ["named", 1]);

        Assert.NotSame(first, MultiEntityMapper<MarkedName, MemoPairRight>.Get(reader));
    }
}
