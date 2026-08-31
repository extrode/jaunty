using System.Data;

using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-199 on <c>GroupedJoinedResultMapper.ResultMapperPlan.Resolve</c>'s property path.
/// </summary>
public class ResultMapperPropertyLookupTests
{
    private static TResult Map<TResult>(string[] aliases, object?[] values)
    {
        var reader = new StubReader(aliases, values);
        reader.Read();
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        return GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases, in plan);
    }

    [Fact]
    public void AnAliasWhoseCasingDiffersFromThePropertyIsStillBound()
    {
        string[] aliases = ["name", "total"];

        NamedRow row = Map<NamedRow>(aliases, ["widget", 7]);

        Assert.Equal("widget", row.Name);
        Assert.Equal(7, row.Total);
    }

    [Fact]
    public void AnExactMatchIsPreferredOverACaseInsensitiveOne()
    {
        string[] aliases = ["Total", "total"];

        TwoCasingsRow row = Map<TwoCasingsRow>(aliases, [1, 2]);

        Assert.Equal(1, row.Total);
        Assert.Equal(2, row.total);
    }

    [Fact]
    public void APropertyReDeclaredWithNewDoesNotThrowAmbiguousMatch()
    {
        string[] aliases = ["Id", "Name"];

        DerivedRow row = Map<DerivedRow>(aliases, ["abc", "widget"]);

        Assert.Equal("abc", row.Id);
        Assert.Equal("widget", row.Name);
        Assert.Equal(0, ((BaseRow)row).Id);
    }

    [Fact]
    public void AnAliasWithNoMatchingPropertyIsStillSkipped()
    {
        string[] aliases = ["Name", "NotAProperty"];

        NamedRow row = Map<NamedRow>(aliases, ["widget", 3]);

        Assert.Equal("widget", row.Name);
        Assert.Equal(0, row.Total);
    }

    public class NamedRow
    {
        public string? Name { get; set; }
        public int Total { get; set; }
    }

    public class TwoCasingsRow
    {
        public int Total { get; set; }
        public int total { get; set; }
    }

    public class BaseRow
    {
        public int Id { get; set; }
    }

    public class DerivedRow : BaseRow
    {
        public new string? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class StubReader(string[] names, object?[] values) : IDataReader
    {
        private bool _read;

        public int GetOrdinal(string name)
        {
            int index = Array.IndexOf(names, name);
            return index >= 0 ? index : throw new IndexOutOfRangeException(name);
        }

        public bool Read()
        {
            if (_read) return false;
            _read = true;
            return true;
        }

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public object GetValue(int i) => values[i]!;
        public bool IsDBNull(int i) => values[i] is null;
        public Type GetFieldType(int i) => values[i]?.GetType() ?? typeof(object);

        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public bool NextResult() => false;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => values[i]!;
        public object this[string name] => values[GetOrdinal(name)]!;

        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "int";
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => 0;
        public string GetString(int i) => values[i]?.ToString() ?? string.Empty;
        public int GetValues(object[] values) => 0;
    }
}
