using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Regression tests for async query transaction handling against a real <see cref="System.Data.Common.DbConnection"/>.
/// The async command path is built via <see cref="System.Data.Common.DbCommand"/>, whose
/// <see cref="System.Data.Common.DbCommand.Transaction"/> setter only accepts <see cref="System.Data.Common.DbTransaction"/>.
/// A non-<see cref="System.Data.Common.DbTransaction"/> <see cref="IDbTransaction"/> must be rejected with a clear
/// exception (via <c>AsyncTransactionValidator.RequireDbTransaction</c>), never silently dropped (which would run
/// the command outside the caller's transaction).
/// </summary>
public class QueryAsyncTransactionTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public QueryAsyncTransactionTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        SeedSchema();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    private void SeedSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );
            INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks');";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task QueryScalarAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var count = await _connection.QueryScalarAsync(
            "SELECT COUNT(*) FROM categories",
            CommandOptions<long>.WithTransaction(transaction));

        Assert.Equal(1L, count);
        transaction.Rollback();
    }

    [Fact]
    public async Task QueryScalarAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.QueryScalarAsync(
                "SELECT COUNT(*) FROM categories",
                CommandOptions<long>.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task QueryStreamAsync_WithRealDbTransaction_YieldsResultsWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var count = 0;
        await foreach (var category in _connection.QueryStreamAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            new CommandOptions<Category>(transaction: transaction)))
        {
            Assert.NotNull(category.CategoryName);
            count++;
        }

        Assert.Equal(1, count);
        transaction.Rollback();
    }

    [Fact]
    public async Task QueryStreamAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        async Task Act()
        {
            await foreach (var _ in _connection.QueryStreamAsync<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
                new CommandOptions<Category>(transaction: nonDbTransaction)))
            {
            }
        }

        var ex = await Assert.ThrowsAsync<ArgumentException>(Act);
        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }
}
