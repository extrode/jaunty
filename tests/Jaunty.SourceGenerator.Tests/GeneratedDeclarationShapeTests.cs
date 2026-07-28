using System.Data;
using System.Reflection;

using Jaunty.Interfaces;
using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R25 (B8-2, B8-3, B8-4): three ways the generator emitted a partial declaration that could
/// not compile, all of them reachable from ordinary entity shapes.
///
/// <para>
/// Like <c>HintNameCollisionTests</c>, the primary proof is that this project builds at all - the
/// entities in <c>Entities/GenInternalEntity.cs</c>, <c>Entities/GenSplitEntity{,.Extra}.cs</c> and
/// <c>Entities/GenIndexerEntity.cs</c> each reproduce one of the three failures against the
/// unfixed generator (CS0262, a duplicate hint name, and a `.Item` member access respectively).
/// The assertions below add the half a build cannot show: that having compiled, each entity got a
/// real, working generated mapper rather than being silently skipped.
/// </para>
/// </summary>
public sealed class GeneratedDeclarationShapeTests
{
    // ------------------------------------------------------------------
    // B8-2: internal entity
    // ------------------------------------------------------------------

    [Fact]
    public void InternalEntity_GetsAGeneratedMapper()
    {
        Assert.True(typeof(IMapped<GenInternalEntity>).IsAssignableFrom(typeof(GenInternalEntity)));
        Assert.True(typeof(IEntityMetadataSource).IsAssignableFrom(typeof(GenInternalEntity)));
    }

    [Fact]
    public void InternalEntity_GeneratedPartialKeepsTheDeclaredAccessibility()
    {
        // The generated part is what would have said `public`. If accessibility is ever emitted
        // from something other than the symbol, the type's own accessibility is what shifts.
        Assert.True(typeof(GenInternalEntity).IsNotPublic);
    }

    [Fact]
    public void InternalEntity_MapsARow()
    {
        var reader = new StubReader(["id", "name"], [42, "internal-entity"]);
        Assert.True(reader.Read());

        GenInternalEntity entity = GenInternalEntity.ReadEntity(reader);

        Assert.Equal(42, entity.Id);
        Assert.Equal("internal-entity", entity.Name);
    }

    [Fact]
    public void InternalEntity_ExposesItsTableName()
    {
        Assert.Equal("gen_internal_entities", GenInternalEntity.TableName);
    }

    // ------------------------------------------------------------------
    // B8-3: entity split across two files
    // ------------------------------------------------------------------

    [Fact]
    public void SplitEntity_GetsExactlyOneGeneratedMapper()
    {
        // Two AddSource calls with the same hint name fail the build outright, so reaching this
        // assertion is most of the proof. What it adds is that the surviving emission is a single
        // coherent one - a duplicated member set would have failed differently (CS0111).
        MethodInfo[] readEntity = typeof(GenSplitEntity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(GenSplitEntity.ReadEntity))
            .ToArray();

        Assert.Single(readEntity);
    }

    [Fact]
    public void SplitEntity_MapsARow_AndKeepsTheHandWrittenPart()
    {
        var reader = new StubReader(["id", "name"], [7, "split"]);
        Assert.True(reader.Read());

        GenSplitEntity entity = GenSplitEntity.ReadEntity(reader);

        Assert.Equal(7, entity.Id);
        Assert.Equal("split", entity.Name);
        // Declared in the second file, which is the part that carries no [Table].
        Assert.Equal("7:split", entity.Describe());
    }

    [Fact]
    public void SplitEntity_ExposesItsTableName()
    {
        Assert.Equal("gen_split_entities", GenSplitEntity.TableName);
    }

    // ------------------------------------------------------------------
    // B8-4: entity with a settable indexer
    // ------------------------------------------------------------------

    [Fact]
    public void IndexerEntity_DoesNotTreatTheIndexerAsAColumn()
    {
        // "Item" is the name an indexer surfaces under; it must not appear as a mapped column.
        IReadOnlyList<string> columns = [.. GenIndexerEntity.EntityColumns.Select(c => c.PropertyName)];

        Assert.DoesNotContain("Item", columns);
        Assert.Contains("Id", columns);
        Assert.Contains("Name", columns);
    }

    [Fact]
    public void IndexerEntity_MapsARow_AndLeavesTheIndexerAlone()
    {
        var reader = new StubReader(["id", "name"], [3, "indexed"]);
        Assert.True(reader.Read());

        GenIndexerEntity entity = GenIndexerEntity.ReadEntity(reader);

        Assert.Equal(3, entity.Id);
        Assert.Equal("indexed", entity.Name);
        Assert.Null(entity[0]);

        // The indexer still works as the user wrote it - it was skipped, not suppressed.
        entity[0] = "slot-zero";
        Assert.Equal("slot-zero", entity[0]);
    }

    [Fact]
    public void IndexerEntity_ExposesItsTableName()
    {
        Assert.Equal("gen_indexer_entities", GenIndexerEntity.TableName);
    }

    // ------------------------------------------------------------------

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
