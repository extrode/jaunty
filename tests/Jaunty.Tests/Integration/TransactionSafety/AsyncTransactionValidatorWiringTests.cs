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

    // AUD-R6: GetByIdSimpleCoreDirect assigned options.Transaction to the shared IDbCommand
    // before branching on "connection is DbConnection" - so with a real DbConnection (making the
    // command a genuine DbCommand at runtime) and a non-DbTransaction, the plain assignment threw
    // an opaque InvalidCastException instead of routing through AsyncTransactionValidator like its
    // sibling GetAllCoreDirect/ExecuteReaderDirect already did.
    [Fact]
    public void Get_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        Category? category = _connection.Get<Category>(1, CommandOptions<Category>.WithTransaction(transaction));

        Assert.NotNull(category);
        transaction.Rollback();
    }

    [Fact]
    public void Get_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.Get<Category>(1, CommandOptions<Category>.WithTransaction(nonDbTransaction)));

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

    // AUD-R11: the sync *CoreDirect methods below assigned options.Transaction to the shared
    // IDbCommand unconditionally, without branching on "connection is DbConnection" like
    // GetByIdSimpleCoreDirect (AUD-R6) already did. Round 11's batch-3 audit caught
    // Delete/Insert/Update/ExecuteNonQueryCore; this sweep found the same unguarded pattern in
    // Upsert, ExecuteBatch, QueryScalarCore, ExecuteQueryMultipleDirect, and QueryCoreListDirect.

    [Fact]
    public void Upsert_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var category = new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Upserted" };
        int rows = _connection.Upsert(category, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public void Upsert_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var category = new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Upserted" };

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.Upsert(category, CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public void ExecuteBatch_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        int rows = _connection.ExecuteBatch(
            "UPDATE categories SET description = @Description WHERE category_id = @CategoryId",
            new[] { new { CategoryId = 1, Description = "Batched" } },
            CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public void ExecuteBatch_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.ExecuteBatch(
                "UPDATE categories SET description = @Description WHERE category_id = @CategoryId",
                new[] { new { CategoryId = 1, Description = "Batched" } },
                CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public void ExecuteScalar_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        long count = _connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM categories",
            CommandOptions<long>.WithTransaction(transaction));

        Assert.Equal(1, count);
        transaction.Rollback();
    }

    [Fact]
    public void ExecuteScalar_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM categories",
                CommandOptions<long>.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public void QueryMultiple_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        using GridReader reader = _connection.QueryMultiple(
            "SELECT * FROM categories",
            CommandOptions.WithTransaction(transaction));
        List<Category> categories = reader.Read<Category>();

        Assert.Single(categories);
        transaction.Rollback();
    }

    [Fact]
    public void QueryMultiple_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.QueryMultiple("SELECT * FROM categories", CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public void QueryPartialList_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        List<IDictionary<string, object?>> rows = _connection.QueryPartialList(
            "SELECT * FROM categories",
            CommandOptions.WithTransaction(transaction));

        Assert.Single(rows);
        transaction.Rollback();
    }

    [Fact]
    public void QueryPartialList_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.QueryPartialList("SELECT * FROM categories", CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    // R12 batch-2: BulkInsert/BulkUpdate/BulkDelete assigned options.Transaction to command.Transaction
    // unconditionally, without the "connection is DbConnection" guard used by every sibling write path.

    [Fact]
    public void BulkInsert_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var categories = new[] { new Category { CategoryName = "Condiments" } };
        int rows = _connection.BulkInsert(categories, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public void BulkInsert_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var categories = new[] { new Category { CategoryName = "Condiments" } };

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.BulkInsert(categories, CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public void BulkUpdate_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Bulk updated" } };
        int rows = _connection.BulkUpdate(categories, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public void BulkUpdate_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Bulk updated" } };

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.BulkUpdate(categories, CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public void BulkDelete_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages" } };
        int rows = _connection.BulkDelete(categories, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public void BulkDelete_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages" } };

        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.BulkDelete(categories, CommandOptions.WithTransaction(nonDbTransaction)));

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    // R16 batch-2 coverage: the async siblings of the four sync Bulk* transaction-wiring tests
    // above had no equivalents anywhere in tests/ - BulkUpdateAsync/BulkDeleteAsync's transaction
    // wiring (including the "incompatible transaction throws ArgumentException, not
    // InvalidCastException" path) was entirely unverified for the async surface.

    [Fact]
    public async Task BulkUpdateAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Bulk updated" } };
        int rows = await _connection.BulkUpdateAsync(categories, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public async Task BulkUpdateAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages", Description = "Bulk updated" } };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.BulkUpdateAsync(categories, CommandOptions.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }

    [Fact]
    public async Task BulkDeleteAsync_WithRealDbTransaction_ExecutesWithinTransaction()
    {
        using var transaction = _connection.BeginTransaction();

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages" } };
        int rows = await _connection.BulkDeleteAsync(categories, CommandOptions.WithTransaction(transaction));

        Assert.Equal(1, rows);
        transaction.Rollback();
    }

    [Fact]
    public async Task BulkDeleteAsync_WithNonDbTransaction_ThrowsArgumentExceptionInsteadOfInvalidCastException()
    {
        using var realTransaction = _connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var categories = new[] { new Category { CategoryId = 1, CategoryName = "Beverages" } };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _connection.BulkDeleteAsync(categories, CommandOptions.WithTransaction(nonDbTransaction)).AsTask());

        Assert.Contains("DbTransaction", ex.Message);
        realTransaction.Rollback();
    }
}
