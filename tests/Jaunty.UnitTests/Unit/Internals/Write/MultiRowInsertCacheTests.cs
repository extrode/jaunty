using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty.Tests.Unit.Internals.Write;

public class MultiRowInsertCacheTests
{
    // GetOrBuildGetters caches compiled getters keyed by (entity type, layout key), where the
    // layout key used to be built by concatenating column names with no separator. Column sets
    // ["ab", "c"] and ["a", "bc"] both concatenate (with the "2" column-count prefix) to the
    // identical "2abc", so under the old bug the second call's getters would silently be served
    // the first call's stale, mismatched getters. Marker getters below (independent of any real
    // CLR property) make that cross-contamination directly observable.
    private sealed class CacheKeyCollisionProbeEntity { }

    private static ColumnMetadata MarkerColumn(string columnName, string markerValue) =>
        new("Prop_" + columnName, typeof(string), columnName, isPrimaryKey: false, isIdentity: false, isComputed: false,
            getter: _ => markerValue, setter: (_, _) => { });

    [Fact]
    public void GetOrBuildGetters_ColumnNamesThatWouldCollideUnderNaiveConcatenation_ProduceDistinctGetters()
    {
        var metadataA = new EntityMetadata("probe_table", null,
        [
            MarkerColumn("ab", "FROM_AB"),
            MarkerColumn("c", "FROM_C")
        ]);
        var metadataB = new EntityMetadata("probe_table", null,
        [
            MarkerColumn("a", "FROM_A"),
            MarkerColumn("bc", "FROM_BC")
        ]);

        Func<CacheKeyCollisionProbeEntity, object?>[] gettersA =
            MultiRowInsertCache.GetOrBuildGetters<CacheKeyCollisionProbeEntity>(metadataA);
        Func<CacheKeyCollisionProbeEntity, object?>[] gettersB =
            MultiRowInsertCache.GetOrBuildGetters<CacheKeyCollisionProbeEntity>(metadataB);

        var dummy = new CacheKeyCollisionProbeEntity();

        Assert.Equal("FROM_AB", gettersA[0](dummy));
        Assert.Equal("FROM_C", gettersA[1](dummy));

        // Under the old bug, this would incorrectly return metadataA's cached getters
        // ("FROM_AB"/"FROM_C") because both layouts hashed to the same unseparated key.
        Assert.Equal("FROM_A", gettersB[0](dummy));
        Assert.Equal("FROM_BC", gettersB[1](dummy));
    }
}
