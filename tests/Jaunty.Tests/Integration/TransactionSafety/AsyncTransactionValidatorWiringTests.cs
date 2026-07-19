using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.TransactionSafety;

/// <summary>
/// Regression tests confirming that <c>AsyncTransactionValidator.RequireDbTransaction</c> is wired into
/// every async CRUD core method (Get, GetAll, Insert, Update, Delete, Execute). Each of these methods
/// builds its command via <see cref="System.Data.Common.DbCommand"/>, whose
/// <see cref="System.Data.Common.DbCommand.Transaction"/> setter only accepts
/// <see cref="System.Data.Common.DbTransaction"/>. A non-<see cref="System.Data.Common.DbTransaction"/>
/// <see cref="IDbTransaction"/> must be rejected with a clear <see cref="ArgumentException"/>, never
/// silently dropped (which would run the command outside the caller's transaction).
/// </summary>
public class AsyncTransactionValidatorWiringTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public AsyncTransactionValidatorWiringTests()
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
    public async Task GetAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        Category? category = await _connection.GetAsync<Category>(1, CommandOptions<Category>.WithTransaction(transaction));

        Assert.NotNull(category);
        transaction.Rollback();
    }

    [Fact]
    public async Task GetAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.GetAsync<Category>(1, CommandOptions<Category>.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task GetAllAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        List<Category> categories = await _connection.GetAllAsync<Category>(CommandOptions<Category>.WithTransaction(transaction));

        Assert.Single(categories);
        transaction.Rollback();
    }

    [Fact]
    public async Task GetAllAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.GetAllAsync<Category>(CommandOptions<Category>.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task InsertAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var category = new Category { CategoryName = "Condiments", Description = "Sauces and spices" };
        long id = await _connection.InsertAsync(category, CommandOptions.WithTransaction(transaction));

        Assert.True(id > 0);
        transaction.Rollback();
    }

    [Fact]
    public async Task InsertAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var category = new Category { CategoryName = "Condiments", Description = "Sauces and spices" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.InsertAsync(category, CommandOptions.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task UpdateAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var category = new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Updated" };
        int rows = await _connection.UpdateAsync(category, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public async Task UpdateAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var category = new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Updated" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.UpdateAsync(category, CommandOptions.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task DeleteAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var category = new Category { CategoryId = 1, CategoryName = "Beverages" };
        int rows = await _connection.DeleteAsync(category, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public async Task DeleteAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var category = new Category { CategoryId = 1, CategoryName = "Beverages" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.DeleteAsync(category, CommandOptions.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task ExecuteAsync_WithRealDbTransactionOrNull_ExecutesSuccessfully()
    {
        using var transaction = _connection.BeginTransaction();

        int rows = await _connection.ExecuteAsync(
            "UPDATE categories SET description = 'Within transaction' WHERE category_id = 1",
            CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();

        int rowsNoTransaction = await _connection.ExecuteAsync(
            "UPDATE categories SET description = 'No transaction' WHERE category_id = 1");

        Assert.Equal(1, rowsNoTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfSilentlyDroppingTransaction()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.ExecuteAsync(
                "UPDATE categories SET description = 'Should not apply' WHERE category_id = 1",
                CommandOptions.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }
}
