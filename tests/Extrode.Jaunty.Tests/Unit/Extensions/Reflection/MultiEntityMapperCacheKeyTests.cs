using System.Data;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Tests.Helpers;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// The multi-entity mappers' two caches - the per-reader memo (arity 2) and the schema-key cache
/// (every arity) - must never hand back a mapper built for a different column layout or an older
/// configuration generation. Also pins left-to-right claiming of a column name shared by every
/// entity, for arities 3 to 7.
/// </summary>
[Collection("Type Handler Operations")]
public class MultiEntityMapperCacheKeyTests : IDisposable
{
    public void Dispose()
    {
        JauntyConfig.ColumnNameResolver = null;
        GC.SuppressFinalize(this);
    }

    private static readonly Func<string, string> SnakeOrderId =
        static name => name == nameof(PairRight.OrderId) ? "order_id" : name;

    // ------------------------------------------------------------------
    // Arity 2
    // ------------------------------------------------------------------

    private static PairRight MapPair(IDataReader reader)
    {
        var mapper = MultiEntityMapper<PairLeft, PairRight>.Get(reader);
        var left = new PairLeft();
        var right = new PairRight();
        mapper.Map(left, right, reader);
        return right;
    }

    [Fact]
    public void Pair_SameReader_AConfigurationChangeIsNotServedFromTheMemo()
    {
        var reader = new MutableStubReader(["Id", "order_id"], [1, 7]);
        Assert.Equal(0, MapPair(reader).OrderId);

        JauntyConfig.ColumnNameResolver = SnakeOrderId;

        Assert.Equal(7, MapPair(reader).OrderId);
    }

    [Fact]
    public void Pair_NewReader_AConfigurationChangeIsNotServedFromTheSchemaCache()
    {
        Assert.Equal(0, MapPair(new MutableStubReader(["Id", "order_id"], [1, 7])).OrderId);

        JauntyConfig.ColumnNameResolver = SnakeOrderId;

        Assert.Equal(7, MapPair(new MutableStubReader(["Id", "order_id"], [1, 7])).OrderId);
    }

    [Fact]
    public void Pair_SameReader_AnAddedColumnIsMapped()
    {
        var reader = new MutableStubReader(["Id"], [1]);
        MapPair(reader);

        reader.Reshape(["Id", "Code"], [1, "c"]);

        Assert.Equal("c", MapPair(reader).Code);
    }

    [Fact]
    public void Pair_SameReader_ARenamedColumnIsMapped()
    {
        var reader = new MutableStubReader(["Id", "Name"], [1, "n"]);
        MapPair(reader);

        reader.Reshape(["Id", "Code"], [1, "c"]);

        Assert.Equal("c", MapPair(reader).Code);
    }

    [Fact]
    public void Pair_NewReader_ADifferentShapeOfTheSameWidthIsMapped()
    {
        MapPair(new MutableStubReader(["Id", "Name"], [1, "n"]));

        Assert.Equal("c", MapPair(new MutableStubReader(["Id", "Code"], [1, "c"])).Code);
    }

    // ------------------------------------------------------------------
    // Arities 3 to 7
    // ------------------------------------------------------------------

    public static TheoryData<int> Arities => new() { 3, 4, 5, 6, 7 };

    private static Slot[] Map(int arity, IDataReader r)
    {
        switch (arity)
        {
            case 3:
            {
                var m = MultiEntityMapper<S1, S2, S3>.Build(r);
                S1 a = new(); S2 b = new(); S3 c = new();
                m.ApplyT1(a, r); m.ApplyT2(b, r); m.ApplyT3(c, r);
                return [a, b, c];
            }
            case 4:
            {
                var m = MultiEntityMapper<S1, S2, S3, S4>.Build(r);
                S1 a = new(); S2 b = new(); S3 c = new(); S4 d = new();
                m.ApplyT1(a, r); m.ApplyT2(b, r); m.ApplyT3(c, r); m.ApplyT4(d, r);
                return [a, b, c, d];
            }
            case 5:
            {
                var m = MultiEntityMapper<S1, S2, S3, S4, S5>.Build(r);
                S1 a = new(); S2 b = new(); S3 c = new(); S4 d = new(); S5 e = new();
                m.ApplyT1(a, r); m.ApplyT2(b, r); m.ApplyT3(c, r); m.ApplyT4(d, r); m.ApplyT5(e, r);
                return [a, b, c, d, e];
            }
            case 6:
            {
                var m = MultiEntityMapper<S1, S2, S3, S4, S5, S6>.Build(r);
                S1 a = new(); S2 b = new(); S3 c = new(); S4 d = new(); S5 e = new(); S6 f = new();
                m.ApplyT1(a, r); m.ApplyT2(b, r); m.ApplyT3(c, r); m.ApplyT4(d, r); m.ApplyT5(e, r); m.ApplyT6(f, r);
                return [a, b, c, d, e, f];
            }
            default:
            {
                var m = MultiEntityMapper<S1, S2, S3, S4, S5, S6, S7>.Build(r);
                S1 a = new(); S2 b = new(); S3 c = new(); S4 d = new(); S5 e = new(); S6 f = new(); S7 g = new();
                m.ApplyT1(a, r); m.ApplyT2(b, r); m.ApplyT3(c, r); m.ApplyT4(d, r); m.ApplyT5(e, r); m.ApplyT6(f, r); m.ApplyT7(g, r);
                return [a, b, c, d, e, f, g];
            }
        }
    }

    private static MutableStubReader Ids(int count, params (string Name, object Value)[] prefix)
    {
        var names = prefix.Select(p => (string?)p.Name).Concat(Enumerable.Repeat<string?>("Id", count)).ToArray();
        var values = prefix.Select(p => (object?)p.Value).Concat(Enumerable.Range(1, count).Select(i => (object?)i)).ToArray();
        return new MutableStubReader(names, values);
    }

    [Theory]
    [MemberData(nameof(Arities))]
    public void ASharedColumnName_IsClaimedLeftToRight(int arity)
    {
        Slot[] slots = Map(arity, Ids(arity));

        Assert.Equal(Enumerable.Range(1, arity), slots.Select(s => s.Id));
    }

    [Theory]
    [MemberData(nameof(Arities))]
    public void ADifferentShapeOfTheSameWidth_IsNotServedFromTheSchemaCache(int arity)
    {
        Map(arity, Ids(arity));

        Slot[] slots = Map(arity, Ids(arity - 1, ("Tag", "t")));

        Assert.Equal("t", slots[0].Tag);
        Assert.Equal(Enumerable.Range(1, arity - 1).Append(0), slots.Select(s => s.Id));
    }

    [Theory]
    [MemberData(nameof(Arities))]
    public void AConfigurationChange_IsNotServedFromTheSchemaCache(int arity)
    {
        Assert.Equal(0, Map(arity, Ids(arity, ("order_id", 7)))[0].OrderId);

        JauntyConfig.ColumnNameResolver = SnakeOrderId;

        Assert.Equal(7, Map(arity, Ids(arity, ("order_id", 7)))[0].OrderId);
    }

    private sealed class PairLeft
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class PairRight
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public int OrderId { get; set; }
    }

    private abstract class Slot
    {
        public int Id { get; set; }
        public string? Tag { get; set; }
        public int OrderId { get; set; }
    }

    private sealed class S1 : Slot;
    private sealed class S2 : Slot;
    private sealed class S3 : Slot;
    private sealed class S4 : Slot;
    private sealed class S5 : Slot;
    private sealed class S6 : Slot;
    private sealed class S7 : Slot;
}
