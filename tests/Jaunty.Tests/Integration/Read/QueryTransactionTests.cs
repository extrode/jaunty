using System.Data;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryTransactionTests : IDisposable
{
    private readonly Database _db;

    public QueryTransactionTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_WithTransaction_ExecutesCorrectly()
    {
        _db.Connection.Open();
        using var transaction = _db.Connection.BeginTransaction();

        var categories = _db.Connection.Query(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            new CommandOptions<Category>(transaction: transaction));

        Assert.NotEmpty(categories);
        transaction.Rollback();
    }

    [Fact]
    public void QueryScalar_WithTransaction_ExecutesCorrectly()
    {
        _db.Connection.Open();
        using var transaction = _db.Connection.BeginTransaction();

        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products",
            new CommandOptions(transaction));

        Assert.True(count > 0);
        transaction.Rollback();
    }
}