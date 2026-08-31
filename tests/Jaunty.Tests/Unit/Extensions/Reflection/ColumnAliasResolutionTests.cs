using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Entity;

using Xunit;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// AUD-R26, the <c>ColumnToIndex</c> finding - reopened at the round-26 close-out, because
/// reconciliation showed three commits had touched <c>MetadataCache.cs</c> without changing the
/// line the finding named.
///
/// <para>
/// <b>What the audit claimed, and what measuring showed.</b> The finding described two properties
/// mapped to one column collapsing silently to whichever came last. That is <em>not</em> what
/// happens: <see cref="EntityMetadata"/>'s <c>ParameterMap</c> is keyed on column name and already
/// rejected it. Only the diagnostic was missing - the caller got .NET's own
/// <c>"An item with the same key has already been added. Key: status"</c>, wrapped in a
/// <see cref="TypeInitializationException"/>, naming neither the entity nor either property.
/// </para>
///
/// <para>
/// <b>What measuring found instead, which the finding did not describe.</b> The static constructor
/// registered a property-name alias in the same pass as the real column name, so a later property's
/// <em>alias</em> could overwrite an earlier property's <em>actual column</em>. That is silent
/// corruption rather than a silent drop: a property ends up holding a value that belongs to a
/// different column. <see cref="AliasShadow"/> below is the measured case.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class ColumnAliasResolutionTests
{
    public ColumnAliasResolutionTests() => JauntyReflectionExtensions.UseReflectionMapping();

    /// <summary>
    /// Property <c>A</c> maps to column "B"; property <c>B</c> maps to column "C". The name "B" is
    /// therefore both a real column (A's) and a property name (B's) - the collision the second pass
    /// exists to resolve.
    /// </summary>
    [Table("alias_shadow")]
    public class AliasShadow
    {
        [Key]
        public int Id { get; set; }

        [Column("B")]
        public string? A { get; set; }

        [Column("C")]
        public string? B { get; set; }
    }

    /// <summary>
    /// A real column name always wins over a property-name alias. Before the fix the setters were
    /// (Id -> Id, B -> B): property A was never populated, property B held column B's value rather
    /// than its own column C's, and column C was not mapped at all.
    /// </summary>
    [Fact]
    public void ARealColumnName_IsNotOverwrittenByALaterPropertyNameAlias()
    {
        var reader = new StubReader(["Id", "B", "C"], [1, "b-value", "c-value"]);

        PropertySetter<AliasShadow>[] setters = MetadataCache<AliasShadow>.GetSetters(reader, MappingMode.Projection);

        Assert.Equal(3, setters.Length);

        reader.Read();
        var entity = new AliasShadow();
        foreach (PropertySetter<AliasShadow> setter in setters)
            setter.Context.Setter(entity, reader, setter.Ordinal);

        Assert.Equal("b-value", entity.A);
        Assert.Equal("c-value", entity.B);
    }

    /// <summary>
    /// The corruption stated on its own terms: no property may end up holding a value that belongs
    /// to a different column. Kept separate from the assertion above so a regression says which of
    /// the two things broke.
    /// </summary>
    [Fact]
    public void NoProperty_ReceivesAnotherColumnsValue()
    {
        var reader = new StubReader(["Id", "B", "C"], [1, "b-value", "c-value"]);

        PropertySetter<AliasShadow>[] setters = MetadataCache<AliasShadow>.GetSetters(reader, MappingMode.Projection);

        reader.Read();
        var entity = new AliasShadow();
        foreach (PropertySetter<AliasShadow> setter in setters)
            setter.Context.Setter(entity, reader, setter.Ordinal);

        Assert.NotEqual("b-value", entity.B);
    }

    [Table("plain_alias")]
    public class PlainAlias
    {
        [Key]
        public int Id { get; set; }

        [Column("display_name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// The alias is still a fallback where nothing contends for it - matching a reader that returns
    /// the property name rather than the mapped column name is the behaviour it was added for, and
    /// narrowing the overwrite must not remove it.
    /// </summary>
    [Fact]
    public void APropertyNameAlias_StillMatches_WhenNoColumnClaimsIt()
    {
        var reader = new StubReader(["Id", "Name"], [1, "by-property-name"]);

        PropertySetter<PlainAlias>[] setters = MetadataCache<PlainAlias>.GetSetters(reader, MappingMode.Projection);

        reader.Read();
        var entity = new PlainAlias();
        foreach (PropertySetter<PlainAlias> setter in setters)
            setter.Context.Setter(entity, reader, setter.Ordinal);

        Assert.Equal("by-property-name", entity.Name);
    }

    /// <summary>And the mapped column name itself still matches, which is the primary path.</summary>
    [Fact]
    public void TheMappedColumnName_StillMatches()
    {
        var reader = new StubReader(["Id", "display_name"], [1, "by-column-name"]);

        PropertySetter<PlainAlias>[] setters = MetadataCache<PlainAlias>.GetSetters(reader, MappingMode.Projection);

        reader.Read();
        var entity = new PlainAlias();
        foreach (PropertySetter<PlainAlias> setter in setters)
            setter.Context.Setter(entity, reader, setter.Ordinal);

        Assert.Equal("by-column-name", entity.Name);
    }

    // ------------------------------------------------------------------
    // The duplicate-column diagnostic
    // ------------------------------------------------------------------

    /// <summary>
    /// The message must name the entity and both properties. Built directly rather than through an
    /// entity type, because a duplicate is rejected in the static constructor - reaching it through
    /// <c>MetadataCache&lt;T&gt;</c> buries it under a <see cref="TypeInitializationException"/>,
    /// which is itself part of what made the old message so unhelpful.
    /// </summary>
    [Fact]
    public void TwoPropertiesOnOneColumn_AreRejected_WithAMessageNamingBoth()
    {
        ColumnMetadata[] columns = [Column(nameof(AliasShadow.A), "status"), Column(nameof(AliasShadow.B), "status")];

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new EntityMetadata("orders", null, columns));

        Assert.Contains("orders", ex.Message, StringComparison.Ordinal);
        Assert.Contains("status", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AliasShadow.A), ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AliasShadow.B), ex.Message, StringComparison.Ordinal);

        // The old message was .NET's own, from whichever dictionary happened to build ParameterMap.
        Assert.DoesNotContain("An item with the same key", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Column names are matched case-insensitively everywhere else, so the rejection has to be too -
    /// otherwise <c>Status</c> and <c>status</c> would build a metadata object whose own
    /// <c>ParameterMap</c> could not hold both.
    /// </summary>
    [Fact]
    public void TheDuplicateCheck_IsCaseInsensitive()
    {
        ColumnMetadata[] columns = [Column(nameof(AliasShadow.A), "Status"), Column(nameof(AliasShadow.B), "status")];

        Assert.Throws<ArgumentException>(() => new EntityMetadata("orders", null, columns));
    }

    /// <summary>Distinct column names are unaffected - the guard must not reject ordinary entities.</summary>
    [Fact]
    public void DistinctColumnNames_AreAccepted()
    {
        ColumnMetadata[] columns = [Column(nameof(AliasShadow.A), "status"), Column(nameof(AliasShadow.B), "state")];

        var metadata = new EntityMetadata("orders", null, columns);

        Assert.Equal(2, metadata.ParameterMap.Count);
    }

    private static ColumnMetadata Column(string propertyName, string columnName)
        => new(typeof(AliasShadow).GetProperty(propertyName)!, columnName, isPrimaryKey: false, databaseGeneratedOption: null);

    // ------------------------------------------------------------------

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
        public int GetValues(object[] target) => 0;
    }
}
