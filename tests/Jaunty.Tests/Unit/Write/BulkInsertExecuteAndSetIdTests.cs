using System.Data.Common;
using Jaunty.Tests.Helpers;
using Microsoft.Data.Sqlite;

using static global::Jaunty.Jaunty;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// Unit tests for the internal per-row insert helper used by BulkInsert/BulkInsertAsync, covering
/// the count-contribution bug where a suppressed identity insert (e.g. an INSTEAD OF trigger,
/// where ExecuteScalar() returns null/DBNull) used to still be counted as 1 row inserted.
/// </summary>
public class BulkInsertExecuteAndSetIdTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
    }

    [Fact]
    public void ExecuteInsertAndSetId_NullScalarResult_ReturnsZeroAndDoesNotSetId()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        inner.Open();
        using var connection = new ThrowingDbConnection(inner) { OnExecute = _ => null };
        using DbCommand command = connection.CreateCommand();

        var entity = new Entity { Id = 0 };
        int contribution = ExecuteInsertAndSetId(command, entity, (e, id) => e.Id = id);

        Assert.Equal(0, contribution);
        Assert.Equal(0, entity.Id);
    }

    [Fact]
    public void ExecuteInsertAndSetId_PositiveScalarResult_ReturnsOneAndSetsId()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        inner.Open();
        using var connection = new ThrowingDbConnection(inner) { OnExecute = _ => 42L };
        using DbCommand command = connection.CreateCommand();

        var entity = new Entity { Id = 0 };
        int contribution = ExecuteInsertAndSetId(command, entity, (e, id) => e.Id = id);

        Assert.Equal(1, contribution);
        Assert.Equal(42, entity.Id);
    }

    [Fact]
    public async Task ExecuteInsertAndSetIdAsync_NullScalarResult_ReturnsZeroAndDoesNotSetId()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        inner.Open();
        using var connection = new ThrowingDbConnection(inner) { OnExecute = _ => null };
        using DbCommand command = connection.CreateCommand();

        var entity = new Entity { Id = 0 };
        int contribution = await ExecuteInsertAndSetIdAsync(command, entity, (e, id) => e.Id = id, CancellationToken.None);

        Assert.Equal(0, contribution);
        Assert.Equal(0, entity.Id);
    }

    [Fact]
    public async Task ExecuteInsertAndSetIdAsync_PositiveScalarResult_ReturnsOneAndSetsId()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        inner.Open();
        using var connection = new ThrowingDbConnection(inner) { OnExecute = _ => 7L };
        using DbCommand command = connection.CreateCommand();

        var entity = new Entity { Id = 0 };
        int contribution = await ExecuteInsertAndSetIdAsync(command, entity, (e, id) => e.Id = id, CancellationToken.None);

        Assert.Equal(1, contribution);
        Assert.Equal(7, entity.Id);
    }
}
