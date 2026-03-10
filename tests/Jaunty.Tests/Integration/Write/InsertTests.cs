using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;
using System.Threading; // Added for CancellationToken
using System; // Added for ArgumentNullException

namespace Jaunty.Tests.Integration.Write;

[Collection("Write Operations")]
public class InsertTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public InsertTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static int GetRowCount(System.Data.IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_SingleEntity_ReturnsIdentity(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = ctx.Connection.Insert(entity);

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_MultipleEntities_ReturnsIncrementingIds(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity1 = new BulkTestEntity { Name = "Test1", Value = 100 };
        var entity2 = new BulkTestEntity { Name = "Test2", Value = 200 };

        long id1 = ctx.Connection.Insert(entity1);
        long id2 = ctx.Connection.Insert(entity2);

        Assert.True(id1 > 0);
        Assert.True(id2 > id1);
        Assert.Equal(2, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        using var transaction = ctx.Connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        long id = ctx.Connection.Insert(entity, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_WithTransaction_RollbackDiscardsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        using var transaction = ctx.Connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test1", Value = 100 };

        ctx.Connection.Insert(entity, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        Assert.Equal(0, GetRowCount(ctx.Connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_IEntityNonGeneric_SetsIdAfterInsert(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = new IEntityTestEntity { Name = "TestIEntity", Value = 42 };

        long id = ctx.Connection.Insert(entity);

        Assert.True(id > 0);
        Assert.Equal(id, entity.Id);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_ExplicitlyProvidedId_IsIgnored(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var entity = new BulkTestEntity { Id = 999, Name = "ProvidedIdTest", Value = 123 };

        long id = ctx.Connection.Insert(entity);

        // Assert that the returned ID is NOT the provided ID (database should generate its own)
        Assert.NotEqual(999, id);
        Assert.True(id > 0); // And it should be a valid, generated ID
        Assert.Equal(1, GetRowCount(ctx.Connection));

        // Verify the inserted entity has the generated ID
        var insertedEntity = ctx.Connection.QueryFirst<BulkTestEntity>(
            "SELECT id AS Id, name AS Name, value AS Value FROM bulk_test WHERE id = @Id",
            new { Id = id });

        Assert.Equal(id, insertedEntity.Id);
        Assert.Equal("ProvidedIdTest", insertedEntity.Name);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        Assert.Throws<ArgumentNullException>(() => ctx.Connection.Insert<BulkTestEntity>(null));
    }
}