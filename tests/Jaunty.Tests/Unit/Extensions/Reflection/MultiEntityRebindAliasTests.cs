using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// AUD-R35-022. <c>GetSettersExcluding</c> rebinds a setter whose first-bound ordinal an earlier
/// type already claimed. The search matched on <c>Context.ColumnName</c> alone, while
/// <c>BuildSetters</c> binds on three names - the metadata column name, the property-name fallback
/// alias, and the <c>ColumnNameResolver</c> index - so a setter bound through either of the latter
/// two was searched for under a name no reader column carries, found nothing, and was silently
/// dropped.
/// </summary>
[Collection("Type Handler Operations")]
public class MultiEntityRebindAliasTests : IDisposable
{
    public void Dispose()
    {
        JauntyConfig.ColumnNameResolver = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void PropertyNameAliasIsRebound_NotDropped()
    {
        // Aliased.Id maps to column "ProductID"; the property-name fallback also binds a reader
        // column literally named "Id". Both are present, so Aliased first-binds to ordinal 0 -
        // and ordinal 0 is claimed by an earlier type.
        var reader = new StubReader(["ProductID", "Id"], [1, 2]);
        var claimed = new HashSet<int> { 0 };

        (PropertySetter<Aliased>[] setters, int[] ordinals) =
            MultiEntityMapperCore.GetSettersExcluding<Aliased>(reader, claimed);

        Assert.Single(setters);
        Assert.Equal(1, setters[0].Ordinal);
        Assert.Equal([1], ordinals);
    }

    [Fact]
    public void PropertyNameAliasRebind_ProducesTheRightValue()
    {
        var reader = new StubReader(["ProductID", "Id"], [1, 2]);
        reader.Read();

        (PropertySetter<Aliased>[] setters, _) =
            MultiEntityMapperCore.GetSettersExcluding<Aliased>(reader, new HashSet<int> { 0 });

        var entity = new Aliased();
        for (int i = 0; i < setters.Length; i++)
            setters[i].Set(entity, reader);

        Assert.Equal(2, entity.Id);
    }

    [Fact]
    public void ResolverMappedColumnIsRebound_NotDropped()
    {
        // A control rather than a discriminator: MetadataBuilder already resolves the name at
        // snapshot-build time (MetadataBuilder.cs:89), so Context.ColumnName is "order_id" here
        // and the old name-string search happened to find it too. Kept because the resolver index
        // in BuildSetters is a second, independent route to the same binding, and a change to
        // either side would show up here.
        JauntyConfig.ColumnNameResolver = static name => name == nameof(Plain.OrderId) ? "order_id" : name;

        var reader = new StubReader(["order_id", "order_id"], [7, 9]);

        (PropertySetter<Plain>[] setters, _) =
            MultiEntityMapperCore.GetSettersExcluding<Plain>(reader, new HashSet<int> { 0 });

        Assert.Single(setters);
        Assert.Equal(1, setters[0].Ordinal);
    }

    [Fact]
    public void TheOrdinaryColumnNameRebindStillWorks()
    {
        var reader = new StubReader(["ProductID", "ProductID"], [1, 2]);

        (PropertySetter<Aliased>[] setters, _) =
            MultiEntityMapperCore.GetSettersExcluding<Aliased>(reader, new HashSet<int> { 0 });

        Assert.Single(setters);
        Assert.Equal(1, setters[0].Ordinal);
    }

    [Fact]
    public void WithNothingClaimed_TheFirstBindingIsKept()
    {
        var reader = new StubReader(["ProductID", "Id"], [1, 2]);

        (PropertySetter<Aliased>[] setters, _) =
            MultiEntityMapperCore.GetSettersExcluding<Aliased>(reader, new HashSet<int>());

        Assert.Single(setters);
        Assert.Equal(0, setters[0].Ordinal);
    }

    [Fact]
    public void WithNoUnclaimedMatchLeft_ThePropertyIsStillDropped()
    {
        var reader = new StubReader(["ProductID", "Unrelated"], [1, 2]);

        (PropertySetter<Aliased>[] setters, _) =
            MultiEntityMapperCore.GetSettersExcluding<Aliased>(reader, new HashSet<int> { 0 });

        Assert.Empty(setters);
    }

    [Fact]
    public void ARebindNeverStealsADifferentPropertysColumn()
    {
        // "Id" is Aliased.Id's fallback alias, not Name's; a rebind of Name must not take it.
        var reader = new StubReader(["Name", "Id"], ["a", 2]);

        (PropertySetter<TwoProps>[] setters, _) =
            MultiEntityMapperCore.GetSettersExcluding<TwoProps>(reader, new HashSet<int> { 0 });

        Assert.Single(setters);
        Assert.Equal("Id", reader.GetName(setters[0].Ordinal));
        Assert.Equal(nameof(TwoProps.Id), setters[0].Context.PropertyName);
    }

    private sealed class Aliased
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("ProductID")]
        public int Id { get; set; }
    }

    private sealed class TwoProps
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class Plain
    {
        public int OrderId { get; set; }
    }

    private sealed class StubReader(string[] names, object[] values) : IDataReader
    {
        private int _row;

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i];
        public bool IsDBNull(int i) => values[i] is null or DBNull;
        public Type GetFieldType(int i) => values[i].GetType();
        public int GetInt32(int i) => Convert.ToInt32(values[i]);
        public string GetString(int i) => (string)values[i];

        public bool Read() => _row++ < 1;
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
        public int GetValues(object[] values2) => 0;
    }
}
