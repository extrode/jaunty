using Jaunty.Configuration;

namespace Jaunty.Tests.Unit.Internals.BulkCopy;

/// <summary>
/// Unit tests for BulkCopyOptions and BulkCopyConfiguration.
/// </summary>
public class BulkCopyConfigurationTests : IDisposable
{
    [Fact]
    public void BulkCopyOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new BulkCopyOptions();

        // Assert
        Assert.Equal(10000, options.BatchSize);
        Assert.Equal(30, options.Timeout);
        Assert.Equal(BulkCopyIdentityMode.Default, options.IdentityMode);
        Assert.False(options.CheckConstraints);
        Assert.Equal(TableLockOption.Default, options.TableLock);
        Assert.True(options.EnableStreaming);
        Assert.Null(options.Transaction);
    }

    [Fact]
    public void BulkCopyOptions_CanSetAllProperties()
    {
        // Arrange
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var options = new BulkCopyOptions();

        // Act
        options.BatchSize = 5000;
        options.Timeout = 60;
        options.IdentityMode = BulkCopyIdentityMode.KeepIdentity;
        options.CheckConstraints = true;
        options.TableLock = TableLockOption.BulkLock;
        options.EnableStreaming = false;
        options.Transaction = transaction;

        // Assert
        Assert.Equal(5000, options.BatchSize);
        Assert.Equal(60, options.Timeout);
        Assert.Equal(BulkCopyIdentityMode.KeepIdentity, options.IdentityMode);
        Assert.True(options.CheckConstraints);
        Assert.Equal(TableLockOption.BulkLock, options.TableLock);
        Assert.False(options.EnableStreaming);
        Assert.Same(transaction, options.Transaction);
    }

    [Fact]
    public void BulkCopyConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act - defaults should already be set

        // Assert
        Assert.Equal(10000, BulkCopyConfiguration.DefaultBatchSize);
        Assert.Equal(30, BulkCopyConfiguration.DefaultTimeout);
        Assert.Equal(BulkCopyIdentityMode.Default, BulkCopyConfiguration.DefaultIdentityMode);
        Assert.False(BulkCopyConfiguration.DefaultCheckConstraints);
        Assert.Equal(100, BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);
        Assert.True(BulkCopyConfiguration.EnableNativeBulkCopy);
    }

    [Fact]
    public void BulkCopyConfiguration_CanModifyGlobalSettings()
    {
        // Arrange
        var originalBatchSize = BulkCopyConfiguration.DefaultBatchSize;

        try
        {
            // Act
            BulkCopyConfiguration.DefaultBatchSize = 5000;
            BulkCopyConfiguration.DefaultTimeout = 60;
            BulkCopyConfiguration.DefaultIdentityMode = BulkCopyIdentityMode.KeepIdentity;
            BulkCopyConfiguration.DefaultCheckConstraints = true;
            BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 50;
            BulkCopyConfiguration.EnableNativeBulkCopy = false;

            // Assert
            Assert.Equal(5000, BulkCopyConfiguration.DefaultBatchSize);
            Assert.Equal(60, BulkCopyConfiguration.DefaultTimeout);
            Assert.Equal(BulkCopyIdentityMode.KeepIdentity, BulkCopyConfiguration.DefaultIdentityMode);
            Assert.True(BulkCopyConfiguration.DefaultCheckConstraints);
            Assert.Equal(50, BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);
            Assert.False(BulkCopyConfiguration.EnableNativeBulkCopy);
        }
        finally
        {
            // Cleanup
            BulkCopyConfiguration.DefaultBatchSize = originalBatchSize;
        }
    }

    [Fact]
    public void BulkCopyConfiguration_Reset_RestoresDefaults()
    {
        // Arrange
        BulkCopyConfiguration.DefaultBatchSize = 5000;
        BulkCopyConfiguration.DefaultTimeout = 60;
        BulkCopyConfiguration.DefaultIdentityMode = BulkCopyIdentityMode.KeepIdentity;
        BulkCopyConfiguration.DefaultCheckConstraints = true;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 50;
        BulkCopyConfiguration.EnableNativeBulkCopy = false;

        // Act
        BulkCopyConfiguration.Reset();

        // Assert
        Assert.Equal(10000, BulkCopyConfiguration.DefaultBatchSize);
        Assert.Equal(30, BulkCopyConfiguration.DefaultTimeout);
        Assert.Equal(BulkCopyIdentityMode.Default, BulkCopyConfiguration.DefaultIdentityMode);
        Assert.False(BulkCopyConfiguration.DefaultCheckConstraints);
        Assert.Equal(100, BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);
        Assert.True(BulkCopyConfiguration.EnableNativeBulkCopy);
    }

    [Fact]
    public void TableLockEnum_HasExpectedValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)TableLockOption.Default);
        Assert.Equal(1, (int)TableLockOption.BulkLock);
        Assert.Equal(2, (int)TableLockOption.NoLock);
    }

    [Fact]
    public void BulkCopyIdentityModeEnum_HasExpectedValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)BulkCopyIdentityMode.Default);
        Assert.Equal(1, (int)BulkCopyIdentityMode.KeepIdentity);
        Assert.Equal(2, (int)BulkCopyIdentityMode.AutoGenerate);
    }

    public void Dispose()
    {
        // Reset configuration after each test
        BulkCopyConfiguration.Reset();
    }
}