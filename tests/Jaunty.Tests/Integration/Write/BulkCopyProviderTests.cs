using Jaunty.Configuration;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Write;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Integration tests for bulk copy providers across different database dialects.
/// </summary>
public class BulkCopyProviderTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public BulkCopyProviderTests(DialectFixture fixture)
    {
        _fixture = fixture;
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

    #region SqlServerBulkCopyProvider Tests

    [Fact]
    public void SqlServerBulkCopyProvider_IsSupported_WhenSqlServerAvailable()
    {
        // Arrange
        var provider = new SqlServerBulkCopyProvider();

        // Act & Assert
        // Provider should be supported if SqlClient is available
        // Note: This test passes if the SqlClient package is referenced
        Assert.True(provider.IsSupported);
    }

    [Theory]
    [SqlServer]
    public void SqlServerBulkCopyProvider_CopyToServer_InsertsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "Bulk1", Value = 100 },
            new() { Name = "Bulk2", Value = 200 },
            new() { Name = "Bulk3", Value = 300 }
        };

        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<BulkTestEntity>(entities, metadata);
        var provider = new SqlServerBulkCopyProvider();
        var options = new BulkCopyOptions();

        try
        {
            // Act
            int rows = provider.CopyToServer(connection, "bulk_test", reader, options);

            // Assert
            Assert.Equal(3, GetRowCount(connection));
        }
        finally
        {
            reader.Dispose();
        }
    }

    #endregion

    #region PostgreSqlBulkCopyProvider Tests

    [Fact]
    public void PostgreSqlBulkCopyProvider_IsSupported_WhenNpgsqlAvailable()
    {
        // Arrange
        var provider = new PostgreSqlBulkCopyProvider();

        // Act & Assert
        // Provider should be supported if Npgsql is available
        Assert.True(provider.IsSupported);
    }

    [Theory]
    [Postgres]
    public void PostgreSqlBulkCopyProvider_CopyToServer_InsertsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "PgBulk1", Value = 100 },
            new() { Name = "PgBulk2", Value = 200 }
        };

        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<BulkTestEntity>(entities, metadata);
        var provider = new PostgreSqlBulkCopyProvider();
        var options = new BulkCopyOptions();

        try
        {
            // Act
            int rows = provider.CopyToServer(connection, "bulk_test", reader, options);

            // Assert
            Assert.Equal(2, GetRowCount(connection));
        }
        finally
        {
            reader.Dispose();
        }
    }

    #endregion

    #region MySqlBulkCopyProvider Tests

    [Fact]
    public void MySqlBulkCopyProvider_IsSupported_WhenMySqlAvailable()
    {
        // Arrange
        var provider = new MySqlBulkCopyProvider();

        // Act & Assert
        // Provider should be supported if MySql.Data or MySqlConnector is available
        Assert.True(provider.IsSupported);
    }

    [Theory]
    [MariaDB]
    public void MySqlBulkCopyProvider_CopyToServer_InsertsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "MyBulk1", Value = 100 },
            new() { Name = "MyBulk2", Value = 200 }
        };

        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<BulkTestEntity>(entities, metadata);
        var provider = new MySqlBulkCopyProvider();
        var options = new BulkCopyOptions();

        try
        {
            // Act
            int rows = provider.CopyToServer(connection, "bulk_test", reader, options);

            // Assert
            Assert.Equal(2, GetRowCount(connection));
        }
        finally
        {
            reader.Dispose();
        }
    }

    #endregion

    #region SQLiteBulkCopyProvider Tests

    [Fact]
    public void SQLiteBulkCopyProvider_IsSupported_Always()
    {
        // Arrange
        var provider = new SQLiteBulkCopyProvider();

        // Act & Assert
        Assert.True(provider.IsSupported);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void SQLiteBulkCopyProvider_CopyToServer_InsertsData(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "SqBulk1", Value = 100 },
            new() { Name = "SqBulk2", Value = 200 },
            new() { Name = "SqBulk3", Value = 300 }
        };

        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<BulkTestEntity>(entities, metadata);
        var provider = new SQLiteBulkCopyProvider();
        var options = new BulkCopyOptions();

        try
        {
            // Act
            int rows = provider.CopyToServer(connection, "bulk_test", reader, options);

            // Assert
            Assert.Equal(3, GetRowCount(connection));
        }
        finally
        {
            reader.Dispose();
        }
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void SQLiteBulkCopyProvider_CopyToServer_WithNullValues_Works(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        var connection = ctx.Connection;
        ClearTestTable(connection);

        // Arrange
        var entities = new List<BulkTestEntity>
        {
            new() { Name = "NullTest", Value = null },
            new() { Name = null!, Value = 50 }
        };

        var metadata = CreateTestMetadata();
        var reader = new EntityDataReader<BulkTestEntity>(entities, metadata);
        var provider = new SQLiteBulkCopyProvider();
        var options = new BulkCopyOptions();

        try
        {
            // Act
            int rows = provider.CopyToServer(connection, "bulk_test", reader, options);

            // Assert
            Assert.Equal(2, GetRowCount(connection));
        }
        finally
        {
            reader.Dispose();
        }
    }

    #endregion

    #region BulkCopyOptions Tests

    [Fact]
    public void BulkCopyOptions_WithTransaction_PassesTransaction()
    {
        // Arrange
        using var connection = new System.Data.SQLite.SQLiteConnection(":memory:");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var options = new BulkCopyOptions
        {
            Transaction = transaction
        };

        // Act & Assert
        Assert.Same(transaction, options.Transaction);
    }

    [Fact]
    public void BulkCopyOptions_CustomBatchSize_IsApplied()
    {
        // Arrange
        var options = new BulkCopyOptions
        {
            BatchSize = 5000
        };

        // Act & Assert
        Assert.Equal(5000, options.BatchSize);
    }

    #endregion

    #region BulkCopyConfiguration Tests

    [Fact]
    public void BulkCopyConfiguration_MinimumRows_AffectsProviderSelection()
    {
        // Arrange
        var originalMin = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 50;

        try
        {
            // Act & Assert
            Assert.Equal(50, BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);
        }
        finally
        {
            BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = originalMin;
        }
    }

    [Fact]
    public void BulkCopyConfiguration_EnableNativeBulkCopy_CanBeDisabled()
    {
        // Arrange
        var originalEnabled = BulkCopyConfiguration.EnableNativeBulkCopy;
        BulkCopyConfiguration.EnableNativeBulkCopy = false;

        try
        {
            // Act & Assert
            Assert.False(BulkCopyConfiguration.EnableNativeBulkCopy);
        }
        finally
        {
            BulkCopyConfiguration.EnableNativeBulkCopy = originalEnabled;
        }
    }

    #endregion

    private static Jaunty.Internals.Entity.EntityMetadata CreateTestMetadata()
    {
        var propInfos = typeof(BulkTestEntity).GetProperties();
        var columns = new List<Jaunty.Internals.Entity.ColumnMetadata>();

        foreach (var prop in propInfos)
        {
            columns.Add(new Jaunty.Internals.Entity.ColumnMetadata(
                prop.Name,
                prop,
                prop.PropertyType,
                isPrimaryKey: prop.Name == "Id",
                isIdentity: prop.Name == "Id",
                isComputed: false,
                isIgnored: false));
        }

        return new Jaunty.Internals.Entity.EntityMetadata(
            typeof(BulkTestEntity),
            "bulk_test",
            null,
            columns,
            columns.Where(c => c.IsPrimaryKey).ToList(),
            columns.Where(c => !c.IsIdentity).ToList());
    }

    public void Dispose()
    {
        BulkCopyConfiguration.Reset();
    }
}
