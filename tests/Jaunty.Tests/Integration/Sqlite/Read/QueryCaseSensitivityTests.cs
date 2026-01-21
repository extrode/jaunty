using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryCaseSensitivityTests : IDisposable
{
    private readonly Database _db;

    public QueryCaseSensitivityTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_ColumnNameCaseInsensitive_MapsCorrectly()
    {
        // Tests case-insensitive matching in MetadataCache
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS categoryid, category_name AS categoryname, description AS DESCRIPTION FROM categories WHERE category_id = @Id",
             new { Id = 1 });

        Assert.Single(categories);
        Assert.False(string.IsNullOrEmpty(categories[0].CategoryName));
    }
}