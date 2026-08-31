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
/// The name check is memoised per <em>parameter collection</em>, not once per
/// <c>WriteParameterCache&lt;T&gt;</c>. The first version of this fix used a plain bool - "both sides
/// of the comparison are static per T, so once is enough" - and that reasoning was wrong; see
/// <see cref="AnAlreadyVerifiedTypeIsRecheckedOnANewCollection"/> for the case it let through. Each
/// misalignment test still owns its entity type, which is now belt and braces rather than
/// load-bearing.
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

    [Table("alignment_recheck")]
    public class RecheckWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
    }

    [Table("alignment_sigil")]
    public class SigilWidget
    {
        [Key]
        [Column("$type")]
        public int Type { get; set; }

        public string? Name { get; set; }
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
    /// The case that killed the first version of this fix, which memoised the name check with a
    /// plain bool on the reasoning that "both sides of the comparison are static per T".
    ///
    /// <para>
    /// They are not equally static. The getters freeze when <c>WriteParameterCache&lt;T&gt;</c>'s
    /// static constructor runs. The parameters come from <c>CrudSqlCache.GetSql&lt;T&gt;(connection)</c>,
    /// which is keyed on <c>(entityType, connectionType)</c> - so the first use of a given entity on
    /// a <em>new connection type</em> re-resolves metadata. If
    /// <c>JauntyConfig.ReflectionTableMetadataResolver</c> is replaced in between (public API;
    /// <c>JauntyConfig.Reset()</c> plus re-registration does it), the second resolution can order
    /// columns differently while the getters keep the first order.
    /// </para>
    ///
    /// <para>
    /// That is an equal-count reorder - the worst misbind available, and the one case the count
    /// check cannot see. With the bool, the second collection was waved through and every row wrote
    /// each column's value into a different column. The memo is now keyed on the collection's
    /// identity, so a collection this setter has not seen before is always checked.
    /// </para>
    /// </summary>
    [Fact]
    public void AnAlreadyVerifiedTypeIsRecheckedOnANewCollection()
    {
        Action<IDataParameterCollection, RecheckWidget>? setter =
            WriteParameterCache<RecheckWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        // First collection is correct and binds, marking the setter "verified" under the old bool.
        FakeParameterCollection aligned = FakeParameterCollection.Named("@Id", "@Name", "@Quantity");
        setter!(aligned, new RecheckWidget { Id = 1, Name = "a", Quantity = 7 });
        Assert.Equal(1, ((IDataParameter)aligned[0]).Value);

        // A different collection object, same count, different order. The count check passes.
        FakeParameterCollection reordered = FakeParameterCollection.Named("@Quantity", "@Id", "@Name");

        var exception = Assert.Throws<InvalidOperationException>(
            () => setter!(reordered, new RecheckWidget { Id = 2, Name = "b", Quantity = 9 }));

        Assert.Contains("RecheckWidget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Quantity", exception.Message, StringComparison.Ordinal);

        // And nothing was written into the misaligned collection before it was rejected.
        Assert.All(new[] { reordered[0], reordered[1], reordered[2] },
            p => Assert.Null(((IDataParameter)p).Value));
    }

    /// <summary>
    /// Re-binding the same collection must not re-run the name check - that is the whole point of
    /// memoising it. Observable indirectly: a collection whose names are correct binds any number of
    /// times, and the second bind sees each row's own values rather than the first row's.
    /// </summary>
    [Fact]
    public void RebindingTheSameCollectionKeepsWorking()
    {
        Action<IDataParameterCollection, RecheckWidget>? setter =
            WriteParameterCache<RecheckWidget>.InsertValueSetter;

        FakeParameterCollection collection = FakeParameterCollection.Named("@Id", "@Name", "@Quantity");

        for (int i = 1; i <= 5; i++)
        {
            setter!(collection, new RecheckWidget { Id = i, Name = $"n{i}", Quantity = i * 3 });

            Assert.Equal(i, ((IDataParameter)collection[0]).Value);
            Assert.Equal($"n{i}", ((IDataParameter)collection[1]).Value);
            Assert.Equal(i * 3, ((IDataParameter)collection[2]).Value);
        }
    }

    /// <summary>
    /// A column whose own name starts with a dialect sigil. The prefix is stripped from the
    /// parameter's name only - stripping it from the column name too turned <c>"$type"</c> into
    /// <c>"type"</c> and threw on a correct configuration.
    /// </summary>
    [Fact]
    public void AColumnNameThatStartsWithASigil_StillBinds()
    {
        Action<IDataParameterCollection, SigilWidget>? setter =
            WriteParameterCache<SigilWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        // Exactly what PrepareInsertParameters would have produced: "@" + ColumnName.
        FakeParameterCollection collection = FakeParameterCollection.Named("@$type", "@Name");

        setter!(collection, new SigilWidget { Type = 4, Name = "ok" });

        Assert.Equal(4, ((IDataParameter)collection[0]).Value);
        Assert.Equal("ok", ((IDataParameter)collection[1]).Value);
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
