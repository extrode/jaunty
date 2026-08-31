using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R35-063. Within one class, <c>SelectPartial(columns)</c>/<c>SelectPartialAsync(columns)</c>
/// and <c>SelectPartialFirst/Single(+OrDefault)</c> answered the duplicate-column question two
/// different ways at arities 3 and 4, because they ran on two different execution paths: the plain
/// overloads called the core <c>Connection.QueryPartialList</c>, whose row builder is an
/// <c>OrdinalIgnoreCase</c> dictionary filled by assignment (duplicates resolve silently last-wins,
/// lookups are case-insensitive), while every other member delegated to the arity-2 builder's
/// <c>MapToDictionary</c>, which is case-sensitive and throws. The arity-2 builder threw for both.
/// <para>
/// Selecting a caller-written column list over a join is exactly where a duplicate name arises, and
/// it was the plain overload - the one that swallowed it - that callers reach first. The plain
/// overloads now delegate too, which also puts both halves of the family on the same parameter
/// binder.
/// </para>
/// </summary>
public class FluentJoinSelectPartialDuplicateColumnTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinSelectPartialDuplicateColumnTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private const string Duplicates = "p.category_id, c.category_id";

    private static void AssertAmbiguous(InvalidOperationException ex)
    {
        Assert.Contains("ambiguous", ex.Message, StringComparison.Ordinal);
        Assert.Contains("category_id", ex.Message, StringComparison.Ordinal);
    }

    private IJoinedQuery<Product, Category> TwoTable() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id");

    private IJoinedQuery3<Product, Category, Supplier> ThreeTable() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id");

    private IJoinedQuery4<Product, Category, Supplier, Order> FourTable() =>
        _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On("o.order_id > 0");

    // ------------------------------------------------------------------
    // The reference behaviour: arity 2, which always threw.
    // ------------------------------------------------------------------

    [Fact]
    public void TwoTable_SelectPartial_WithDuplicateColumn_Throws()
    {
        AssertAmbiguous(Assert.Throws<InvalidOperationException>(
            () => TwoTable().SelectPartial(Duplicates)));
    }

    [Fact]
    public async Task TwoTable_SelectPartialAsync_WithDuplicateColumn_Throws()
    {
        AssertAmbiguous(await Assert.ThrowsAsync<InvalidOperationException>(
            () => TwoTable().SelectPartialAsync(Duplicates)));
    }

    // ------------------------------------------------------------------
    // Arity 3
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeTable_SelectPartial_WithDuplicateColumn_Throws()
    {
        AssertAmbiguous(Assert.Throws<InvalidOperationException>(
            () => ThreeTable().SelectPartial(Duplicates)));
    }

    [Fact]
    public async Task ThreeTable_SelectPartialAsync_WithDuplicateColumn_Throws()
    {
        AssertAmbiguous(await Assert.ThrowsAsync<InvalidOperationException>(
            () => ThreeTable().SelectPartialAsync(Duplicates)));
    }

    [Fact]
    public void ThreeTable_SelectPartialFirst_WithDuplicateColumn_ThrowsTheSameWay()
    {
        AssertAmbiguous(Assert.Throws<InvalidOperationException>(
            () => ThreeTable().SelectPartialFirst(Duplicates)));
    }

    // ------------------------------------------------------------------
    // Arity 4
    // ------------------------------------------------------------------

    [Fact]
    public void FourTable_SelectPartial_WithDuplicateColumn_Throws()
    {
        AssertAmbiguous(Assert.Throws<InvalidOperationException>(
            () => FourTable().SelectPartial(Duplicates)));
    }

    [Fact]
    public async Task FourTable_SelectPartialAsync_WithDuplicateColumn_Throws()
    {
        AssertAmbiguous(await Assert.ThrowsAsync<InvalidOperationException>(
            () => FourTable().SelectPartialAsync(Duplicates)));
    }

    [Fact]
    public void FourTable_SelectPartialFirst_WithDuplicateColumn_ThrowsTheSameWay()
    {
        AssertAmbiguous(Assert.Throws<InvalidOperationException>(
            () => FourTable().SelectPartialFirst(Duplicates)));
    }

    // ------------------------------------------------------------------
    // Keys are case-sensitive now, matching the rest of the family. The core
    // QueryPartialList dictionary the plain overloads used to build was OrdinalIgnoreCase.
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeTable_SelectPartial_KeysAreCaseSensitive()
    {
        var rows = ThreeTable().SelectPartial("p.product_name");

        Assert.NotEmpty(rows);
        Assert.True(rows[0].ContainsKey("product_name"));
        Assert.False(rows[0].ContainsKey("PRODUCT_NAME"));
    }

    [Fact]
    public async Task FourTable_SelectPartialAsync_KeysAreCaseSensitive()
    {
        var rows = await FourTable().SelectPartialAsync("p.product_name");

        Assert.NotEmpty(rows);
        Assert.True(rows[0].ContainsKey("product_name"));
        Assert.False(rows[0].ContainsKey("PRODUCT_NAME"));
    }

    // ------------------------------------------------------------------
    // The control: a distinct column list still returns rows through the delegated path.
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeTable_SelectPartial_WithDistinctColumns_StillReturnsRows()
    {
        var rows = ThreeTable().SelectPartial("p.product_name, c.category_name, s.company_name");

        Assert.NotEmpty(rows);
        Assert.Equal(3, rows[0].Count);
    }

    [Fact]
    public void FourTable_SelectPartial_WithDistinctColumns_StillReturnsRows()
    {
        var rows = FourTable().SelectPartial("p.product_name, c.category_name");

        Assert.NotEmpty(rows);
        Assert.Equal(2, rows[0].Count);
    }
}
