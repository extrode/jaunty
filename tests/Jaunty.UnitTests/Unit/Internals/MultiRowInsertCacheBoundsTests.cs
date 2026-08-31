using System.Data;
using System.Data.SQLite;
using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Dialects;
using Jaunty.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-058. <c>MultiRowInsertCache._cache</c> is keyed by (entity type, connection type, batch
/// size) and was a <c>ConcurrentDictionary</c> nothing ever removed from. The batch-size component
/// makes that key caller-shaped rather than type-shaped: <c>BulkInsertMultiRow</c> passes
/// <c>Math.Min(maxBatchSize, entityCount - offset)</c>, so every bulk insert whose row count is not
/// an exact multiple of <c>maxBatchSize</c> leaves a permanent entry behind for its remainder size.
/// A workload with varying collection sizes therefore retained up to <c>maxBatchSize</c> distinct
/// SQL strings per (entity type, connection type) pair, each O(columns x batchSize) characters.
/// <para>
/// AUD-R26-053 converted thirteen caller-shaped caches to <see cref="BoundedCache{TKey, TValue}"/>
/// and named only this file's <c>_getterCache</c> as deliberately exempt, on grounds - compiled
/// delegates, metadata-shaped key - that do not describe <c>_cache</c>.
/// </para>
/// </summary>
/// <remarks>
/// Reads the private static field by reflection for the same reason
/// <see cref="SchemaCacheBoundsTests"/> does: boundedness is a property of the storage, with nothing
/// observable to assert on instead.
/// </remarks>
public class MultiRowInsertCacheBoundsTests
{
    private const int Cap = BoundedCacheLimits.SchemaCacheMaxEntries;

    [Table("bounds_multirow_widgets")]
    public class BoundsWidget
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Quantity { get; set; }
    }

    private static EntityMetadata Metadata() =>
        new(
            "bounds_multirow_widgets",
            null,
            [
                new ColumnMetadata("Id", typeof(int), "Id", isPrimaryKey: true, isIdentity: false, isComputed: false, null, null, null),
                new ColumnMetadata("Name", typeof(string), "Name", isPrimaryKey: false, isIdentity: false, isComputed: false, null, null, null),
                new ColumnMetadata("Quantity", typeof(int), "Quantity", isPrimaryKey: false, isIdentity: false, isComputed: false, null, null, null),
            ]);

    private static string Build(int batchSize) =>
        MultiRowInsertCache.GetOrBuild(
            typeof(BoundsWidget), typeof(SQLiteConnection), batchSize, Metadata(), new SQLiteDialect());

    private static object Cache()
    {
        FieldInfo field = typeof(MultiRowInsertCache).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "MultiRowInsertCache has no non-public static field '_cache'. If it was renamed or " +
                "restructured, this test needs updating - it is the only thing checking the cache is bounded.");

        return field.GetValue(null)!;
    }

    private static int Count()
    {
        object cache = Cache();
        Type type = cache.GetType();

        Assert.True(
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BoundedCache<,>),
            $"Expected a BoundedCache so the cache is capped; found {type.Name}. A plain " +
            "ConcurrentDictionary here is the unbounded growth AUD-R35-058 filed.");

        PropertyInfo count = type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (int)count.GetValue(cache)!;
    }

    /// <summary>
    /// The scenario as it reaches this cache: 2,000 distinct remainder batch sizes. Unbounded, that
    /// is 2,000 permanent entries; bounded, it is the cap.
    /// </summary>
    [Fact]
    public void ManyDistinctBatchSizes_DoNotGrowTheCacheWithoutLimit()
    {
        for (int batchSize = 1; batchSize <= 2000; batchSize++)
            Build(batchSize);

        Assert.True(Count() <= Cap, $"Expected at most {Cap} entries; found {Count()}.");
    }

    /// <summary>
    /// Bounding must not break the cache: the SQL for a given batch size is still correct after the
    /// entry for it has been evicted and rebuilt.
    /// </summary>
    [Fact]
    public void SqlIsStillCorrectAfterEviction()
    {
        string first = Build(3);

        for (int batchSize = 1000; batchSize <= 1000 + Cap + 10; batchSize++)
            Build(batchSize);

        string rebuilt = Build(3);

        Assert.Equal(first, rebuilt);
        Assert.Contains("INSERT INTO", rebuilt, StringComparison.Ordinal);
        Assert.Contains("@Name_2", rebuilt, StringComparison.Ordinal);
        Assert.DoesNotContain("@Name_3", rebuilt, StringComparison.Ordinal);
    }

    /// <summary>
    /// A repeated lookup of one key still hits: bounding changed the storage, not the caching.
    /// </summary>
    [Fact]
    public void RepeatedLookupsOfOneBatchSizeAddOneEntry()
    {
        int before = Count();

        for (int i = 0; i < 50; i++)
            Build(7);

        Assert.True(Count() <= before + 1, $"Expected at most one new entry; went from {before} to {Count()}.");
    }
}
