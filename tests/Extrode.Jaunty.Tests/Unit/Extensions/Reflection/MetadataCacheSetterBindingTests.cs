using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Tests.Helpers;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// <c>MetadataCache&lt;T&gt;.GetSetters</c>: the per-reader memo under a reshaped reader, strict-mode
/// and nameless-column failures, and a <c>[Column]</c> name the resolver does not alias.
/// </summary>
[Collection("Type Handler Operations")]
public class MetadataCacheSetterBindingTests : IDisposable
{
    public void Dispose()
    {
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AReaderThatGainsAColumn_IsRemappedInsteadOfServedFromTheMemo()
    {
        var reader = new MutableStubReader(["Id"], [1]);
        Assert.Single(MetadataCache<Grown>.GetSetters(reader, MappingMode.Projection));

        reader.Reshape(["Id", "Name"], [1, "n"]);
        PropertySetter<Grown>[] setters = MetadataCache<Grown>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal(2, setters.Length);
    }

    [Fact]
    public void StrictMode_AnUnmappedColumn_Throws()
    {
        var reader = new MutableStubReader(["Id", "Extra"], [1, 2]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            MetadataCache<StrictTarget>.GetSetters(reader, MappingMode.Strict));

        Assert.Equal(
            $"Mapping failed: Column 'Extra' does not map to any property of type '{typeof(StrictTarget).FullName}'.",
            ex.Message);
    }

    [Fact]
    public void AColumnWithNoName_Throws()
    {
        var reader = new MutableStubReader([null], [1]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            MetadataCache<Nameless>.GetSetters(reader, MappingMode.Projection));

        Assert.Equal("Column 0 has no name", ex.Message);
    }

    [Fact]
    public void AColumnAttributeName_IsTheOnlyNameItsPropertyBindsBy()
    {
        var asked = new List<string>();
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => { lock (asked) asked.Add(name); return name == nameof(Renamed.OrderId) ? "order_id" : null!; });
        var reader = new MutableStubReader(["order_id", "oid", "Other"], [7, 9, 3]);

        PropertySetter<Renamed>[] setters = MetadataCache<Renamed>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal([(nameof(Renamed.OrderId), 1), (nameof(Renamed.Other), 2)], setters.Select(s => (s.Context.PropertyName, s.Ordinal)));
        Assert.DoesNotContain(nameof(Renamed.OrderId), asked);
    }

    private sealed class Grown
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class StrictTarget
    {
        public int Id { get; set; }
    }

    private sealed class Nameless
    {
        public int Id { get; set; }
    }

    private sealed class Renamed
    {
        [Column("oid")] public int OrderId { get; set; }
        public int Other { get; set; }
    }
}
