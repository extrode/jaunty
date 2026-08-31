using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Parameters;

using Xunit;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// AUD-R25 (B6-2): the read and write paths resolved <see cref="JauntyConfig.DefaultEnumStorage"/>
/// at different times, so they could disagree.
///
/// <para>
/// On the write path <c>BuildValueConverter</c> deliberately defers the lookup into its per-call
/// closure, and its XML doc spells out why: the default is "mutable process-wide state (callers can
/// change it at runtime, e.g. tests or multi-tenant apps) and so must be re-checked on every call
/// rather than captured once - same reasoning CreateSetter already applies". On the read path
/// <c>CreateFallbackSetter</c> did the opposite: <c>enumAttr?.Storage ?? DefaultEnumStorage</c> was
/// evaluated once, inside <c>MetadataCache&lt;T&gt;</c>'s static constructor, and the resulting
/// setter was baked in for the process lifetime. The comment cited CreateSetter as precedent for
/// behaviour CreateSetter did not have.
/// </para>
///
/// <para>
/// The audit recorded the consequence as per-row failures once an application set
/// <c>DefaultEnumStorage = String</c> after entity T had been read. Verifying it showed that
/// overstated: both setters accept both representations, because the String-storage setter falls
/// back to a numeric value in its <c>catch</c> and the Convert path routes through
/// <c>DbValueConverter.ChangeType</c>, which <c>Enum.Parse</c>s a string enum. On well-formed data
/// the stale choice is invisible.
/// </para>
///
/// <para>
/// It is observable on input neither setter can parse, where the two report different exceptions -
/// see <see cref="TheSetterChoiceFollowsTheCurrentDefault_NotTheOneInEffectAtFirstRead"/>, the one
/// test here that fails without the fix. The finding stands on its own terms regardless: the read
/// path baked in mutable process-wide state while the write path deliberately re-checked it, and
/// justified doing so by citing a precedent that did not exist. <c>JauntyConfig.Reset()</c> resets
/// <c>_defaultEnumStorage</c> to Numeric but cannot reset <c>MetadataCache&lt;T&gt;</c>'s static
/// state, so the divergence survived exactly the test-isolation mechanism meant to clear it.
/// </para>
///
/// <para>
/// Each test below uses its own entity type, because the defect is about state baked into
/// <c>MetadataCache&lt;T&gt;</c>'s static constructor - a type already materialized by an earlier
/// test would not exercise it.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class EnumStorageReadWriteParityTests : IDisposable
{
    public EnumStorageReadWriteParityTests()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
    }

    public enum Status
    {
        Pending = 0,
        Active = 1,
    }

    // ------------------------------------------------------------------
    // The defect: the first read decides how every later read behaves
    // ------------------------------------------------------------------

    [Table("flip_after_first_read")]
    public class FlipAfterFirstRead
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void ChangingTheDefaultAfterAFirstRead_TakesEffectOnTheNextRead()
    {
        // Materialize MetadataCache<T> under Numeric, which is what used to bake the setter in.
        var numeric = Read<FlipAfterFirstRead>(new NumericRow(1));
        Assert.Equal(Status.Active, numeric.Status);

        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        // The same entity type, now stored as a name. Before the fix this threw
        // InvalidOperationException from the string-to-numeric fallback.
        var stringed = Read<FlipAfterFirstRead>(new StringRow(nameof(Status.Active)));
        Assert.Equal(Status.Active, stringed.Status);
    }

    [Table("flip_back")]
    public class FlipBack
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void ChangingTheDefaultBack_TakesEffectAgain()
    {
        // The re-check must be a live read of the config, not a one-time upgrade to String.
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;
        Assert.Equal(Status.Active, Read<FlipBack>(new StringRow(nameof(Status.Active))).Status);

        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
        Assert.Equal(Status.Active, Read<FlipBack>(new NumericRow(1)).Status);

        JauntyConfig.DefaultEnumStorage = EnumStorage.String;
        Assert.Equal(Status.Pending, Read<FlipBack>(new StringRow(nameof(Status.Pending))).Status);
    }

    [Table("read_write_parity")]
    public class ReadWriteParity
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void ReadAndWrite_AgreeAfterTheDefaultChanges()
    {
        // The heart of the finding: the write path always re-checked, so after a flip the two
        // halves disagreed about the same column on the same entity.
        Read<ReadWriteParity>(new NumericRow(1));

        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        object? written = BindAndCapture(new ReadWriteParity { Id = 1, Status = Status.Active });
        Assert.Equal(nameof(Status.Active), written);

        // Whatever the write path produced must be what the read path accepts.
        Assert.Equal(Status.Active, Read<ReadWriteParity>(new StringRow((string)written!)).Status);
    }

    [Table("selection_follows_config")]
    public class SelectionFollowsConfig
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void TheSetterChoiceFollowsTheCurrentDefault_NotTheOneInEffectAtFirstRead()
    {
        // The discriminating case. Both setters accept both representations - the String-storage
        // setter falls back to a numeric value in its catch, and the Convert path routes through
        // DbValueConverter.ChangeType, which Enum.Parses a string enum - so on well-formed input
        // the choice is invisible. It becomes observable on input neither can parse: the
        // String-storage setter reports InvalidOperationException naming the value and the property,
        // while the Convert path lets Enum.Parse's own ArgumentException out.
        Read<SelectionFollowsConfig>(new NumericRow(1));

        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        var ex = Assert.Throws<InvalidOperationException>(
            () => Read<SelectionFollowsConfig>(new StringRow("not-a-status")));

        Assert.Contains("not-a-status", ex.Message);
        Assert.Contains(nameof(SelectionFollowsConfig), ex.Message);
    }

    // ------------------------------------------------------------------
    // An attribute is immutable, so it is still baked in - and still wins
    // ------------------------------------------------------------------

    [Table("attributed_string")]
    public class AttributedString
    {
        public int Id { get; set; }

        [EnumStorage(Attributes.EnumStorage.String)]
        public Status Status { get; set; }
    }

    [Fact]
    public void AttributedProperty_IgnoresTheGlobalDefault()
    {
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;

        Assert.Equal(Status.Active, Read<AttributedString>(new StringRow(nameof(Status.Active))).Status);

        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        Assert.Equal(Status.Active, Read<AttributedString>(new StringRow(nameof(Status.Active))).Status);
    }

    [Table("attributed_numeric")]
    public class AttributedNumeric
    {
        public int Id { get; set; }

        [EnumStorage(Attributes.EnumStorage.Numeric)]
        public Status Status { get; set; }
    }

    [Fact]
    public void AttributedNumericProperty_IsNotSwitchedByTheGlobalDefault()
    {
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        Assert.Equal(Status.Active, Read<AttributedNumeric>(new NumericRow(1)).Status);
    }

    // ------------------------------------------------------------------
    // Unchanged behaviour
    // ------------------------------------------------------------------

    [Table("string_storage_tolerates_numeric")]
    public class StringStorageToleratesNumeric
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void StringStorage_StillFallsBackToANumericValue()
    {
        // The numeric-fallback catch inside the string setter is pre-existing tolerance for a
        // column that holds numbers despite String storage; deferring the choice must not lose it.
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        Assert.Equal(Status.Active, Read<StringStorageToleratesNumeric>(new StringRow("1")).Status);
    }

    [Table("string_storage_rejects_garbage")]
    public class StringStorageRejectsGarbage
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void StringStorage_StillThrowsOnAnUnparseableValue()
    {
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        var ex = Assert.Throws<InvalidOperationException>(
            () => Read<StringStorageRejectsGarbage>(new StringRow("not-a-status")));

        Assert.Contains("not-a-status", ex.Message);
    }

    [Table("case_insensitive_names")]
    public class CaseInsensitiveNames
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    [Fact]
    public void StringStorage_StillParsesNamesCaseInsensitively()
    {
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        Assert.Equal(Status.Active, Read<CaseInsensitiveNames>(new StringRow("ACTIVE")).Status);
    }

    // ------------------------------------------------------------------

    private static T Read<T>(SingleRowReader reader) where T : new()
    {
        var map = JauntyConfig.ReflectionMapperResolver?.Invoke(typeof(T), MappingMode.Strict) as Func<IDataReader, T>;
        Assert.True(map is not null, "the reflection mapper resolver returned no mapper");

        Assert.True(reader.Read());
        return map!(reader);
    }

    /// <summary>
    /// Runs the write path over a single-property entity and returns the value it produced for
    /// Status, which is what <c>BuildValueConverter</c> decides.
    /// </summary>
    private static object? BindAndCapture<T>(T entity) where T : notnull
    {
        var command = new CapturingCommand { CommandText = "UPDATE t SET Status = @Status WHERE Id = @Id" };
        ParameterBinder.Bind(command, entity);

        foreach (CapturingParameter parameter in command.Captured)
        {
            if (parameter.ParameterName.EndsWith("Status", StringComparison.OrdinalIgnoreCase))
                return parameter.Value;
        }

        Assert.Fail("no Status parameter was bound");
        return null;
    }


    // ------------------------------------------------------------------

    public abstract class SingleRowReader : IDataReader
    {
        private int _row;

        protected abstract object StatusValue { get; }

        public int FieldCount => 2;
        public string GetName(int i) => i == 0 ? "Id" : "Status";
        public int GetOrdinal(string name) => string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        public object GetValue(int i) => i == 0 ? 1 : StatusValue;
        public Type GetFieldType(int i) => i == 0 ? typeof(int) : StatusValue.GetType();
        public bool IsDBNull(int i) => false;
        public int GetInt32(int i) => i == 0 ? 1 : Convert.ToInt32(StatusValue);
        public string GetString(int i) => GetValue(i).ToString()!;

        public bool Read() => _row++ < 1;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));

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
        public int GetValues(object[] values) => 0;
    }

    public sealed class NumericRow(int status) : SingleRowReader
    {
        protected override object StatusValue => status;
    }

    public sealed class StringRow(string status) : SingleRowReader
    {
        protected override object StatusValue => status;
    }

    private sealed class CapturingParameter : IDbDataParameter
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

    private sealed class CapturingCommand : IDbCommand
    {
        public List<CapturingParameter> Captured { get; } = [];

        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; }
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public CapturingCommand() => Parameters = new CapturingCollection(Captured);

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new CapturingParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }

        private sealed class CapturingCollection(List<CapturingParameter> captured) : List<object>, IDataParameterCollection
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
                if (value is CapturingParameter parameter)
                    captured.Add(parameter);

                base.Add(value);
                return Count - 1;
            }
        }
    }
}
