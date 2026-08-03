using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-031. An entity with no mapped properties at all - every one <c>[Ignore]</c>d,
/// <c>[NotMapped]</c> or get-only - reached every write site and failed there, differently each time
/// and never naming the entity: <c>DivideByZeroException</c> from the batch inserts' rows-per-chunk
/// arithmetic, and a raw DuckDB parser error on <c>INSERT INTO "t" () VALUES ()</c> from the
/// single-entity ones. The condition is a property of the type, so it is now caught once where the
/// type is first inspected.
/// </summary>
public class UnmappedEntityGuardTests
{
    [Table("all_ignored")]
    private sealed class AllIgnored
    {
        [Ignore]
        public int Id { get; set; }

        [Ignore]
        public string Name { get; set; } = "";
    }

    [Table("all_not_mapped")]
    private sealed class AllNotMapped
    {
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int Id { get; set; }
    }

    [Table("all_get_only")]
    private sealed class AllGetOnly
    {
        public int Id => 1;
        public string Name => "";
    }

    [Table("no_properties")]
    private sealed class NoProperties
    {
    }

    [Table("one_mapped")]
    private sealed class OneMapped
    {
        [Ignore]
        public int Ignored { get; set; }

        public int Id { get; set; }
    }

    private static string MessageFor(Type type) =>
        Assert.Throws<InvalidOperationException>(() => ColumnMappingCache.Get(type)).Message;

    [Fact]
    public void EveryPropertyIgnored_ThrowsNamingTheEntity()
    {
        string message = MessageFor(typeof(AllIgnored));

        Assert.Contains(nameof(AllIgnored), message, StringComparison.Ordinal);
        Assert.Contains("no mapped properties", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPropertyNotMapped_Throws() =>
        Assert.Contains("no mapped properties", MessageFor(typeof(AllNotMapped)), StringComparison.Ordinal);

    [Fact]
    public void EveryPropertyGetOnly_Throws() =>
        Assert.Contains("no mapped properties", MessageFor(typeof(AllGetOnly)), StringComparison.Ordinal);

    [Fact]
    public void NoPropertiesAtAll_Throws() =>
        Assert.Contains("no mapped properties", MessageFor(typeof(NoProperties)), StringComparison.Ordinal);

    /// <summary>
    /// The message has to say what "mapped" means, or the caller is told their entity is wrong
    /// without being told what to change.
    /// </summary>
    [Fact]
    public void TheMessageExplainsWhatCountsAsMapped()
    {
        string message = MessageFor(typeof(AllIgnored));

        Assert.Contains("[Ignore]", message, StringComparison.Ordinal);
        Assert.Contains("[NotMapped]", message, StringComparison.Ordinal);
        Assert.Contains("setter", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control: one surviving property is enough, and nothing about the ordinary case changes.
    /// </summary>
    [Fact]
    public void OneMappedPropertyIsEnough()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(OneMapped));

        Assert.Single(mappings);
        Assert.True(mappings.ContainsKey("Id"));
    }

    /// <summary>
    /// Nothing is cached when the guard throws, so a second call reports the same thing rather than
    /// handing back an empty mapping set.
    /// </summary>
    [Fact]
    public void TheGuardThrowsEveryTime()
    {
        Assert.Throws<InvalidOperationException>(() => ColumnMappingCache.Get(typeof(AllIgnored)));
        Assert.Throws<InvalidOperationException>(() => ColumnMappingCache.Get(typeof(AllIgnored)));
    }
}
