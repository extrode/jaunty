using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryCommandOptionsTests : IDisposable
{
    private readonly Database _db;

    public QueryCommandOptionsTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_WithTimeoutOption_ExecutesCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(60));

        Assert.NotEmpty(categories);
    }

    [Fact]
    public void Query_WithCommandOptions_ExecutesCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(30));

        Assert.NotEmpty(categories);
    }
}
