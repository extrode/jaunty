using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R38-008: <c>Distinct()</c> led round the compile-time paged-write fence, and the DELETE or
/// UPDATE that followed dropped the Take/Skip and touched every matching row.
/// </summary>
public class PagedWriteRuntimeGuardTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private int ProductCount() => _db.Connection.From<Product>().Count();

    [Fact]
    public void TakeThenDistinctThenDelete_ThrowsAndDeletesNothing()
    {
        int before = ProductCount();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.From<Product>().Take(1).Distinct().Where(p => p.ProductId > 0).Delete());

        Assert.Equal("Take/Skip are not carried into DELETE, so it would affect every matching row rather than the paged subset. Remove the Take/Skip, or select the rows first and delete them by key.", ex.Message);
        Assert.Equal(before, ProductCount());
    }

    [Fact]
    public async Task DistinctThenSkipThenDeleteAsync_ThrowsAndDeletesNothing()
    {
        int before = ProductCount();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _db.Connection.From<Product>().Distinct().Skip(1).Where(p => p.ProductId > 0).DeleteAsync());

        Assert.Equal(before, ProductCount());
    }

    [Fact]
    public void PagedBuilderReachingUpdate_ThrowsAndUpdatesNothing()
    {
        IFromClause<Product> paged = (QueryBuilder<Product>)_db.Connection.From<Product>().Distinct().Take(1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            paged.Set(p => p.ProductName, "Renamed").UpdateAll());

        Assert.StartsWith("Take/Skip are not carried into UPDATE", ex.Message);
        Assert.EndsWith("update them by key.", ex.Message);
        Assert.Equal(0, _db.Connection.From<Product>().Where(p => p.ProductName == "Renamed").Count());
    }

    [Fact]
    public void ToDeleteSql_AfterPaging_Throws()
    {
        var query = (QueryBuilder<Product>)_db.Connection.From<Product>().Distinct().Take(1);

        Assert.Throws<InvalidOperationException>(() => query.ToDeleteSql());
    }

    [Fact]
    public void DeleteWithoutPaging_StillDeletes()
    {
        int before = ProductCount();

        int deleted = _db.Connection.From<Product>().Distinct().Where(p => p.ProductId == 1).Delete();

        Assert.Equal(1, deleted);
        Assert.Equal(before - 1, ProductCount());
    }
}
