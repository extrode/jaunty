using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;
using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Internals.BulkCopy;

/// <summary>
/// Unit tests for EntityDataReader{T}.
/// </summary>
public class EntityDataReaderTests : IDisposable
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    [Fact]
    public void EntityDataReader_FieldCount_ReturnsColumnCount()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Product 1", Price = 10.00m, CreatedAt = DateTime.UtcNow }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Equal(4, reader.FieldCount);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_Read_IteratesOverEntities()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Product 1", Price = 10.00m },
            new TestEntity { Id = 2, Name = "Product 2", Price = 20.00m },
            new TestEntity { Id = 3, Name = "Product 3", Price = 30.00m }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act
        int rowCount = 0;
        while (reader.Read())
        {
            rowCount++;
        }

        // Assert
        Assert.Equal(3, rowCount);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetValue_ReturnsCorrectValues()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Product 1", Price = 10.50m, CreatedAt = new DateTime(2024, 1, 15) }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);
        reader.Read();

        // Act & Assert
        Assert.Equal(1, reader.GetValue(0));
        Assert.Equal("Product 1", reader.GetValue(1));
        Assert.Equal(10.50m, reader.GetValue(2));
        Assert.Equal(new DateTime(2024, 1, 15), reader.GetValue(3));

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetValue_NullProperty_ReturnsDBNull()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = null, Price = null, CreatedAt = null }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);
        reader.Read();

        // Act & Assert
        Assert.Equal(1, reader.GetValue(0));
        Assert.Equal(DBNull.Value, reader.GetValue(1));
        Assert.Equal(DBNull.Value, reader.GetValue(2));
        Assert.Equal(DBNull.Value, reader.GetValue(3));

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_IsDBNull_DetectsNullValues()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Product", Price = null, CreatedAt = null }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);
        reader.Read();

        // Act & Assert
        Assert.False(reader.IsDBNull(0)); // Id has value
        Assert.False(reader.IsDBNull(1)); // Name has value
        Assert.True(reader.IsDBNull(2));  // Price is null
        Assert.True(reader.IsDBNull(3));  // CreatedAt is null

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetName_ReturnsColumnName()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Equal("Id", reader.GetName(0));
        Assert.Equal("Name", reader.GetName(1));
        Assert.Equal("Price", reader.GetName(2));
        Assert.Equal("CreatedAt", reader.GetName(3));

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetOrdinal_FindsColumnByName()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Equal(0, reader.GetOrdinal("Id"));
        Assert.Equal(1, reader.GetOrdinal("Name"));
        Assert.Equal(2, reader.GetOrdinal("Price"));
        Assert.Equal(3, reader.GetOrdinal("CreatedAt"));

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetOrdinal_InvalidName_Throws()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => reader.GetOrdinal("NonExistent"));

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetFieldType_ReturnsCorrectTypes()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Equal(typeof(int), reader.GetFieldType(0));
        Assert.Equal(typeof(string), reader.GetFieldType(1));
        Assert.Equal(typeof(decimal?), reader.GetFieldType(2));
        Assert.Equal(typeof(DateTime?), reader.GetFieldType(3));

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetValues_FillsArray()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Product", Price = 15.00m, CreatedAt = new DateTime(2024, 6, 1) }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);
        reader.Read();
        var values = new object[4];

        // Act
        int count = reader.GetValues(values);

        // Assert
        Assert.Equal(4, count);
        Assert.Equal(1, values[0]);
        Assert.Equal("Product", values[1]);
        Assert.Equal(15.00m, values[2]);
        Assert.Equal(new DateTime(2024, 6, 1), values[3]);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_Indexer_ByOrdinal_Works()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 42, Name = "Test", Price = 9.99m, CreatedAt = DateTime.UtcNow }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);
        reader.Read();

        // Act & Assert
        Assert.Equal(42, reader[0]);
        Assert.Equal("Test", reader[1]);
        Assert.Equal(9.99m, reader[2]);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_Indexer_ByName_Works()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 100, Name = "Named Access", Price = 19.99m, CreatedAt = DateTime.UtcNow }
        };
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);
        reader.Read();

        // Act & Assert
        Assert.Equal(100, reader["Id"]);
        Assert.Equal("Named Access", reader["Name"]);
        Assert.Equal(19.99m, reader["Price"]);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_Dispose_ClosesEnumerator()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act
        reader.Dispose();

        // Assert
        Assert.True(reader.IsClosed);
    }

    [Fact]
    public void EntityDataReader_EmptyCollection_ReadReturnsFalse()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.False(reader.Read());

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_Depth_IsZero()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Equal(0, reader.Depth);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_RecordsAffected_IsZero()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Equal(0, reader.RecordsAffected);

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_NextResult_ReturnsFalse()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.False(reader.NextResult());

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReader_GetSchemaTable_ThrowsNotSupportedException()
    {
        // Arrange
        var entities = new List<TestEntity>();
        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<TestEntity>(entities, metadata);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => reader.GetSchemaTable());

        reader.Dispose();
    }

    [Fact]
    public void EntityDataReaderCache_DifferentColumnLayouts_SameType_UsesGettersMatchingEachLayout()
    {
        // Regression test: EntityDataReaderCache<TEntity> used to build getters once
        // on the FIRST Initialize call and silently reuse them for every subsequent
        // call, even when a later call for the same TEntity specifies a different
        // column subset/order. That caused wrong values to be written to wrong
        // columns on a second bulk-insert of the same entity type with a different
        // layout.

        // Arrange: full layout (Id, Name, Price, CreatedAt), same as other tests in
        // this file — may or may not be the first EntityDataReader<TestEntity> built
        // in this test run, which is exactly the point: the fix must not depend on
        // call order.
        var fullMetadata = CreateTestMetadata();
        var fullEntities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Full", Price = 5.00m, CreatedAt = new DateTime(2024, 1, 1) }
        };
        var fullReader = new EntityDataReader<TestEntity>(fullEntities, fullMetadata);
        fullReader.Read();

        Assert.Equal(4, fullReader.FieldCount);
        Assert.Equal(1, fullReader.GetValue(0));
        Assert.Equal("Full", fullReader.GetValue(1));
        Assert.Equal(5.00m, fullReader.GetValue(2));
        Assert.Equal(new DateTime(2024, 1, 1), fullReader.GetValue(3));
        fullReader.Dispose();

        // Act: a different (narrower, reordered) layout for the SAME TestEntity type —
        // simulates a second bulk-insert specifying a different column subset/order.
        var partialMetadata = CreateReorderedSubsetMetadata();
        var partialEntities = new List<TestEntity>
        {
            new TestEntity { Id = 99, Name = "Partial", Price = 1.00m, CreatedAt = DateTime.UtcNow }
        };
        var partialReader = new EntityDataReader<TestEntity>(partialEntities, partialMetadata);
        partialReader.Read();

        // Assert: getters must match THIS layout (Name at ordinal 0, Id at ordinal 1)
        // — not stale getters bound to the full layout's ordinal order.
        Assert.Equal(2, partialReader.FieldCount);
        Assert.Equal("Partial", partialReader.GetValue(0));
        Assert.Equal(99, partialReader.GetValue(1));

        partialReader.Dispose();
    }

    // AUD-R25 (B3-4): GetOrdinal compared with "==" - ordinal, case-sensitive only.
    // IDataRecord.GetOrdinal is documented to try a case-sensitive lookup first and then fall back
    // to a case-insensitive one, which every ADO.NET provider reader implements, and every other
    // column-name lookup in Jaunty is deliberately case-insensitive. A caller resolving a
    // differently-cased name got IndexOutOfRangeException instead of the column. No in-tree caller
    // reaches it - the providers feed back names they got from GetName(i) - but this reader is
    // handed to third-party provider bulk-copy APIs whose lookup behaviour Jaunty does not control.

    [Theory]
    [InlineData("id")]
    [InlineData("ID")]
    [InlineData("nAmE")]
    public void EntityDataReader_GetOrdinal_FallsBackToACaseInsensitiveMatch(string name)
    {
        var entities = new List<TestEntity> { new() { Id = 1, Name = "A" } };
        using var reader = new EntityDataReader<TestEntity>(entities, CreateTestMetadata());

        int ordinal = reader.GetOrdinal(name);

        Assert.Equal(reader.GetOrdinal(reader.GetName(ordinal)), ordinal);
    }

    [Fact]
    public void EntityDataReader_GetOrdinal_ExactMatchStillWins()
    {
        // Two passes rather than one case-insensitive pass, so an exact match beats a
        // differently-cased one. Guards the ordering, not just the fallback.
        var entities = new List<TestEntity> { new() { Id = 1, Name = "A" } };
        using var reader = new EntityDataReader<TestEntity>(entities, CreateTestMetadata());

        Assert.Equal(0, reader.GetOrdinal("Id"));
        Assert.Equal(1, reader.GetOrdinal("Name"));
    }

    [Fact]
    public void EntityDataReader_GetOrdinal_StillThrowsForAGenuinelyAbsentColumn()
    {
        // The fallback must not turn a missing column into a silent wrong answer.
        var entities = new List<TestEntity> { new() { Id = 1, Name = "A" } };
        using var reader = new EntityDataReader<TestEntity>(entities, CreateTestMetadata());

        Assert.Throws<IndexOutOfRangeException>(() => reader.GetOrdinal("nonexistent"));
    }

    // AUD-R35-106 (round-35 batch 04a): GetValue memoises one (ordinal, value) slot per row so a
    // consumer using the standard IsDBNull-then-GetValue pattern invokes the compiled getter once
    // rather than twice. These pin what the memo must not break.

    private sealed class CountingEntity
    {
        public int Reads;

        private string? _name;

        public int Id { get; set; }

        public string? Name
        {
            get { Reads++; return _name; }
            set => _name = value;
        }
    }

    private static EntityMetadata CountingMetadata()
    {
        var idProp = typeof(CountingEntity).GetProperty(nameof(CountingEntity.Id))!;
        var nameProp = typeof(CountingEntity).GetProperty(nameof(CountingEntity.Name))!;

        return new EntityMetadata("counting", null,
        [
            new ColumnMetadata(idProp, idProp.Name, isPrimaryKey: true, databaseGeneratedOption: null),
            new ColumnMetadata(nameProp, nameProp.Name, isPrimaryKey: false, databaseGeneratedOption: null),
        ]);
    }

    [Fact]
    public void IsDBNullThenGetValue_InvokesTheGetterOnce()
    {
        var entity = new CountingEntity { Id = 1, Name = "kept" };
        using var reader = new EntityDataReader<CountingEntity>([entity], CountingMetadata());

        Assert.True(reader.Read());
        entity.Reads = 0;

        Assert.False(reader.IsDBNull(1));
        Assert.Equal("kept", reader.GetValue(1));

        Assert.Equal(1, entity.Reads);
    }

    [Fact]
    public void TheMemo_DoesNotLeakAcrossRows()
    {
        var entities = new List<CountingEntity>
        {
            new() { Id = 1, Name = "first" },
            new() { Id = 2, Name = null },
            new() { Id = 3, Name = "third" },
        };
        using var reader = new EntityDataReader<CountingEntity>(entities, CountingMetadata());

        var seen = new List<object>();
        while (reader.Read())
        {
            reader.IsDBNull(1);
            seen.Add(reader.GetValue(1));
        }

        Assert.Equal(["first", DBNull.Value, "third"], seen);
    }

    [Fact]
    public void TheMemo_DoesNotLeakAcrossOrdinals()
    {
        var entity = new CountingEntity { Id = 7, Name = "seven" };
        using var reader = new EntityDataReader<CountingEntity>([entity], CountingMetadata());

        Assert.True(reader.Read());

        Assert.Equal(7, reader.GetValue(0));
        Assert.Equal("seven", reader.GetValue(1));
        Assert.Equal(7, reader.GetValue(0));
    }

    [Fact]
    public void IsDBNull_StillReportsANullColumn()
    {
        var entity = new CountingEntity { Id = 1, Name = null };
        using var reader = new EntityDataReader<CountingEntity>([entity], CountingMetadata());

        Assert.True(reader.Read());

        Assert.True(reader.IsDBNull(1));
        Assert.False(reader.IsDBNull(0));
    }

    // AUD-R35-109/110/111/112 (round-35 batch 04a re-reports, first filed in rounds 9, 27 and 34).

    private sealed class TypedEntity
    {
        public bool Flag { get; set; }
        public byte Small { get; set; }
        public char Letter { get; set; }
        public DateTime When { get; set; }
        public decimal Money { get; set; }
        public double Big { get; set; }
        public float Middle { get; set; }
        public Guid Key { get; set; }
        public short Short { get; set; }
        public int Number { get; set; }
        public long Long { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private static EntityMetadata TypedMetadata() => new("typed", null,
    [
        .. typeof(TypedEntity).GetProperties()
            .Select(p => new ColumnMetadata(p, p.Name, isPrimaryKey: false, databaseGeneratedOption: null)),
    ]);

    private static readonly Guid SampleKey = new("11112222-3333-4444-5555-666677778888");
    private static readonly DateTime SampleWhen = new(2026, 8, 3, 4, 5, 6, DateTimeKind.Utc);

    private static TypedEntity Sample() => new()
    {
        Flag = true,
        Small = 7,
        Letter = 'j',
        When = SampleWhen,
        Money = 12.34m,
        Big = 1.5d,
        Middle = 2.5f,
        Key = SampleKey,
        Short = 9,
        Number = 42,
        Long = 43L,
        Text = "text",
    };

    private static int Ordinal(IDataReader reader, string name) => reader.GetOrdinal(name);

    [Fact]
    public void TheTypedAccessors_ReturnTheColumnValues()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());
        Assert.True(reader.Read());

        Assert.True(reader.GetBoolean(Ordinal(reader, nameof(TypedEntity.Flag))));
        Assert.Equal((byte)7, reader.GetByte(Ordinal(reader, nameof(TypedEntity.Small))));
        Assert.Equal('j', reader.GetChar(Ordinal(reader, nameof(TypedEntity.Letter))));
        Assert.Equal(SampleWhen, reader.GetDateTime(Ordinal(reader, nameof(TypedEntity.When))));
        Assert.Equal(12.34m, reader.GetDecimal(Ordinal(reader, nameof(TypedEntity.Money))));
        Assert.Equal(1.5d, reader.GetDouble(Ordinal(reader, nameof(TypedEntity.Big))));
        Assert.Equal(2.5f, reader.GetFloat(Ordinal(reader, nameof(TypedEntity.Middle))));
        Assert.Equal(SampleKey, reader.GetGuid(Ordinal(reader, nameof(TypedEntity.Key))));
        Assert.Equal((short)9, reader.GetInt16(Ordinal(reader, nameof(TypedEntity.Short))));
        Assert.Equal(42, reader.GetInt32(Ordinal(reader, nameof(TypedEntity.Number))));
        Assert.Equal(43L, reader.GetInt64(Ordinal(reader, nameof(TypedEntity.Long))));
        Assert.Equal("text", reader.GetString(Ordinal(reader, nameof(TypedEntity.Text))));
    }

    [Fact]
    public void GetDataTypeName_IsTheFieldTypeName()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());

        int ordinal = Ordinal(reader, nameof(TypedEntity.Number));

        Assert.Equal(typeof(int), reader.GetFieldType(ordinal));
        Assert.Equal("Int32", reader.GetDataTypeName(ordinal));
    }

    [Fact]
    public async Task TheProviderSpecificAndAsyncAccessors_MatchTheirSynchronousTwins()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());
        Assert.True(reader.Read());

        int ordinal = Ordinal(reader, nameof(TypedEntity.Number));

        Assert.Equal(42, reader.GetProviderSpecificValue(ordinal));
        Assert.False(await reader.IsDBNullAsync(ordinal, CancellationToken.None));

        var values = new object[reader.FieldCount];
        Assert.Equal(reader.FieldCount, reader.GetProviderSpecificValues(values));
        Assert.Equal(42, values[ordinal]);
    }

    [Fact]
    public void TheUnsupportedMembers_Throw()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());
        Assert.True(reader.Read());

        Assert.Throws<NotSupportedException>(() => reader.GetBytes(0, 0, null, 0, 0));
        Assert.Throws<NotSupportedException>(() => reader.GetChars(0, 0, null, 0, 0));
        Assert.Throws<NotSupportedException>(() => ((IDataRecord)reader).GetData(0));
        Assert.Throws<NotSupportedException>(reader.GetSchemaTable);
        Assert.False(reader.NextResult());
    }

    // AUD-R35-109, first filed round 9: Close() was an empty body while IsClosed reported _disposed,
    // so the reader claimed to be open after Close and never disposed its enumerator.
    [Fact]
    public void Close_ClosesTheReader()
    {
        var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());

        Assert.False(reader.IsClosed);

        reader.Close();

        Assert.True(reader.IsClosed);

        // Idempotent, as IDataReader.Close is specified to be - and as Dispose already was.
        reader.Close();
        reader.Dispose();
        Assert.True(reader.IsClosed);
    }

    // AUD-R35-110, first filed round 27: GetValues wrote FieldCount elements whatever the array's
    // length, so a short array raised IndexOutOfRangeException instead of copying what fits.
    [Fact]
    public void GetValues_WithAShortArray_CopiesWhatFitsAndReturnsTheCount()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());
        Assert.True(reader.Read());

        var values = new object[2];

        Assert.Equal(2, reader.GetValues(values));
        Assert.All(values, Assert.NotNull);
    }

    [Fact]
    public void GetValues_WithALongArray_FillsOnlyTheColumns()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());
        Assert.True(reader.Read());

        var values = new object[reader.FieldCount + 3];

        Assert.Equal(reader.FieldCount, reader.GetValues(values));
        Assert.Null(values[reader.FieldCount]);
    }

    [Fact]
    public void GetValues_WithANullArray_ThrowsArgumentNullException()
    {
        using var reader = new EntityDataReader<TypedEntity>([Sample()], TypedMetadata());
        Assert.True(reader.Read());

        Assert.Throws<ArgumentNullException>(() => reader.GetValues(null!));
    }

    private static EntityMetadata CreateReorderedSubsetMetadata()
    {
        var nameProp = typeof(TestEntity).GetProperty(nameof(TestEntity.Name))!;
        var idProp = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;

        var columns = new List<ColumnMetadata>
        {
            new ColumnMetadata(nameProp, nameProp.Name, isPrimaryKey: false, databaseGeneratedOption: null),
            new ColumnMetadata(idProp, idProp.Name, isPrimaryKey: true, databaseGeneratedOption: null),
        };

        return new EntityMetadata("TestEntities", null, columns);
    }

    private static EntityMetadata CreateTestMetadata()
    {
        // Create mock metadata for TestEntity
        var propInfos = typeof(TestEntity).GetProperties();
        var columns = new List<ColumnMetadata>();

        foreach (var prop in propInfos)
        {
            columns.Add(new ColumnMetadata(
                prop,
                prop.Name,
                isPrimaryKey: prop.Name == "Id",
                databaseGeneratedOption: null));
        }

        return new EntityMetadata(
            "TestEntities",
            null,
            columns);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}