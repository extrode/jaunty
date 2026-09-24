using System.Data;

namespace Extrode.Jaunty.Tests.Helpers;

/// <summary>
/// A one-row <see cref="IDataReader"/> whose column layout can be swapped in place, standing in for
/// a provider (Npgsql) that recycles one reader instance across commands with different shapes.
/// </summary>
internal sealed class MutableStubReader(string?[] names, object?[] values) : IDataReader
{
    private string?[] _names = names;
    private object?[] _values = values;

    public void Reshape(string?[] names, object?[] values)
    {
        _names = names;
        _values = values;
    }

    public int FieldCount => _names.Length;
    public string GetName(int i) => _names[i]!;
    public int GetOrdinal(string name) => Array.FindIndex(_names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    public object GetValue(int i) => _values[i] ?? DBNull.Value;
    public bool IsDBNull(int i) => _values[i] is null or DBNull;
    public Type GetFieldType(int i) => _values[i]?.GetType() ?? typeof(object);
    public int GetInt32(int i) => Convert.ToInt32(_values[i]);
    public string GetString(int i) => (string)_values[i]!;

    public bool Read() => true;
    public bool NextResult() => false;
    public int Depth => 0;
    public bool IsClosed => false;
    public int RecordsAffected => 0;
    public void Close() { }
    public void Dispose() { }
    public DataTable? GetSchemaTable() => null;

    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));

    public bool GetBoolean(int i) => Convert.ToBoolean(_values[i]);
    public byte GetByte(int i) => Convert.ToByte(_values[i]);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
    public char GetChar(int i) => Convert.ToChar(_values[i]);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
    public IDataReader GetData(int i) => throw new NotSupportedException();
    public string GetDataTypeName(int i) => GetFieldType(i).Name;
    public DateTime GetDateTime(int i) => Convert.ToDateTime(_values[i]);
    public decimal GetDecimal(int i) => Convert.ToDecimal(_values[i]);
    public double GetDouble(int i) => Convert.ToDouble(_values[i]);
    public float GetFloat(int i) => Convert.ToSingle(_values[i]);
    public Guid GetGuid(int i) => (Guid)_values[i]!;
    public short GetInt16(int i) => Convert.ToInt16(_values[i]);
    public long GetInt64(int i) => Convert.ToInt64(_values[i]);
    public int GetValues(object[] values) => 0;
}
