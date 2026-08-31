using System.Collections.Concurrent;
using System.Data;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// R27 batch 11 (medium). The emitted OrdinalMap.Resolve refreshed its per-reader memo with
/// ConditionalWeakTable.Remove followed by Add. Add throws ArgumentException when the key is
/// already present, so two threads that both missed on the same reader could both remove and
/// then both add, the second throwing from generated code the consumer cannot edit. The
/// generator now emits AddOrUpdate (net8+) / a locked pair (older TFMs), mirroring
/// MetadataCache.GetSetters.
/// </summary>
public sealed class GeneratedOrdinalMapConcurrencyTests
{
    [Fact]
    public async Task ConcurrentFirstResolve_OnOneReader_DoesNotThrow()
    {
        var exceptions = new ConcurrentBag<Exception>();

        for (int round = 0; round < 100; round++)
        {
            var reader = new StubReader(["id", "name"], [42, "row"]);
            Assert.True(reader.Read());
            using var gate = new Barrier(4);

            var tasks = new Task[4];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    gate.SignalAndWait();
                    try
                    {
                        GenInternalEntity.ReadEntity(reader);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        Assert.Empty(exceptions);
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
        public int GetValues(object[] target) => 0;
    }
}
