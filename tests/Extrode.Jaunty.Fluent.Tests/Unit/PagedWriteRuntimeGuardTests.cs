using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R38-008: <c>Distinct()</c> led round the compile-time paged-write fence, and the DELETE or
/// UPDATE that followed dropped the Take/Skip and touched every matching row. Distinct is fenced at
/// compile time now too; an upcast still reaches the write terminals, so the runtime guard stays.
/// </summary>
public class PagedWriteRuntimeGuardTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private int ProductCount() => _db.Connection.From<Product>().Count();

    [Fact]
    public void TakeThenUpcastThenDelete_ThrowsAndDeletesNothing()
    {
        int before = ProductCount();
        IFromClause<Product> paged = _db.Connection.From<Product>().Take(1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            paged.Where(p => p.ProductId > 0).Delete());

        Assert.Equal("Take/Skip are not carried into DELETE: SQL has no portable DELETE ... LIMIT, so it would affect every matching row rather than the paged subset. Select the keys of the rows you want, then delete them by key in one transaction (see 'Writes after Take or Skip' in docs/01-api-reference/fluent-api.md).", ex.Message);
        Assert.Equal(before, ProductCount());
    }

    [Fact]
    public async Task DistinctThenSkipThenUpcastThenDeleteAsync_ThrowsAndDeletesNothing()
    {
        int before = ProductCount();
        IDistinctClause<Product> distinct = _db.Connection.From<Product>().Distinct().Skip(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            distinct.Where(p => p.ProductId > 0).DeleteAsync());

        Assert.Equal(before, ProductCount());
    }

    [Fact]
    public void PagedBuilderReachingUpdate_ThrowsAndUpdatesNothing()
    {
        IFromClause<Product> paged = (QueryBuilder<Product>)_db.Connection.From<Product>().Distinct().Take(1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            paged.Set(p => p.ProductName, "Renamed").UpdateAll());

        Assert.StartsWith("Take/Skip are not carried into UPDATE", ex.Message);
        Assert.Contains("update them by key in one transaction", ex.Message);
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
