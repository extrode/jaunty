using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Internals.Entity;
using Extrode.Jaunty.Internals.Write;

namespace Extrode.Jaunty.Tests.Unit.Internals.Write;

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

    private sealed class PlaceholderProbeEntity { }

    private sealed class PlaceholderProbeConnection { }

    [Fact]
    public void GetOrBuild_ColumnNamesWithNonParameterCharacters_GetSanitisedPlaceholdersAndKeepTheirColumnNames()
    {
        var metadata = new EntityMetadata("probe_table", null,
        [
            MarkerColumn("price$usd", "x"),
            MarkerColumn("qty", "y")
        ]);

        string sql = MultiRowInsertCache.GetOrBuild(
            typeof(PlaceholderProbeEntity), typeof(PlaceholderProbeConnection), 2, metadata, new PostgreSqlDialect());

        Assert.Contains("(price$usd, qty)", sql);
        Assert.Contains("(@price_usd_0, @qty_0), (@price_usd_1, @qty_1)", sql);
    }

    private sealed class CollidingStemProbeEntity { }

    [Fact]
    public void GetOrBuild_ColumnsWhoseStemsCollide_GetDistinctPlaceholders()
    {
        var metadata = new EntityMetadata("probe_table", null,
        [
            MarkerColumn("Price$", "x"),
            MarkerColumn("Price_", "y"),
            MarkerColumn("Price__2", "z")
        ]);

        string sql = MultiRowInsertCache.GetOrBuild(
            typeof(CollidingStemProbeEntity), typeof(PlaceholderProbeConnection), 2, metadata, new PostgreSqlDialect());

        Assert.Equal(["Price_", "Price__3", "Price__2"], MultiRowInsertCache.ParameterStems(metadata.InsertColumns));
        Assert.Contains("(Price$, Price_, Price__2)", sql);
        Assert.Contains("(@Price__0, @Price__3_0, @Price__2_0), (@Price__1, @Price__3_1, @Price__2_1)", sql);
    }

    [Theory]
    [InlineData("ProductName", "ProductName")]
    [InlineData("price$usd", "price_usd")]
    [InlineData("#code", "_code")]
    [InlineData("Unit Price", "Unit_Price")]
    public void ParameterStem_KeepsLettersDigitsAndUnderscores(string columnName, string expected)
    {
        Assert.Equal(expected, MultiRowInsertCache.ParameterStem(columnName));
    }
}
