using System.Data;

using Jaunty.Fluent.Internals;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-065. The scalar path added by AUD-R34-005 returned <c>default!</c> when the single
/// projected column was NULL, so <c>.Select(g =&gt; g.Max(x =&gt; x.Price))</c> typed as a
/// non-nullable <c>decimal</c>/<c>int</c> yielded <c>0</c> for a group whose aggregate is NULL -
/// inventing data, and indistinguishable from a real zero. The constructor path in the same method
/// had already decided the opposite for exactly this case (AUD-R33-005: a value the type declared
/// it requires, so substituting a zero would be inventing data) and throws a naming
/// <see cref="InvalidOperationException"/>. The two paths now agree.
/// <para>
/// No test drove a NULL through the scalar path: <c>FluentGroupBySingleExpressionProjectionTests</c>
/// covers only non-NULL <c>g.Key</c>/<c>g.Count()</c>, and <c>GroupedJoinedResultMapperOrdinalTests</c>
/// has no scalar case at all.
/// </para>
/// </summary>
public class GroupedJoinedResultMapperNullScalarTests
{
    private static readonly string[] Aliases = ["Total"];

    private static TResult Map<TResult>(bool isNull)
    {
        var reader = new ScalarReader(Aliases[0], isNull);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(Aliases);

        Assert.True(plan.IsScalar);
        return GroupedJoinedResultMapper.MapResult<TResult>(reader, Aliases, in plan);
    }

    // ------------------------------------------------------------------
    // A non-nullable value type: throws, naming the column and the type.
    // ------------------------------------------------------------------

    [Fact]
    public void ANullColumn_IntoDecimal_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Map<decimal>(isNull: true));

        Assert.Contains("Total", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Decimal", ex.Message, StringComparison.Ordinal);
        Assert.Contains("NULL", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullColumn_IntoInt_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Map<int>(isNull: true));
    }

    [Fact]
    public void ANullColumn_IntoLong_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Map<long>(isNull: true));
    }

    [Fact]
    public void ANullColumn_IntoDouble_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Map<double>(isNull: true));
    }

    [Fact]
    public void ANullColumn_IntoDateTime_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Map<DateTime>(isNull: true));
    }

    // ------------------------------------------------------------------
    // Nullable and reference targets keep returning null - the change narrows
    // nothing it should not have.
    // ------------------------------------------------------------------

    [Fact]
    public void ANullColumn_IntoNullableDecimal_ReturnsNull()
    {
        Assert.Null(Map<decimal?>(isNull: true));
    }

    [Fact]
    public void ANullColumn_IntoNullableInt_ReturnsNull()
    {
        Assert.Null(Map<int?>(isNull: true));
    }

    [Fact]
    public void ANullColumn_IntoString_ReturnsNull()
    {
        Assert.Null(Map<string>(isNull: true));
    }

    // ------------------------------------------------------------------
    // The control: a non-NULL column still maps on every one of those targets.
    // ------------------------------------------------------------------

    [Fact]
    public void ANonNullColumn_IntoDecimal_StillMaps()
    {
        Assert.Equal(42m, Map<decimal>(isNull: false));
    }

    [Fact]
    public void ANonNullColumn_IntoNullableDecimal_StillMaps()
    {
        Assert.Equal(42m, Map<decimal?>(isNull: false));
    }

    [Fact]
    public void ANonNullColumn_IntoInt_StillMaps()
    {
        Assert.Equal(42, Map<int>(isNull: false));
    }

    private sealed class ScalarReader(string name, bool isNull) : IDataReader
    {
        public int GetOrdinal(string requested) =>
            requested == name ? 0 : throw new IndexOutOfRangeException(requested);

        public bool IsDBNull(int i) => isNull;
        public object GetValue(int i) => isNull ? DBNull.Value : 42;

        public int FieldCount => 1;
        public string GetName(int i) => name;
        public Type GetFieldType(int i) => typeof(int);

        public bool Read() => false;
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public bool NextResult() => false;
        public void Close() { }
        public void Dispose() { }
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => GetValue(i);
        public object this[string n] => GetValue(GetOrdinal(n));

        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "int";
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 42m;
        public double GetDouble(int i) => 42;
        public float GetFloat(int i) => 42;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 42;
        public int GetInt32(int i) => 42;
        public long GetInt64(int i) => 42;
        public string GetString(int i) => "42";
        public int GetValues(object[] values) => 0;
    }
}
