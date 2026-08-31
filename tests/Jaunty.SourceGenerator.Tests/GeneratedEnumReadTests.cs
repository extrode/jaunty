using System.Data;
using System.Data.Common;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R25 (B8-6): <c>GetReaderTypeInfo</c>'s catch-all arm is reached by every property type not
/// named explicitly - enums above all - and the code it generated performed a raw <b>unboxing
/// cast</b>.
///
/// <para>
/// On the plain-<see cref="IDataReader"/> path the emitted expression was
/// <c>(global::MyApp.Severity)reader.GetValue(ord[i])</c>; on the <see cref="DbDataReader"/> fast
/// path it was <c>dbReader.GetFieldValue&lt;Severity&gt;(ord[i])</c>, whose base
/// <see cref="DbDataReader.GetFieldValue{T}"/> implementation is the same
/// <c>(T)GetValue(ordinal)</c> cast. Either way an enum was read by unboxing whatever the provider
/// boxed.
/// </para>
///
/// <para>
/// <b>The finding's stated mechanism was wrong and is corrected here.</b> It claimed that unboxing a
/// boxed <see cref="int"/> to an enum type "throws InvalidCastException unconditionally in .NET -
/// this is a language-level guarantee". It does not: the CLR explicitly permits
/// <c>unbox.any</c> between an enum and its own underlying type, so a boxed <see cref="int"/> reads
/// into an <see cref="int"/>-backed enum perfectly well. That case is pinned below and passes with
/// or without the fix.
/// </para>
///
/// <para>
/// What actually threw - verified by reverting the fix - is narrower but hits harder in practice:
/// </para>
/// <list type="bullet">
/// <item><description>
/// a boxed <see cref="long"/>, because the underlying types must <em>match</em>, not merely be
/// convertible. SQLite and MySQL return <see cref="long"/> for integer columns regardless of the
/// declared width, so this is the ordinary case on two of the four supported providers, not an edge
/// one.
/// </description></item>
/// <item><description>
/// a boxed <see cref="string"/>, for any schema storing enums by name.
/// </description></item>
/// </list>
///
/// <para>
/// It went unnoticed because the single enum test entity was read through
/// <c>Microsoft.Data.Sqlite</c>, which specialises <c>GetFieldValue&lt;T&gt;</c> and converts
/// properly. The gap was visible in the coverage: <c>GeneratedGetValueFallbackTests</c> exercised
/// <see cref="GenEventLog"/> only via <see cref="DbDataReader"/> on SQLite, and the plain
/// <see cref="IDataReader"/> fallback was exercised only with <c>GenProduct</c>, which has no
/// fallback-typed property - so the enum × <see cref="IDataReader"/> combination was never
/// executed. These tests execute it, against a reader that is deliberately <em>not</em> a
/// <see cref="DbDataReader"/>.
/// </para>
///
/// <para>
/// The generated <c>ReadFallback&lt;T&gt;</c> now converts rather than casts, which also brings the
/// generated path level with the reflection path's long-standing enum handling
/// (<c>MetadataCache.CreateStringEnumSetter</c>/<c>CreateConvertingSetter</c>): both numeric and
/// string storage are accepted.
/// </para>
/// </summary>
public sealed class GeneratedEnumReadTests
{
    [Fact]
    public void Enum_FromABoxedInt32_WorkedBeforeToo()
    {
        // Kept deliberately, and labelled for what it is: this does NOT reproduce the finding.
        // The CLR permits unbox.any between an enum and its own underlying type, so the old raw
        // cast handled this case correctly - it passes with the fix reverted. It is here so the
        // conversion path is pinned against regressing the case that already worked.
        GenEventLog entity = ReadOne(["event_id", "severity", "duration", "occurred_at"],
            [1, 2, TimeSpan.FromMinutes(3), DateTimeOffset.UnixEpoch]);

        Assert.Equal(GenEventSeverity.Error, entity.Severity);
    }

    [Fact]
    public void Enum_FromABoxedInt64_AsSqliteAndMySqlReturnIntegers()
    {
        // This is the case that actually threw: "Unable to cast object of type 'System.Int64' to
        // type 'GenEventSeverity'". Unboxing requires the underlying types to *match*, not merely
        // to be convertible - and SQLite and MySQL hand back Int64 for integer columns regardless
        // of the declared width, so this is the ordinary case on two of the four providers.
        GenEventLog entity = ReadOne(["event_id", "severity", "duration", "occurred_at"],
            [1, 1L, TimeSpan.Zero, DateTimeOffset.UnixEpoch]);

        Assert.Equal(GenEventSeverity.Warning, entity.Severity);
    }

    [Fact]
    public void Enum_FromAString_AsAProviderStoringItByName()
    {
        GenEventLog entity = ReadOne(["event_id", "severity", "duration", "occurred_at"],
            [1, "Warning", TimeSpan.Zero, DateTimeOffset.UnixEpoch]);

        Assert.Equal(GenEventSeverity.Warning, entity.Severity);
    }

    [Fact]
    public void Enum_FromAStringInTheWrongCase()
    {
        // Parsed case-insensitively, matching MetadataCache's string-enum setter on the
        // reflection path rather than diverging from it.
        GenEventLog entity = ReadOne(["event_id", "severity", "duration", "occurred_at"],
            [1, "eRRoR", TimeSpan.Zero, DateTimeOffset.UnixEpoch]);

        Assert.Equal(GenEventSeverity.Error, entity.Severity);
    }

    [Fact]
    public void Enum_FromAnUndefinedNumericValue_IsPreservedRatherThanRejected()
    {
        // Enum.ToObject does not validate against declared members, and neither did the cast it
        // replaces - so this also passes either way. Flags enums and forward-compatible schemas
        // depend on it, which makes it worth pinning as behaviour the fix must not tighten.
        GenEventLog entity = ReadOne(["event_id", "severity", "duration", "occurred_at"],
            [1, 99, TimeSpan.Zero, DateTimeOffset.UnixEpoch]);

        Assert.Equal(99, (int)entity.Severity);
    }

    [Fact]
    public void Enum_FromAnUnparseableString_StillThrows()
    {
        // Converting instead of casting must not turn a genuinely bad value into a silent zero.
        Assert.Throws<ArgumentException>(() => ReadOne(
            ["event_id", "severity", "duration", "occurred_at"],
            [1, "not-a-severity", TimeSpan.Zero, DateTimeOffset.UnixEpoch]));
    }

    [Fact]
    public void TimeSpanAndDateTimeOffset_StillReadOnThePlainIDataReaderPath()
    {
        // These share the catch-all arm with enums. They worked before via a cast that succeeded
        // when the provider already returned the right type; the conversion path must not regress
        // that case, which is the common one.
        var duration = TimeSpan.FromSeconds(90);
        var occurred = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.FromHours(2));

        GenEventLog entity = ReadOne(["event_id", "severity", "duration", "occurred_at"],
            [7, 0, duration, occurred]);

        Assert.Equal(7, entity.EventId);
        Assert.Equal(GenEventSeverity.Info, entity.Severity);
        Assert.Equal(duration, entity.Duration);
        Assert.Equal(occurred, entity.OccurredAt);
    }

    [Fact]
    public void Enum_ReadsTheSameWayThroughCreateRowMapper()
    {
        // CreateRowMapper duplicates ReadEntity's emission in a closure; before the fix both of its
        // branches carried the same defect, so both need covering.
        var reader = new StubReader(["event_id", "severity", "duration", "occurred_at"],
            [5, 2, TimeSpan.Zero, DateTimeOffset.UnixEpoch]);

        Func<IDataReader, GenEventLog> map = GenEventLog.CreateRowMapper(reader);
        Assert.True(reader.Read());
        GenEventLog entity = map(reader);

        Assert.Equal(5, entity.EventId);
        Assert.Equal(GenEventSeverity.Error, entity.Severity);
    }

    private static GenEventLog ReadOne(string[] names, object[] values)
    {
        var reader = new StubReader(names, values);
        Assert.True(reader.Read());

        // Guards the premise of the whole class: if this ever became a DbDataReader the tests
        // would silently start exercising the other branch.
        Assert.IsNotType<DbDataReader>(reader, exactMatch: false);

        return GenEventLog.ReadEntity(reader);
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
        public Guid GetGuid(int i) => (Guid)values[i];
        public short GetInt16(int i) => Convert.ToInt16(values[i]);
        public long GetInt64(int i) => Convert.ToInt64(values[i]);
        public int GetValues(object[] target) => 0;
    }
}
