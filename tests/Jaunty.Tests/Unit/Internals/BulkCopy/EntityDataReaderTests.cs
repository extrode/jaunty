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