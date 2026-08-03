using System.Data;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R35-069. NULL into a non-nullable property behaved three different ways, decided by the
/// property's type and by whether the generator package was referenced. The reflection twin is
/// uniform: <c>PropertySetter&lt;T&gt;.Set</c> throws
/// <c>InvalidOperationException("Cannot assign NULL to non-nullable property 'X'")</c> for every
/// non-nullable value type. The generated mapper instead emitted (a) an unguarded typed getter for
/// <c>int</c>/<c>long</c>/<c>bool</c>/<c>decimal</c>/<c>double</c>/<c>float</c>/<c>short</c>/
/// <c>byte</c>/<c>DateTime</c>/<c>Guid</c>, so the caller got a provider-specific
/// <c>InvalidCastException</c>/<c>SqlNullValueException</c> rather than the named error, and (b) for
/// every catch-all type - enum, <c>DateOnly</c>, <c>TimeOnly</c>, <c>char</c>, <c>uint</c>,
/// <c>ulong</c>, <c>sbyte</c>, <c>ushort</c> - a <em>silent skip</em>, because
/// <c>GetReaderTypeInfo</c>'s <c>_</c> arm hardcodes <c>NeedsNullCheck: true</c> regardless of
/// nullability. So a non-nullable enum column that went NULL produced a loud named error under
/// reflection and a quietly wrong entity once the generator package was added.
/// <para>
/// Distinct from AUD-R33-009, which settled skip-versus-reset for <em>nullable</em> properties; the
/// last two tests here pin that unchanged.
/// </para>
/// </summary>
public sealed class GeneratedNonNullableNullTests
{
    private static readonly string[] Columns =
        ["id", "day", "moment", "duration", "occurred_at", "ref_id", "grade", "counter"];

    private static readonly object[] Defaults =
    [
        1,
        new DateOnly(2024, 1, 15),
        new TimeOnly(14, 30, 0),
        new TimeSpan(1, 2, 3),
        new DateTimeOffset(2024, 1, 15, 14, 30, 0, TimeSpan.Zero),
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        GenFallbackGrade.High,
        7u
    ];

    private static NullableReader ReaderWithNull(string column)
    {
        var values = (object?[])Defaults.Clone();
        values[Array.IndexOf(Columns, column)] = DBNull.Value;

        var reader = new NullableReader(Columns, values);
        Assert.True(reader.Read());
        return reader;
    }

    // ------------------------------------------------------------------
    // Every non-nullable property, on the ReadEntity path.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("id", "Id")]                 // typed getter, was an unguarded GetInt32
    [InlineData("day", "Day")]               // catch-all, was silently skipped
    [InlineData("moment", "Moment")]         // catch-all
    [InlineData("duration", "Duration")]     // catch-all
    [InlineData("occurred_at", "OccurredAt")]// catch-all
    [InlineData("ref_id", "RefId")]          // catch-all
    [InlineData("grade", "Grade")]           // enum - the headline case
    [InlineData("counter", "Counter")]       // catch-all
    public void ANullColumn_ForANonNullableProperty_ThrowsTheSameNamedErrorAsReflection(
        string column, string propertyName)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => GenFallbackEntity.ReadEntity(ReaderWithNull(column)));

        Assert.Equal($"Cannot assign NULL to non-nullable property '{propertyName}'.", ex.Message);
    }

    // ------------------------------------------------------------------
    // The same on the CreateRowMapper path, which carries its own copy of the emission.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("grade", "Grade")]
    [InlineData("id", "Id")]
    [InlineData("day", "Day")]
    public void ANullColumn_ThroughTheRowMapper_ThrowsTheSameNamedError(string column, string propertyName)
    {
        NullableReader reader = ReaderWithNull(column);
        Func<IDataReader, GenFallbackEntity> mapper = GenFallbackEntity.CreateRowMapper(reader);

        var ex = Assert.Throws<InvalidOperationException>(() => mapper(reader));

        Assert.Equal($"Cannot assign NULL to non-nullable property '{propertyName}'.", ex.Message);
    }

    // ------------------------------------------------------------------
    // Control: a fully populated row still maps.
    // ------------------------------------------------------------------

    [Fact]
    public void ARowWithNoNulls_StillMaps()
    {
        var reader = new NullableReader(Columns, (object?[])Defaults.Clone());
        Assert.True(reader.Read());

        GenFallbackEntity entity = GenFallbackEntity.ReadEntity(reader);

        Assert.Equal(1, entity.Id);
        Assert.Equal(GenFallbackGrade.High, entity.Grade);
        Assert.Equal(7u, entity.Counter);
    }

    // ------------------------------------------------------------------
    // AUD-R33-009 unchanged: a nullable or reference property still skips.
    // ------------------------------------------------------------------

    [Fact]
    public void ANullColumn_ForAReferenceProperty_LeavesTheInitializerInPlace()
    {
        var reader = new NullableReader(["Id", "Name", "Note"], [1, DBNull.Value, DBNull.Value]);
        Assert.True(reader.Read());

        GenInitializedEntity entity = GenInitializedEntity.ReadEntity(reader);

        Assert.Equal("unset", entity.Name);
        Assert.Null(entity.Note);
    }

    [Fact]
    public void ANullColumn_ForAReferenceProperty_ThroughTheRowMapper_AlsoSkips()
    {
        var reader = new NullableReader(["Id", "Name", "Note"], [1, DBNull.Value, DBNull.Value]);
        Assert.True(reader.Read());

        GenInitializedEntity entity = GenInitializedEntity.CreateRowMapper(reader)(reader);

        Assert.Equal("unset", entity.Name);
    }

    private sealed class NullableReader(string[] names, object?[] values) : IDataReader
    {
        private int _row;

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i]!;
        public bool IsDBNull(int i) => values[i] is null or DBNull;
        public Type GetFieldType(int i) => values[i]?.GetType() ?? typeof(object);
        public int GetInt32(int i) => Convert.ToInt32(values[i]);
        public string GetString(int i) => (string)values[i]!;

        public bool Read() => _row++ < 1;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => values[i]!;
        public object this[string name] => values[GetOrdinal(name)]!;

        public bool GetBoolean(int i) => Convert.ToBoolean(values[i]);
        public byte GetByte(int i) => Convert.ToByte(values[i]);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => Convert.ToChar(values[i]);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        public DateTime GetDateTime(int i) => Convert.ToDateTime(values[i]);
        public decimal GetDecimal(int i) => Convert.ToDecimal(values[i]);
        public double GetDouble(int i) => Convert.ToDouble(values[i]);
        public float GetFloat(int i) => Convert.ToSingle(values[i]);
        public Guid GetGuid(int i) => (Guid)values[i]!;
        public short GetInt16(int i) => Convert.ToInt16(values[i]);
        public long GetInt64(int i) => Convert.ToInt64(values[i]);
        public int GetValues(object[] values) => 0;
    }
}
