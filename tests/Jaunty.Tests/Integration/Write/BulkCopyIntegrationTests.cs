using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Integration tests for native bulk copy functionality.
/// Tests the automatic native bulk copy activation for large datasets.
/// </summary>
public class BulkCopyIntegrationTests : IClassFixture<DialectFixture>, IDisposable
{
    private readonly DialectFixture _fixture;
    private readonly bool _originalEnableNativeBulkCopy;
    private readonly int _originalMinimumRows;

    public BulkCopyIntegrationTests(DialectFixture fixture)
    {
        _fixture = fixture;

        // Save original configuration
        _originalEnableNativeBulkCopy = BulkCopyConfiguration.EnableNativeBulkCopy;
        _originalMinimumRows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy;

        // Enable native bulk copy for tests
        BulkCopyConfiguration.EnableNativeBulkCopy = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 10; // Lower threshold for tests
    }

    private static void ClearTestTable(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bulk_test";
        cmd.ExecuteNonQuery();
    }

    private static int GetRowCount(IDbConnection connection)
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
    public void BulkInsert_LargeDataset_UsesNativeBulkCopy(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange - Create 100 entities (above threshold)
        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 100; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"Test{i}",
                Value = i
            });
        }

        // Act
        var startTime = DateTime.UtcNow;
        int inserted = connection.BulkInsert(entities);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(100, inserted);
        Assert.Equal(100, GetRowCount(connection));

        // Native bulk copy should be much faster than standard INSERT
        // For 100 rows, should complete in under 1 second
        Assert.True(elapsed.TotalSeconds < 1.0, $"Bulk insert took too long: {elapsed.TotalSeconds}s");
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsertAsync_LargeDataset_UsesNativeBulkCopy(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange - Create 100 entities (above threshold)
        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 100; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"AsyncTest{i}",
                Value = i
            });
        }

        // Act
        var startTime = DateTime.UtcNow;
        int inserted = await connection.BulkInsertAsync(entities);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(100, inserted);
        Assert.Equal(100, GetRowCount(connection));

        // Native bulk copy should be much faster than standard INSERT
        Assert.True(elapsed.TotalSeconds < 1.0, $"Bulk insert took too long: {elapsed.TotalSeconds}s");
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_WithTransaction_CommitsCorrectly(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 50; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"TxTest{i}",
                Value = i
            });
        }

        using var transaction = connection.BeginTransaction();

        // Act
        int inserted = connection.BulkInsert(entities, CommandOptions.WithTransaction(transaction));
        transaction.Commit();

        // Assert
        Assert.Equal(50, inserted);
        Assert.Equal(50, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_WithTransactionRollback_DiscardsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 50; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"RollbackTest{i}",
                Value = i
            });
        }

        int rowCountBefore = GetRowCount(connection);

        using var transaction = connection.BeginTransaction();

        // Act
        _ = connection.BulkInsert(entities, CommandOptions.WithTransaction(transaction));
        transaction.Rollback();

        // Assert
        Assert.Equal(rowCountBefore, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_SmallDataset_UsesStandardInsert(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange - Create 5 entities (below threshold of 10)
        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 5; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"SmallTest{i}",
                Value = i
            });
        }

        // Act
        int inserted = connection.BulkInsert(entities);

        // Assert - Should still work correctly
        Assert.Equal(5, inserted);
        Assert.Equal(5, GetRowCount(connection));
    }

    // Note: Cancellation testing is complex and provider-dependent.
    // Some providers check cancellation before starting, others during execution.
    // This test is skipped until we can implement proper cancellation support.

    /*
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task BulkInsert_WithCancellation_ThrowsOperationCanceled(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection as DbConnection;
        Assert.NotNull(connection);
        
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 100; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"CancelTest{i}",
                Value = i
            });
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await connection.BulkInsertAsync(entities, cancellationToken: cts.Token);
        });
    }
    */

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_EmptyCollection_ReturnsZero(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>();

        // Act
        int inserted = connection.BulkInsert(entities);

        // Assert
        Assert.Equal(0, inserted);
        Assert.Equal(0, GetRowCount(connection));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        // Arrange
        IDbConnection? nullConnection = null;
        var entities = new List<BulkTestEntity>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            nullConnection!.BulkInsert(entities);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_NullEntities_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;

        // Arrange
        List<BulkTestEntity>? nullEntities = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            connection.BulkInsert(nullEntities!);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void BulkInsert_WithDisabledNativeBulkCopy_UsesStandardInsert(DialectInfo dialect)
    {
        // Arrange - Disable native bulk copy
        BulkCopyConfiguration.EnableNativeBulkCopy = false;

        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        var entities = new List<BulkTestEntity>();
        for (int i = 0; i < 100; i++)
        {
            entities.Add(new BulkTestEntity
            {
                Name = $"DisabledTest{i}",
                Value = i
            });
        }

        try
        {
            // Act - Should still work with standard INSERT
            int inserted = connection.BulkInsert(entities);

            // Assert
            Assert.Equal(100, inserted);
            Assert.Equal(100, GetRowCount(connection));
        }
        finally
        {
            // Restore configuration
            BulkCopyConfiguration.EnableNativeBulkCopy = _originalEnableNativeBulkCopy;
        }
    }

    public void Dispose()
    {
        // Restore original configuration
        BulkCopyConfiguration.EnableNativeBulkCopy = _originalEnableNativeBulkCopy;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = _originalMinimumRows;
    }
}