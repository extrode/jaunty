using System.Data;
using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals;

using Xunit;

using CoreMapper2 = Jaunty.Internals.Read.MultiEntityMapper<Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.CoreLeft, Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.CoreRight>;
using CoreMapper3 = Jaunty.Internals.Read.MultiEntityMapper<Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.CoreLeft, Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.CoreRight, Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.CoreThird>;
using ReflectionMapper2 = Jaunty.Extensions.Reflection.MultiEntityMapper<Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.ReflLeft, Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.ReflRight>;
using ReflectionMapper3 = Jaunty.Extensions.Reflection.MultiEntityMapper<Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.ReflLeft, Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.ReflRight, Jaunty.Tests.Unit.Internals.SchemaCacheBoundsTests.ReflThird>;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R26-053 (batch 6, medium/performance). Thirteen process-lifetime caches were keyed on a
/// string derived from the result set's column names - which the caller chooses, through the SELECT
/// list - and every one was a <c>ConcurrentDictionary</c> on a <c>static readonly</c> field that
/// nothing ever removed from. Any workload that varies its projection (<c>QueryPartial</c> with
/// caller-chosen columns, a reporting screen, a search that returns different fields per request)
/// added a permanent entry per distinct shape, for the process lifetime.
///
/// <para>
/// Measured before the fix: 10,000 distinct shapes produced 10,001 entries in both the setter cache
/// and the arity-2 mapper cache, at 586 B and 2,047 B of retained heap per shape - about 20 MB per
/// 10,000 shapes on the mapper path. After: 256 entries, the cap.
/// </para>
///
/// <para>
/// The library already had the fix and had not applied it here.
/// <see cref="BoundedCache{TKey, TValue}"/>'s own summary names this exact scenario, and it had two
/// adopters, both in <c>Internals/Parameters</c>. The finding named the three files in
/// Jaunty.Extensions.Reflection; the same mechanism, reached through the same public
/// <c>Query&lt;T1, T2, ...&gt;</c> surface, was also in <c>Jaunty/Internals/Read</c> - six more
/// caches in the assembly that <em>defines</em> <c>BoundedCache</c>. Both are covered here.
/// </para>
/// </summary>
/// <remarks>
/// These assertions read private static fields by reflection. That is deliberate: boundedness is a
/// property of the storage, not of anything the mappers return, so there is nothing observable to
/// assert on instead. Exposing a count on each of the thirteen caches purely to be asserted here
/// would put more test-only surface into product code than the reflection costs.
/// </remarks>
[Collection("Type Handler Operations")]
public class SchemaCacheBoundsTests
{
    private const int Cap = BoundedCacheLimits.SchemaCacheMaxEntries;

    // One dedicated entity type per family. These caches are static fields on generic types, so the
    // cache instance is per closed generic type - sharing an entity type with another test file
    // would let that file's entries count against this file's cap and make the assertions depend on
    // execution order.
    [Table("bounds_refl_left")]
    public class ReflLeft
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("bounds_refl_right")]
    public class ReflRight
    {
        [Key]
        public int RightId { get; set; }
        public string? Label { get; set; }
    }

    [Table("bounds_refl_third")]
    public class ReflThird
    {
        [Key]
        public int ThirdId { get; set; }
    }

    [Table("bounds_core_left")]
    public class CoreLeft
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("bounds_core_right")]
    public class CoreRight
    {
        [Key]
        public int RightId { get; set; }
        public string? Label { get; set; }
    }

    [Table("bounds_core_third")]
    public class CoreThird
    {
        [Key]
        public int ThirdId { get; set; }
    }

    [Table("bounds_setters")]
    public class SetterWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public SchemaCacheBoundsTests() => JauntyReflectionExtensions.UseReflectionMapping();

    // ------------------------------------------------------------------
    // Reading the caches
    // ------------------------------------------------------------------

    private static int CountOfStaticCache(Type owner, string fieldName)
    {
        FieldInfo field = owner.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"'{owner.Name}' has no non-public static field '{fieldName}'. If the cache was " +
                "renamed or restructured, this test needs updating - it is the only thing checking " +
                "that the cache is still bounded.");

        return CountOf(field.GetValue(null)!);
    }

    private static int SetterCacheCount()
    {
        FieldInfo snapshotField = typeof(MetadataCache<SetterWidget>)
            .GetField("_snapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
        object snapshot = snapshotField.GetValue(null)!;
        FieldInfo settersField = snapshot.GetType()
            .GetField("SettersCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return CountOf(settersField.GetValue(snapshot)!);
    }

    private static int CountOf(object cache)
    {
        Type type = cache.GetType();

        Assert.True(
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BoundedCache<,>),
            $"Expected a BoundedCache so the cache is capped; found {type.Name}. A plain " +
            "ConcurrentDictionary here is the AUD-R26-053 leak.");

        PropertyInfo count = type.GetProperty(
            "Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (int)count.GetValue(cache)!;
    }

    // ------------------------------------------------------------------
    // The cap holds
    // ------------------------------------------------------------------

    /// <summary>
    /// The single-entity setter cache, reached by every reflection-mapped query. Driven well past
    /// the cap so the assertion cannot pass by simply not having filled it.
    /// </summary>
    [Fact]
    public void SetterCache_StopsAtTheCap()
    {
        for (int i = 0; i < Cap * 4; i++)
        {
            MetadataCache<SetterWidget>.GetSetters(
                new ShapeReader(["Id", "Name", "extra_" + i.ToString()]), MappingMode.Projection);
        }

        Assert.True(SetterCacheCount() <= Cap, $"Setter cache held {SetterCacheCount()} entries, cap is {Cap}.");
    }

    [Fact]
    public void ReflectionArity2MapperCache_StopsAtTheCap()
    {
        for (int i = 0; i < Cap * 4; i++)
            ReflectionMapper2.Get(new ShapeReader(["Id", "Name", "RightId", "Label", "extra_" + i.ToString()]));

        int count = CountOfStaticCache(typeof(ReflectionMapper2), "Cache");
        Assert.True(count <= Cap, $"Reflection arity-2 mapper cache held {count} entries, cap is {Cap}.");
    }

    /// <summary>
    /// Arities 3 through 7 are five copies of one implementation, so one of them standing in for the
    /// family is enough - what would break them individually is a copy-paste slip, and
    /// <see cref="EveryMultiEntityMapperCache_IsBounded"/> covers that by walking all of them.
    /// </summary>
    [Fact]
    public void ReflectionNaryMapperCache_StopsAtTheCap()
    {
        for (int i = 0; i < Cap * 4; i++)
        {
            ReflectionMapper3.Build(
                new ShapeReader(["Id", "Name", "RightId", "Label", "ThirdId", "extra_" + i.ToString()]));
        }

        int count = CountOfStaticCache(typeof(ReflectionMapper3), "Cache");
        Assert.True(count <= Cap, $"Reflection arity-3 mapper cache held {count} entries, cap is {Cap}.");
    }

    [Fact]
    public void CoreArity2MapperCache_StopsAtTheCap()
    {
        for (int i = 0; i < Cap * 4; i++)
            CoreMapper2.Build(new ShapeReader(["Id", "Name", "RightId", "Label", "extra_" + i.ToString()]));

        int count = CountOfStaticCache(typeof(CoreMapper2), "_cache");
        Assert.True(count <= Cap, $"Core arity-2 mapper cache held {count} entries, cap is {Cap}.");
    }

    [Fact]
    public void CoreNaryMapperCache_StopsAtTheCap()
    {
        for (int i = 0; i < Cap * 4; i++)
        {
            CoreMapper3.Build(
                new ShapeReader(["Id", "Name", "RightId", "Label", "ThirdId", "extra_" + i.ToString()]));
        }

        int count = CountOfStaticCache(typeof(CoreMapper3), "_cache");
        Assert.True(count <= Cap, $"Core arity-3 mapper cache held {count} entries, cap is {Cap}.");
    }

    /// <summary>
    /// Walks every schema-keyed mapper cache in both assemblies and checks the storage type. The
    /// per-arity tests above drive real traffic through two of them; this one catches the case those
    /// cannot - one of the thirteen declarations being reverted, or a new arity being added by
    /// copying an older one, without any test noticing until a production heap grew.
    /// </summary>
    [Fact]
    public void EveryMultiEntityMapperCache_IsBounded()
    {
        Type[] stand_ins =
        [
            typeof(CoreMapper2), typeof(CoreMapper3),
            typeof(ReflectionMapper2), typeof(ReflectionMapper3),
        ];

        var checkedTypes = new List<string>();

        foreach (Type openOwner in AllMultiEntityMapperDefinitions(stand_ins))
        {
            Type closed = CloseOverPlaceholders(openOwner);
            FieldInfo field = closed.GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic)
                ?? closed.GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"No schema cache field found on {closed}.");

            CountOf(field.GetValue(null)!);
            checkedTypes.Add(closed.Name + "`" + closed.GetGenericArguments().Length.ToString());
        }

        // 2 assemblies x arities 2..7. A drop here means a mapper stopped being discovered, which
        // would silently shrink this test's coverage to whatever remained.
        Assert.Equal(12, checkedTypes.Count);
    }

    private static IEnumerable<Type> AllMultiEntityMapperDefinitions(Type[] standIns)
    {
        var seen = new HashSet<Type>();

        foreach (Type standIn in standIns)
        {
            Type definition = standIn.GetGenericTypeDefinition();

            foreach (Type candidate in definition.Assembly.GetTypes())
            {
                if (!candidate.IsGenericTypeDefinition) continue;
                if (candidate.Namespace != definition.Namespace) continue;
                if (!candidate.Name.StartsWith("MultiEntityMapper`", StringComparison.Ordinal)) continue;
                if (seen.Add(candidate)) yield return candidate;
            }
        }
    }

    // The mappers are constrained to new(), so any concrete entity type closes them; which one is
    // irrelevant because only the storage type is being read.
    private static Type CloseOverPlaceholders(Type openOwner)
    {
        int arity = openOwner.GetGenericArguments().Length;
        var args = new Type[arity];
        for (int i = 0; i < arity; i++) args[i] = typeof(SetterWidget);
        return openOwner.MakeGenericType(args);
    }

    // ------------------------------------------------------------------
    // Capping did not break caching, and eviction did not break mapping
    // ------------------------------------------------------------------

    /// <summary>
    /// Below the cap the cache must still be a cache. Without this, "bounded" could be satisfied by
    /// never storing anything at all.
    /// </summary>
    [Fact]
    public void BelowTheCap_TheSameShapeIsStillCached()
    {
        var first = new ShapeReader(["Id", "Name"]);
        var second = new ShapeReader(["Id", "Name"]);

        // Distinct reader instances, so this goes past the per-reader memo to the shared cache.
        PropertySetter<SetterWidget>[] a = MetadataCache<SetterWidget>.GetSetters(first, MappingMode.Projection);
        PropertySetter<SetterWidget>[] b = MetadataCache<SetterWidget>.GetSetters(second, MappingMode.Projection);

        Assert.Same(a, b);
    }

    [Fact]
    public void BelowTheCap_TheSameShapeStillReusesTheMapper()
    {
        var first = new ShapeReader(["Id", "Name", "RightId", "Label"]);
        var second = new ShapeReader(["Id", "Name", "RightId", "Label"]);

        Assert.Same(ReflectionMapper2.Get(first), ReflectionMapper2.Get(second));
    }

    /// <summary>
    /// An evicted shape has to rebuild correctly, not just rebuild. Eviction is FIFO, so the first
    /// shape inserted is the first to go - this maps a row through the rebuilt setters and checks
    /// the values land in the right properties, which is what would break if a rebuild bound to
    /// stale ordinals.
    /// </summary>
    [Fact]
    public void AnEvictedShape_RebuildsSettersThatStillMapCorrectly()
    {
        var original = new ShapeReader(["Id", "Name"], [7, "seven"]);
        PropertySetter<SetterWidget>[] before = MetadataCache<SetterWidget>.GetSetters(original, MappingMode.Projection);

        // Push the original past the cap. FIFO eviction means it is gone well before this ends.
        for (int i = 0; i < Cap * 2; i++)
        {
            MetadataCache<SetterWidget>.GetSetters(
                new ShapeReader(["Id", "Name", "evict_" + i.ToString()]), MappingMode.Projection);
        }

        var again = new ShapeReader(["Id", "Name"], [7, "seven"]);
        PropertySetter<SetterWidget>[] after = MetadataCache<SetterWidget>.GetSetters(again, MappingMode.Projection);

        Assert.NotSame(before, after);

        var widget = new SetterWidget();
        Assert.True(again.Read());
        foreach (PropertySetter<SetterWidget> setter in after)
            setter.Set(widget, again);

        Assert.Equal(7, widget.Id);
        Assert.Equal("seven", widget.Name);
    }

    // ------------------------------------------------------------------

    private sealed class ShapeReader(string[] names, object[]? values = null, int rows = 1) : IDataReader
    {
        private int _row;
        private object[] Values => values ?? [];

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => Values.Length > i ? Values[i] : 0;
        public bool IsDBNull(int i) => GetValue(i) is null or DBNull;
        public Type GetFieldType(int i) => GetValue(i).GetType();
        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
        public string GetString(int i) => (string)GetValue(i);
        public bool Read() => _row++ < rows;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public void Close() { }
        public void Dispose() { }
        public DataTable? GetSchemaTable() => null;
        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));
        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => 0;
        public long GetInt64(int i) => 0;
        public int GetValues(object[] target) => 0;
    }
}
