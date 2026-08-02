using System.Data;
using System.Runtime.CompilerServices;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R34-032 (round-33 carry-forward). <c>OrdinalMap._last</c> is a <c>[ThreadStatic]</c> memo of
/// the last resolved <c>CacheEntry</c>, and <c>CacheEntry</c> holds the reader - and through it the
/// command and the connection. Held strongly, that slot is only ever cleared by the next resolve of
/// the same entity type on the same thread, so a pooled or idle thread that mapped one entity and
/// then went quiet pinned the whole chain past <c>Dispose</c> for an unbounded time. The
/// <c>ConditionalWeakTable</c> beside it is deliberately weak; this was the one strong root undoing
/// it.
/// </summary>
public sealed class GeneratedOrdinalMapLifetimeTests
{
    [Fact]
    public void ResolvingOrdinals_DoesNotPinTheReaderToTheThread()
    {
        WeakReference reader = MapOneRowAndDropTheReader();

        Collect();

        Assert.False(reader.IsAlive);
    }

    /// <summary>
    /// And the memo it exists for still works: a second read of the same live reader must not
    /// re-resolve, which is what a weak slot could plausibly have broken.
    /// </summary>
    [Fact]
    public void TheFastPath_StillHitsWhileTheReaderIsAlive()
    {
        var reader = new CountingReader(["id", "name"], [7, "row"]);

        GenLifetimeEntity.ReadEntity(reader);
        int afterFirst = reader.GetOrdinalCalls;

        Collect();
        GenLifetimeEntity.ReadEntity(reader);

        Assert.Equal(afterFirst, reader.GetOrdinalCalls);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MapOneRowAndDropTheReader()
    {
        var reader = new CountingReader(["id", "name"], [1, "row"]);
        Assert.True(reader.Read());
        GenLifetimeEntity.ReadEntity(reader);
        return new WeakReference(reader);
    }

    private static void Collect()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private sealed class CountingReader(string[] names, object[] values) : IDataReader
    {
        private int _row;

        public int GetOrdinalCalls { get; private set; }

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];

        public int GetOrdinal(string name)
        {
            GetOrdinalCalls++;
            return Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        }

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
        public IDataReader GetData(int i) => this;
        public string GetDataTypeName(int i) => values[i].GetType().Name;
        public DateTime GetDateTime(int i) => DateTime.MinValue;
        public decimal GetDecimal(int i) => 0m;
        public double GetDouble(int i) => 0d;
        public float GetFloat(int i) => 0f;
        public Guid GetGuid(int i) => Guid.Empty;
        public short GetInt16(int i) => 0;
        public long GetInt64(int i) => 0L;
        public int GetValues(object[] valueArray) => 0;
    }
}
