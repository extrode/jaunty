using System.Data;

using Jaunty.Fluent.Internals;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R25 (B5-7): <c>ResultMapperPlan</c>'s stated purpose is that "constructor/property lookups
/// [are] resolved once per query execution and reused across every row, instead of re-running
/// reflection (GetConstructors/GetProperty) per row" - but <c>MapResult</c> still called
/// <c>reader.GetOrdinal(aliases[i])</c> for every column of every row, on both the constructor path
/// and the property path.
///
/// <para>
/// <c>GetOrdinal</c> is a string lookup the provider performs per call - per the
/// <see cref="IDataRecord"/> contract, a case-sensitive scan followed by a case-insensitive rescan -
/// so for a 10-column projection over 10,000 rows that is 100,000 name lookups where 10 would do.
/// Ordinals are stable for the lifetime of a result set and are exactly the kind of thing the plan
/// was introduced to hoist; the core mappers already cache them (<c>EntityDataReader</c>'s
/// layout-keyed cache, the source-generated <c>MapperFactory</c> shape resolution).
/// </para>
///
/// <para>
/// The same code existed as a byte-for-byte private copy inside <c>GroupedQueryBuilder</c>, right
/// down to the per-row <c>GetOrdinal</c>. That copy is gone and the builder now calls this shared
/// mapper, so the two cannot diverge - which <c>ConvertColumnValue</c>'s doc comment already gave
/// as the reason for sharing.
/// </para>
/// </summary>
public class GroupedJoinedResultMapperOrdinalTests
{
    private static readonly string[] Aliases = ["Id", "Name", "Total"];

    // ---------------------------------------------------------------------------
    // Ordinals are resolved once per result set, not once per row
    // ---------------------------------------------------------------------------

    [Fact]
    public void MapResult_ConstructorPath_ResolvesEachOrdinalOnlyOnce()
    {
        var reader = new CountingReader(Aliases, rows: 10);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<CtorRow>(Aliases);

        while (reader.Read())
            GroupedJoinedResultMapper.MapResult<CtorRow>(reader, Aliases, in plan);

        Assert.Equal(10, reader.RowsRead);
        Assert.Equal(Aliases.Length, reader.GetOrdinalCalls);
    }

    [Fact]
    public void MapResult_PropertyPath_ResolvesEachOrdinalOnlyOnce()
    {
        var reader = new CountingReader(Aliases, rows: 10);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<PropertyRow>(Aliases);

        while (reader.Read())
            GroupedJoinedResultMapper.MapResult<PropertyRow>(reader, Aliases, in plan);

        Assert.Equal(Aliases.Length, reader.GetOrdinalCalls);
    }

    [Fact]
    public void MapResult_OverManyRows_DoesNotScaleOrdinalLookupsWithRowCount()
    {
        // The shape of the finding: 10 columns x 10,000 rows was 100,000 lookups.
        var reader = new CountingReader(Aliases, rows: 500);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<CtorRow>(Aliases);

        while (reader.Read())
            GroupedJoinedResultMapper.MapResult<CtorRow>(reader, Aliases, in plan);

        Assert.Equal(500, reader.RowsRead);
        Assert.Equal(3, reader.GetOrdinalCalls);
    }

    [Fact]
    public void MapResult_PropertyPath_NeverLooksUpAnAliasItSkips()
    {
        // Behaviour that had to survive: the property path only resolved an ordinal inside the
        // "property is not null && property.CanWrite" branch, so an alias with no matching writable
        // property was never looked up at all. Resolving the whole buffer eagerly would have turned
        // that skip into a lookup - and, for an alias absent from the result set, into a throw.
        string[] aliases = ["Id", "NotAProperty", "ReadOnly"];
        var reader = new CountingReader(aliases, rows: 3);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<PartlyWritableRow>(aliases);

        while (reader.Read())
            GroupedJoinedResultMapper.MapResult<PartlyWritableRow>(reader, aliases, in plan);

        Assert.Equal(1, reader.GetOrdinalCalls);
        Assert.DoesNotContain("NotAProperty", reader.RequestedNames);
        Assert.DoesNotContain("ReadOnly", reader.RequestedNames);
    }

    [Fact]
    public void MapResult_ReusedPlanOnASecondReader_ReResolvesInsteadOfReturningStaleOrdinals()
    {
        // A plan is created per execution today, but caching ordinals on it makes reuse a
        // correctness question rather than just a performance one. The second reader orders its
        // columns differently, so stale ordinals would map the values to the wrong members.
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<CtorRow>(Aliases);

        var first = new CountingReader(Aliases, rows: 1);
        first.Read();
        CtorRow fromFirst = GroupedJoinedResultMapper.MapResult<CtorRow>(first, Aliases, in plan);

        var second = new CountingReader(["Total", "Name", "Id"], rows: 1);
        second.Read();
        CtorRow fromSecond = GroupedJoinedResultMapper.MapResult<CtorRow>(second, Aliases, in plan);

        // CountingReader returns the ordinal as the value, so a correct mapping puts each column's
        // own position into the matching member regardless of the reader's column order.
        Assert.Equal(0, fromFirst.Id);
        Assert.Equal("1", fromFirst.Name);

        Assert.Equal(2, fromSecond.Id);
        Assert.Equal("1", fromSecond.Name);
        Assert.Equal(0, fromSecond.Total);
    }

    // ---------------------------------------------------------------------------
    // Mapping results themselves are unchanged
    // ---------------------------------------------------------------------------

    [Fact]
    public void MapResult_ConstructorPath_StillMapsEveryColumn()
    {
        var reader = new CountingReader(Aliases, rows: 1);
        reader.Read();
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<CtorRow>(Aliases);

        CtorRow row = GroupedJoinedResultMapper.MapResult<CtorRow>(reader, Aliases, in plan);

        Assert.Equal(0, row.Id);
        Assert.Equal("1", row.Name);
        Assert.Equal(2, row.Total);
    }

    [Fact]
    public void MapResult_PropertyPath_StillMapsEveryWritableColumn()
    {
        var reader = new CountingReader(Aliases, rows: 1);
        reader.Read();
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<PropertyRow>(Aliases);

        PropertyRow row = GroupedJoinedResultMapper.MapResult<PropertyRow>(reader, Aliases, in plan);

        Assert.Equal(0, row.Id);
        Assert.Equal("1", row.Name);
        Assert.Equal(2, row.Total);
    }

    [Fact]
    public void MapResult_NullColumn_IsStillLeftAtItsDefault()
    {
        var reader = new CountingReader(Aliases, rows: 1) { NullOrdinal = 1 };
        reader.Read();
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<PropertyRow>(Aliases);

        PropertyRow row = GroupedJoinedResultMapper.MapResult<PropertyRow>(reader, Aliases, in plan);

        Assert.Null(row.Name);
        Assert.Equal(0, row.Id);
    }

    // ---------------------------------------------------------------------------
    // AUD-R31: constructor parameters are matched to aliases by name, not position
    // ---------------------------------------------------------------------------

    [Fact]
    public void MapResult_ConstructorParametersInDifferentOrderThanAliases_BindsByName()
    {
        var reader = new CountingReader(Aliases, rows: 1);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<ReversedCtorRow>(Aliases);

        Assert.True(reader.Read());
        ReversedCtorRow row = GroupedJoinedResultMapper.MapResult<ReversedCtorRow>(reader, Aliases, in plan);

        Assert.Equal(0, row.Id);
        Assert.Equal(1, row.Name);
        Assert.Equal(2, row.Total);
    }

    [Fact]
    public void MapResult_ConstructorParametersMatchingAliasOrder_StillBindsCorrectly()
    {
        var reader = new CountingReader(Aliases, rows: 1);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<CtorRow>(Aliases);

        Assert.True(reader.Read());
        CtorRow row = GroupedJoinedResultMapper.MapResult<CtorRow>(reader, Aliases, in plan);

        Assert.Equal(0, row.Id);
        Assert.Equal("1", row.Name);
        Assert.Equal(2, row.Total);
    }

    [Fact]
    public void MapResult_ConstructorParameterNamesUnrelatedToAliases_RemainsPositional()
    {
        var reader = new CountingReader(Aliases, rows: 1);
        GroupedJoinedResultMapper.ResultMapperPlan plan =
            GroupedJoinedResultMapper.ResultMapperPlan.Resolve<UnrelatedCtorRow>(Aliases);

        Assert.True(reader.Read());
        UnrelatedCtorRow row = GroupedJoinedResultMapper.MapResult<UnrelatedCtorRow>(reader, Aliases, in plan);

        Assert.Equal(0, row.A);
        Assert.Equal(1, row.B);
        Assert.Equal(2, row.C);
    }

    // ---------------------------------------------------------------------------

    public class CtorRow(int id, string name, int total)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public int Total { get; } = total;
    }

    public class ReversedCtorRow(int total, int name, int id)
    {
        public int Id { get; } = id;
        public int Name { get; } = name;
        public int Total { get; } = total;
    }

    public class UnrelatedCtorRow(int a, int b, int c)
    {
        public int A { get; } = a;
        public int B { get; } = b;
        public int C { get; } = c;
    }

    public class PropertyRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Total { get; set; }
    }

    public class PartlyWritableRow
    {
        public int Id { get; set; }
        public string ReadOnly => "";
    }

    /// <summary>
    /// A one-column-per-alias reader that counts <see cref="GetOrdinal"/> calls and returns each
    /// column's own ordinal as its value, so a mis-mapped column is visible in the result.
    /// </summary>
    private sealed class CountingReader(string[] names, int rows) : IDataReader
    {
        private int _row;

        public int GetOrdinalCalls { get; private set; }
        public int RowsRead { get; private set; }
        public List<string> RequestedNames { get; } = [];
        public int? NullOrdinal { get; set; }

        public int GetOrdinal(string name)
        {
            GetOrdinalCalls++;
            RequestedNames.Add(name);

            int index = Array.IndexOf(names, name);
            return index >= 0 ? index : throw new IndexOutOfRangeException(name);
        }

        public bool Read()
        {
            if (_row >= rows)
                return false;

            _row++;
            RowsRead++;
            return true;
        }

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public object GetValue(int i) => i;
        public bool IsDBNull(int i) => NullOrdinal == i;
        public Type GetFieldType(int i) => typeof(int);

        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public bool NextResult() => false;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => i;
        public object this[string name] => GetOrdinal(name);

        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "int";
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => i;
        public double GetDouble(int i) => i;
        public float GetFloat(int i) => i;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => (short)i;
        public int GetInt32(int i) => i;
        public long GetInt64(int i) => i;
        public string GetString(int i) => i.ToString();
        public int GetValues(object[] values) => 0;
    }
}
