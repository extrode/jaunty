using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryParameterBindingErrorTests : IDisposable
{
    private readonly Database _db;

    public QueryParameterBindingErrorTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_MissingParameter_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id AND category_name = @Name",
                new { Id = 1 })); // Missing Name parameter

        Assert.Contains("@Name", ex.Message);
    }

    [Fact]
    public void Query_ExtraParameter_ThrowsArgumentException()
    {
        // Extra parameters in the anonymous object cause an error
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
                new { Id = 1, Extra1 = 2, Extra2 = 3 }));

        Assert.Contains("Unused parameter", ex.Message);
    }
}