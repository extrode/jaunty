using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests that Bulk* write methods reject a collection containing a null entity with a clear
/// ArgumentException instead of letting it reach the reflection-based parameter binder unchecked.
/// </summary>
[Collection("Write Operations")]
public class BulkNullEntityTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public BulkNullEntityTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void BulkInsert_CollectionContainsNull_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetWriteContext(dialect);
        var entities = new List<BulkTestEntity> { new BulkTestEntity { Name = "A", Value = 1 }, null! };

        Assert.Throws<ArgumentException>(() => connection.Connection.BulkInsert(entities));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void BulkUpdate_CollectionContainsNull_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetWriteContext(dialect);
        var entities = new List<BulkTestEntity> { new BulkTestEntity { Id = 1, Name = "A", Value = 1 }, null! };

        Assert.Throws<ArgumentException>(() => connection.Connection.BulkUpdate(entities));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void BulkDelete_CollectionContainsNull_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetWriteContext(dialect);
        var entities = new List<BulkTestEntity> { new BulkTestEntity { Id = 1, Name = "A", Value = 1 }, null! };

        Assert.Throws<ArgumentException>(() => connection.Connection.BulkDelete(entities));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task BulkInsertAsync_CollectionContainsNull_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetWriteContext(dialect);
        var entities = new List<BulkTestEntity> { new BulkTestEntity { Name = "A", Value = 1 }, null! };

        await Assert.ThrowsAsync<ArgumentException>(() => connection.Connection.BulkInsertAsync(entities).AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task BulkUpdateAsync_CollectionContainsNull_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetWriteContext(dialect);
        var entities = new List<BulkTestEntity> { new BulkTestEntity { Id = 1, Name = "A", Value = 1 }, null! };

        await Assert.ThrowsAsync<ArgumentException>(() => connection.Connection.BulkUpdateAsync(entities).AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task BulkDeleteAsync_CollectionContainsNull_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetWriteContext(dialect);
        var entities = new List<BulkTestEntity> { new BulkTestEntity { Id = 1, Name = "A", Value = 1 }, null! };

        await Assert.ThrowsAsync<ArgumentException>(() => connection.Connection.BulkDeleteAsync(entities).AsTask());
    }
}
