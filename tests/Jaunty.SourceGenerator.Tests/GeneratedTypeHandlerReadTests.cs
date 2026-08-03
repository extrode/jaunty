using System.Data;

using Jaunty.Configuration;
using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R35. <c>DrDispatcher.Resolve</c> prefers the generated mapper once the generator package is
/// referenced, and the generated read path never consulted <see cref="JauntyConfig"/>'s type-handler
/// registry - emitted <c>ReadEntity</c>/<c>CreateRowMapper</c> went straight to typed getters or
/// <c>ReadFallback&lt;T&gt;</c>. The generated write path did consult it (AUD-R30-002), as does the
/// reflection read path, so a registered handler applied on write and was skipped on read for the
/// same entity. These pin the read side.
/// </summary>
public sealed class GeneratedTypeHandlerReadTests
{
    private static readonly string[] Columns = ["id", "amount"];

    private static PlainRecordReader Reader(object amount)
    {
        var reader = new PlainRecordReader(Columns, [1, amount]);
        Assert.True(reader.Read());
        return reader;
    }

    private static void WithMoneyHandler(Action body)
    {
        JauntyConfig.RegisterTypeHandler<GenMoney>(
            fromDb: v => new GenMoney(Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture)),
            toDb: m => m.Amount);

        try
        {
            body();
        }
        finally
        {
            JauntyConfig.RemoveTypeHandler<GenMoney>();
        }
    }

    [Fact]
    public void ReadEntity_AppliesARegisteredHandler()
    {
        WithMoneyHandler(() =>
        {
            GenHandledEntity entity = GenHandledEntity.ReadEntity(Reader(12.5m));

            Assert.Equal(12.5m, entity.Amount.Amount);
        });
    }

    [Fact]
    public void CreateRowMapper_AppliesARegisteredHandler()
    {
        WithMoneyHandler(() =>
        {
            PlainRecordReader reader = Reader(99.25m);
            Func<IDataReader, GenHandledEntity> map = GenHandledEntity.CreateRowMapper(reader);

            Assert.Equal(99.25m, map(reader).Amount.Amount);
        });
    }

    [Fact]
    public void ReadEntity_HandlerRegisteredForAnotherType_LeavesThisPropertyAlone()
    {
        JauntyConfig.RegisterTypeHandler<Uri>(fromDb: v => new Uri((string)v!), toDb: u => u?.ToString());

        try
        {
            GenHandledEntity entity = GenHandledEntity.ReadEntity(Reader(new GenMoney(3m)));

            Assert.Equal(3m, entity.Amount.Amount);
        }
        finally
        {
            JauntyConfig.RemoveTypeHandler<Uri>();
        }
    }

    [Fact]
    public void ReadEntity_NoHandler_ReadsTheValueAsItStands()
    {
        GenHandledEntity entity = GenHandledEntity.ReadEntity(Reader(new GenMoney(7m)));

        Assert.Equal(7m, entity.Amount.Amount);
        Assert.Equal(1, entity.Id);
    }

    [Fact]
    public void TheHandlerIsAppliedOnBothSidesOfTheRoundTrip()
    {
        WithMoneyHandler(() =>
        {
            var command = new RecordingCommand();
            GenHandledEntity.BindInsert(command, new GenHandledEntity { Id = 1, Amount = new GenMoney(4.5m) });

            Assert.Contains(command.Bound, p => p.ParameterName == "@amount" && Equals(p.Value, 4.5m));
            Assert.Equal(4.5m, GenHandledEntity.ReadEntity(Reader(4.5m)).Amount.Amount);
        });
    }

    private sealed class RecordingParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class RecordingCommand : IDbCommand
    {
        public List<RecordingParameter> Bound { get; } = [];

        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; }
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public RecordingCommand() => Parameters = new Collection(Bound);

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new RecordingParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }

        private sealed class Collection(List<RecordingParameter> bound) : List<object>, IDataParameterCollection
        {
            public object this[string parameterName]
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }

            public new int Add(object value)
            {
                if (value is RecordingParameter parameter)
                    bound.Add(parameter);

                base.Add(value);
                return Count - 1;
            }
        }
    }

    private sealed class PlainRecordReader(string[] names, object[] values) : IDataReader
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
        public IDataReader GetData(int i) => this;
        public string GetDataTypeName(int i) => values[i].GetType().Name;
        public DateTime GetDateTime(int i) => Convert.ToDateTime(values[i]);
        public decimal GetDecimal(int i) => Convert.ToDecimal(values[i]);
        public double GetDouble(int i) => Convert.ToDouble(values[i]);
        public float GetFloat(int i) => Convert.ToSingle(values[i]);
        public Guid GetGuid(int i) => (Guid)values[i];
        public short GetInt16(int i) => Convert.ToInt16(values[i]);
        public long GetInt64(int i) => Convert.ToInt64(values[i]);
        public int GetValues(object[] valueArray) => 0;
    }
}
