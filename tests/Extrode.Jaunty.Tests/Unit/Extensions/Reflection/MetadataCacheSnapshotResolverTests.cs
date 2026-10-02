using System.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Internals.Entity;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// A <c>MetadataCache</c> snapshot answers <c>GetSetters</c> and <c>ColumnBindsTo</c> from the
/// column-name resolver it was built under, so a multi-entity rebind that holds one snapshot cannot
/// consult a resolver swapped in between its two calls.
/// </summary>
[Collection("Configuration Operations")]
public class MetadataCacheSnapshotResolverTests : IDisposable
{
    public MetadataCacheSnapshotResolverTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose()
    {
        JauntyConfig.ColumnNameResolver = null;
        GC.SuppressFinalize(this);
    }

    [Table("snapshot_resolver_rows")]
    public class Row
    {
        [Key]
        public int Id { get; set; }

        [Column("col_a")]
        public string? A { get; set; }
    }

    private static MetadataCache<Row>.Snapshot SnapshotUnderOldResolverThenSwap()
    {
        JauntyConfig.ColumnNameResolver = name => "old_" + name;
        MetadataCache<Row>.Snapshot snapshot = MetadataCache<Row>.CurrentSnapshot;
        JauntyConfig.ColumnNameResolver = name => "new_" + name;
        return snapshot;
    }

    [Fact]
    public void ColumnBindsTo_UsesTheResolverTheSnapshotWasBuiltUnder()
    {
        MetadataCache<Row>.Snapshot snapshot = SnapshotUnderOldResolverThenSwap();
        PropertyContext<Row> a = snapshot.Properties.Single(p => p.Property.Name == nameof(Row.A));

        Assert.True(snapshot.ColumnBindsTo("old_A", a));
        Assert.False(snapshot.ColumnBindsTo("new_A", a));
    }

    [Fact]
    public void GetSetters_UsesTheResolverTheSnapshotWasBuiltUnder()
    {
        MetadataCache<Row>.Snapshot snapshot = SnapshotUnderOldResolverThenSwap();
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("old_A", typeof(string));
        using IDataReader reader = table.CreateDataReader();

        PropertySetter<Row>[] setters = snapshot.GetSetters(reader, MappingMode.Projection);

        Assert.Contains(setters, s => s.Context.Property.Name == nameof(Row.A) && s.Ordinal == 1);
    }
}
