using System.Collections;
using System.Data;

using Jaunty.Attributes;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Write;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R26 (batch 3, low/bug). The three bulk value setters in <c>WriteParameterCache&lt;T&gt;</c>
/// bind an entity's values into the command's existing parameters <em>by index</em>, and used to
/// clamp the loop with <c>Math.Min(getters.Length, pc.Count)</c>.
///
/// <para>
/// The parameters are created by <c>PrepareInsertParameters</c> and friends from
/// <c>cached.Metadata</c>; the getters are compiled in <c>WriteParameterCache&lt;T&gt;</c>'s static
/// constructor from its own <c>ResolveMetadata()</c> call. Two independent resolutions, coupled by
/// position and by convention only. The clamp meant any disagreement between them was absorbed in
/// silence: the surplus parameters kept whatever they last held, the statement executed, and on the
/// bulk path what they last held is the <em>previous row's</em> values - written under the current
/// row's key, for every row after the first. <c>IDataParameter.ParameterName</c> was available the
/// whole time and never consulted.
/// </para>
///
/// <para>
/// A silent wrong-data write is the worst outcome available on a write path, and it was the
/// default one. These tests pin that a misaligned collection now fails with an error that names
/// the mismatch, and that the aligned case - the only one a correctly-configured Jaunty produces -
/// still binds every row's own values.
/// </para>
///
/// <para>
/// Each misalignment test uses its own entity type on purpose: the name check is memoised per
/// <c>WriteParameterCache&lt;T&gt;</c> (it compares two static shapes, so once is enough, and a
/// per-row string comparison over every column is exactly the cost the bulk path exists to avoid).
/// Sharing an entity type across tests would let a passing bind mark the type verified and make a
/// later misalignment test silently vacuous.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class BulkValueSetterAlignmentTests
{
    public BulkValueSetterAlignmentTests() => JauntyReflectionExtensions.UseReflectionMapping();

    [Table("alignment_short")]
    public class ShortCollectionWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
    }

    [Table("alignment_misnamed")]
    public class MisnamedWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
    }

    [Table("alignment_surplus")]
    public class SurplusWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
    }

    [Table("alignment_delete")]
    public class DeleteWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("alignment_roundtrip")]
    public class RoundTripWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// A parameter collection Jaunty did not build, standing in for the one a mismatched binder
    /// would have produced. Only the members the value setters touch need real behaviour.
    /// </summary>
    private sealed class FakeParameterCollection : IDataParameterCollection
    {
        private readonly List<object> _items = new();

        public object this[int index] { get => _items[index]; set => _items[index] = value; }
        public object this[string parameterName] { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public int Count => _items.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => _items;

        public int Add(object? value) { _items.Add(value!); return _items.Count - 1; }
        public void Clear() => _items.Clear();
        public bool Contains(object? value) => _items.Contains(value!);
        public bool Contains(string parameterName) => IndexOf(parameterName) >= 0;
        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public IEnumerator GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(object? value) => _items.IndexOf(value!);
        public int IndexOf(string parameterName) =>
            _items.FindIndex(i => i is IDataParameter p && p.ParameterName == parameterName);
        public void Insert(int index, object? value) => _items.Insert(index, value!);
        public void Remove(object? value) => _items.Remove(value!);
        public void RemoveAt(int index) => _items.RemoveAt(index);
        public void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

        public static FakeParameterCollection Named(params string[] names)
        {
            var collection = new FakeParameterCollection();
            foreach (string name in names)
                collection.Add(new SqliteParameter { ParameterName = name });
            return collection;
        }
    }

    // ------------------------------------------------------------------
    // Misalignment is reported, not absorbed
    // ------------------------------------------------------------------

    /// <summary>
    /// Fewer parameters than columns. Under the clamp the trailing columns were simply not written,
    /// so on row 2 onward they held row 1's values and the INSERT succeeded with wrong data.
    /// </summary>
    [Fact]
    public void FewerParametersThanColumns_Throws()
    {
        Action<IDataParameterCollection, ShortCollectionWidget>? setter =
            WriteParameterCache<ShortCollectionWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        // The real Prepare step would have created one parameter per insertable column.
        FakeParameterCollection collection = FakeParameterCollection.Named("@Id", "@Name");

        var exception = Assert.Throws<InvalidOperationException>(
            () => setter!(collection, new ShortCollectionWidget { Id = 1, Name = "a", Quantity = 7 }));

        Assert.Contains("ShortCollectionWidget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("2 parameter", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// More parameters than columns: the surplus parameters were left holding the previous row's
    /// values, which is the same corruption from the other direction.
    /// </summary>
    [Fact]
    public void MoreParametersThanColumns_Throws()
    {
        Action<IDataParameterCollection, SurplusWidget>? setter =
            WriteParameterCache<SurplusWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        FakeParameterCollection collection =
            FakeParameterCollection.Named("@Id", "@Name", "@Quantity", "@Extra", "@AlsoExtra");

        var exception = Assert.Throws<InvalidOperationException>(
            () => setter!(collection, new SurplusWidget { Id = 1, Name = "a", Quantity = 7 }));

        Assert.Contains("SurplusWidget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("5 parameter", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The count agrees but the order does not - the case the count check alone cannot see, and the
    /// one that writes every column's value into a different column.
    /// </summary>
    [Fact]
    public void RightCountButWrongOrder_Throws()
    {
        Action<IDataParameterCollection, MisnamedWidget>? setter =
            WriteParameterCache<MisnamedWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        FakeParameterCollection collection = FakeParameterCollection.Named("@Quantity", "@Id", "@Name");

        var exception = Assert.Throws<InvalidOperationException>(
            () => setter!(collection, new MisnamedWidget { Id = 1, Name = "a", Quantity = 7 }));

        Assert.Contains("MisnamedWidget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Quantity", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The delete setter takes the same shape, so it gets the same guard.</summary>
    [Fact]
    public void TheDeleteSetterIsGuardedToo()
    {
        Action<IDataParameterCollection, DeleteWidget>? setter =
            WriteParameterCache<DeleteWidget>.DeleteValueSetter;
        Assert.NotNull(setter);

        FakeParameterCollection collection = FakeParameterCollection.Named("@Id", "@Name", "@Surplus");

        var exception = Assert.Throws<InvalidOperationException>(
            () => setter!(collection, new DeleteWidget { Id = 1, Name = "a" }));

        Assert.Contains("delete", exception.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The aligned case - the only one a correct configuration produces
    // ------------------------------------------------------------------

    /// <summary>
    /// The parameter names a provider reports back may or may not carry the dialect prefix, so the
    /// comparison drops it on both sides. A collection named without '@' must still bind.
    /// </summary>
    [Fact]
    public void ParameterNamesWithoutThePrefix_StillBind()
    {
        Action<IDataParameterCollection, RoundTripWidget>? setter =
            WriteParameterCache<RoundTripWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        FakeParameterCollection collection = FakeParameterCollection.Named("Id", "Name", "Quantity");

        setter!(collection, new RoundTripWidget { Id = 3, Name = "gizmo", Quantity = 9 });

        Assert.Equal(3, ((IDataParameter)collection[0]).Value);
        Assert.Equal("gizmo", ((IDataParameter)collection[1]).Value);
        Assert.Equal(9, ((IDataParameter)collection[2]).Value);
    }

    /// <summary>
    /// End to end on the path that actually uses the setter. SQLite takes the prepared-loop path
    /// (PROD-120), which is the path that reuses one command's parameter collection across every
    /// row - so if a row's values ever leaked into the next, this is where it would show.
    /// </summary>
    [Fact]
    public void BulkInsertBindsEachRowsOwnValues()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();

        using (SqliteCommand create = connection.CreateCommand())
        {
            create.CommandText =
                "CREATE TABLE alignment_roundtrip (Id INTEGER PRIMARY KEY, Name TEXT, Quantity INTEGER);";
            create.ExecuteNonQuery();
        }

        var widgets = new List<RoundTripWidget>();
        for (int i = 1; i <= 25; i++)
            widgets.Add(new RoundTripWidget { Id = i, Name = $"widget-{i}", Quantity = i * 10 });

        int inserted = connection.BulkInsert(widgets);
        Assert.Equal(25, inserted);

        using SqliteCommand read = connection.CreateCommand();
        read.CommandText = "SELECT Id, Name, Quantity FROM alignment_roundtrip ORDER BY Id;";
        using IDataReader reader = read.ExecuteReader();

        int row = 0;
        while (reader.Read())
        {
            row++;
            Assert.Equal(row, reader.GetInt32(0));
            Assert.Equal($"widget-{row}", reader.GetString(1));
            Assert.Equal(row * 10, reader.GetInt32(2));
        }

        Assert.Equal(25, row);
    }
}
