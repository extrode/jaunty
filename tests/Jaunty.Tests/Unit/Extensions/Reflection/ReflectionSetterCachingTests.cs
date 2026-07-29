using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Extensions.Reflection;

using Xunit;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// AUD-R26, two findings against <c>MetadataCache&lt;T&gt;.GetSetters</c>.
///
/// <para>
/// <b>Per-row schema key (performance).</b> <c>GetTypedMapper&lt;T&gt;</c>'s delegate maps one row to
/// one entity, so <c>GetSetters</c> runs once per row of every reflection-mapped result set - and
/// before the lookup could even be attempted it allocated a <c>string[fieldCount + 2]</c> and joined
/// it into a schema key. Measured 274.3 B/row at 3 columns and 1098.3 B/row at 20. The same
/// <see cref="IDataReader"/> instance is passed for every row, so a
/// <c>ConditionalWeakTable</c> memo keyed on reader identity replaces that with an O(columns)
/// comparison from the second row on. <c>MultiEntityMapper&lt;T1,T2&gt;</c> already had exactly this;
/// the single-entity path - the far more common one - never got it.
/// </para>
///
/// <para>
/// <b>Resolver omitted from the cache key (bug).</b> <c>BuildSetters</c> consults
/// <see cref="JauntyConfig.ColumnNameResolver"/> to match columns onto properties, so the setters it
/// produces depend on it - but the cache key was the mapping mode and column names only. Registering
/// or changing a resolver after a given column shape had been mapped once returned the stale setters
/// forever, silently mapping nothing.
/// </para>
///
/// <para>
/// The memo is only safe because every hit is re-validated against the reader's live schema: some
/// providers (Npgsql) recycle one reader instance across commands on a pooled connection, which is
/// the AUD-R9-011 regression. <see cref="RecycledReader"/> below models that directly.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class ReflectionSetterCachingTests : IDisposable
{
    private readonly Func<string, string>? _originalResolver = JauntyConfig.ColumnNameResolver;

    public ReflectionSetterCachingTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose() => JauntyConfig.ColumnNameResolver = _originalResolver;

    [Table("setter_caching_widgets")]
    public class Widget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // ------------------------------------------------------------------
    // The memo: same reader, same shape
    // ------------------------------------------------------------------

    [Fact]
    public void SameReaderAndShape_ReturnsTheSameSetterArray()
    {
        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);

        PropertySetter<Widget>[] first = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);
        PropertySetter<Widget>[] second = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);

        Assert.Same(first, second);
    }

    [Fact]
    public void SameReader_DifferentMappingMode_DoesNotReuseTheMemo()
    {
        // The memo validates the mode, not just the reader: Strict and Projection differ in whether
        // a missing column is tolerated, so returning one mode's setters for the other would change
        // behaviour, not just performance. Uses a complete reader so both modes succeed and the
        // assertion is about the memo rather than about strict-mode validation.
        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);

        PropertySetter<Widget>[] projection = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);
        PropertySetter<Widget>[] strict = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);

        Assert.NotSame(projection, strict);
    }

    [Fact]
    public void EmptyReader_ReturnsEmptyAndIsNotMemoized()
    {
        var reader = new RecycledReader([], []);

        Assert.Empty(MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict));

        // The early return happens before the memo is written, so the same reader gaining columns
        // later must build real setters rather than keep returning the empty array.
        reader.Recycle(["Id", "Name"], [1, "one"]);
        Assert.Equal(2, MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict).Length);
    }

    // ------------------------------------------------------------------
    // The memo must not go stale — recycled readers (AUD-R9-011)
    // ------------------------------------------------------------------

    [Fact]
    public void RecycledReader_WithFewerColumns_RebuildsSetters()
    {
        // Mode is held constant at Projection. An earlier version of this test used Strict then
        // Projection, so the memo missed on the mode check alone and the test passed even with the
        // field-count and column-name re-validation deleted outright - it guarded nothing.
        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
        PropertySetter<Widget>[] wide = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);

        reader.Recycle(["Name"], ["only-name"]);
        PropertySetter<Widget>[] narrow = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);

        Assert.NotSame(wide, narrow);
        Assert.Single(narrow);
    }

    [Fact]
    public void RecycledReader_WithReorderedColumns_RebuildsSetters()
    {
        // Same field count, same names, different order. A memo that checked only FieldCount would
        // hand back setters bound to the previous ordinals and transpose every row.
        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
        MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);

        reader.Recycle(["Name", "Id"], ["two", 2]);
        PropertySetter<Widget>[] reordered = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);

        var widget = new Widget();
        Assert.True(reader.Read());
        foreach (PropertySetter<Widget> setter in reordered)
            setter.Set(widget, reader);

        Assert.Equal(2, widget.Id);
        Assert.Equal("two", widget.Name);
    }

    [Fact]
    public void RecycledReader_WithDifferentColumnNames_RebuildsSetters()
    {
        // Mode held constant - see the note in RecycledReader_WithFewerColumns_RebuildsSetters.
        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
        PropertySetter<Widget>[] first = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);

        reader.Recycle(["Id", "Unrelated"], [1, "x"]);
        PropertySetter<Widget>[] second = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);

        Assert.NotSame(first, second);
    }

    // ------------------------------------------------------------------
    // The resolver is part of the key
    // ------------------------------------------------------------------

    [Fact]
    public void ChangingTheColumnNameResolver_RebuildsSetters()
    {
        JauntyConfig.ColumnNameResolver = null;
        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
        PropertySetter<Widget>[] withoutResolver = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);

        JauntyConfig.ColumnNameResolver = static name => name;
        PropertySetter<Widget>[] withResolver = MetadataCache<Widget>.GetSetters(reader, MappingMode.Projection);

        Assert.NotSame(withoutResolver, withResolver);
    }

    [Fact]
    public void ChangingTheResolverOnAFreshReader_AlsoRebuilds()
    {
        // The reader memo is only the first of two caches. This one goes past it to the shared
        // SettersCache, whose key omitted the resolver entirely - so a *different* reader of the
        // same shape got the stale entry even with the memo working correctly.
        JauntyConfig.ColumnNameResolver = null;
        PropertySetter<Widget>[] withoutResolver = MetadataCache<Widget>.GetSetters(
            new RecycledReader(["Id", "Name"], [1, "one"]), MappingMode.Projection);

        JauntyConfig.ColumnNameResolver = static name => name.ToUpperInvariant();
        PropertySetter<Widget>[] withResolver = MetadataCache<Widget>.GetSetters(
            new RecycledReader(["Id", "Name"], [1, "one"]), MappingMode.Projection);

        Assert.NotSame(withoutResolver, withResolver);
    }

    [Fact]
    public void TheSameResolverInstance_StillHitsTheCache()
    {
        // Reference equality is the comparison, so a stable delegate must not defeat caching.
        Func<string, string> resolver = static name => name;
        JauntyConfig.ColumnNameResolver = resolver;

        var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
        PropertySetter<Widget>[] first = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);
        PropertySetter<Widget>[] second = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);

        Assert.Same(first, second);
    }

    // ------------------------------------------------------------------
    // Memoizing did not change what the setters do
    // ------------------------------------------------------------------

    [Fact]
    public void MemoizedSetters_StillMapEveryRow()
    {
        var reader = new RecycledReader(["Id", "Name"], [3, "three"], rows: 4);

        int mapped = 0;
        while (reader.Read())
        {
            var widget = new Widget();
            foreach (PropertySetter<Widget> setter in MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict))
                setter.Set(widget, reader);

            Assert.Equal(3, widget.Id);
            Assert.Equal("three", widget.Name);
            mapped++;
        }

        Assert.Equal(4, mapped);
    }

    [Fact]
    public void MemoizedSetters_InStrictMode_StillRejectAMissingColumn()
    {
        var reader = new RecycledReader(["Name"], ["only-name"]);

        Assert.Throws<InvalidOperationException>(
            () => MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict));
    }


    // ------------------------------------------------------------------
    // Concurrency
    // ------------------------------------------------------------------

    /// <summary>
    /// The memo write was originally <c>ReaderCache.Remove(reader)</c> followed by
    /// <c>ReaderCache.Add(reader, entry)</c>. <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/>'s
    /// <c>Add</c> throws <see cref="ArgumentException"/> when the key is already present, so that
    /// pair races with itself: two threads both remove, then both add, and the second throws.
    /// Measured at 139 failures in 200 rounds of four threads before the fix.
    ///
    /// <para>
    /// A single reader driven from several threads is already outside ADO.NET's contract, so this
    /// guards the cache's own invariant rather than a supported usage - but the previous code could
    /// not throw here at all, and a caching layer must not introduce a failure mode the thing it
    /// caches did not have.
    /// </para>
    /// </summary>
    [Fact]
    public void ConcurrentGetSetters_OnOneReader_DoesNotThrow()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        for (int round = 0; round < 50; round++)
        {
            var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
            using var gate = new System.Threading.Barrier(4);

            var tasks = new System.Threading.Tasks.Task[4];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    gate.SignalAndWait();
                    try
                    {
                        MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);
        }

        Assert.Empty(exceptions);
    }

    /// <summary>
    /// The same race, but the memo is forced to miss on every call so each thread reaches the write
    /// path rather than settling onto a hit after the first round.
    /// </summary>
    /// <remarks>
    /// An earlier version of this test gave each thread its <em>own</em> reader. Distinct readers are
    /// distinct <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/> keys,
    /// and the race is a same-key <c>Add</c> throwing - so that version could not fail against the
    /// racy pattern no matter how many rounds it ran. All threads now share one reader, and the
    /// alternating mode keeps every call on the write path.
    /// </remarks>
    [Fact]
    public void ConcurrentGetSetters_ForcedMisses_DoNotThrow()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        for (int round = 0; round < 50; round++)
        {
            var reader = new RecycledReader(["Id", "Name"], [1, "one"]);
            using var gate = new System.Threading.Barrier(4);

            var tasks = new System.Threading.Tasks.Task[4];
            for (int i = 0; i < tasks.Length; i++)
            {
                MappingMode mode = i % 2 == 0 ? MappingMode.Strict : MappingMode.Projection;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    gate.SignalAndWait();
                    try
                    {
                        MetadataCache<Widget>.GetSetters(reader, mode);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);
        }

        Assert.Empty(exceptions);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// A reader whose schema can be swapped in place, modelling a provider that recycles one reader
    /// instance across commands on a pooled connection.
    /// </summary>
    private sealed class RecycledReader(string[] names, object[] values, int rows = 1) : IDataReader
    {
        private int _row;

        public void Recycle(string[] newNames, object[] newValues)
        {
            names = newNames;
            values = newValues;
            _row = 0;
        }

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i];
        public bool IsDBNull(int i) => values[i] is null or DBNull;
        public Type GetFieldType(int i) => values[i].GetType();
        public int GetInt32(int i) => Convert.ToInt32(values[i]);
        public string GetString(int i) => (string)values[i];

        public bool Read() => _row++ < rows;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => values[i];
        public object this[string name] => values[GetOrdinal(name)];

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
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public long GetInt64(int i) => 0;
        public int GetValues(object[] target) => 0;
    }
}
