using System.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.TypeHandlers;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// coverage-gaps-2026-09-20: <c>MetadataCache&lt;T&gt;.CreateSetter</c>'s type-handler branch on the
/// read path had no test reaching either the <c>Nullable&lt;T&gt;</c> conversion arm (when the
/// property is nullable but the handler parses to the non-nullable underlying type) or the
/// <c>handler.Parse</c>-throws arm - the write-path equivalent
/// (<see cref="ThrowingTypeHandlerContractTests"/>) was covered, the read path wasn't.
/// </summary>
[Collection("Type Handler Operations")]
public class TypeHandlerReadPathReflectionTests : IDisposable
{
    public TypeHandlerReadPathReflectionTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose() => JauntyConfig.RemoveTypeHandler<Guid>();

    [Table("type_handler_read_widgets")]
    public class Widget
    {
        [Key]
        public int Id { get; set; }
        public Guid? Token { get; set; }
    }

    private sealed class StringGuidHandler : TypeHandler<Guid>
    {
        public override Guid Parse(object? dbValue) => Guid.Parse((string)dbValue!);
        public override object? ToDbValue(Guid value) => value.ToString();
    }

    private sealed class ThrowingGuidHandler : TypeHandler<Guid>
    {
        public override Guid Parse(object? dbValue) => throw new FormatException("handler is broken");
        public override object? ToDbValue(Guid value) => value.ToString();
    }

    [Fact]
    public void NullablePropertyWithARegisteredHandler_ConvertsTheParsedValueToTheUnderlyingType()
    {
        JauntyConfig.RegisterTypeHandler(new StringGuidHandler());

        Guid guid = Guid.NewGuid();
        var reader = new SingleRowReader(["Id", "Token"], [1, guid.ToString()]);

        PropertySetter<Widget>[] setters = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);
        var widget = new Widget();
        Assert.True(reader.Read());
        foreach (PropertySetter<Widget> setter in setters)
            setter.Set(widget, reader);

        Assert.Equal(guid, widget.Token);
    }

    [Fact]
    public void NullablePropertyWhoseHandlerThrows_WrapsTheFailureInAnInvalidOperationException()
    {
        JauntyConfig.RegisterTypeHandler(new ThrowingGuidHandler());

        var reader = new SingleRowReader(["Id", "Token"], [1, "not-a-guid"]);

        PropertySetter<Widget>[] setters = MetadataCache<Widget>.GetSetters(reader, MappingMode.Strict);
        var widget = new Widget();
        Assert.True(reader.Read());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (PropertySetter<Widget> setter in setters)
                setter.Set(widget, reader);
        });

        Assert.Contains(nameof(Widget.Token), ex.Message);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    private sealed class SingleRowReader(string[] names, object[] values) : IDataReader
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
