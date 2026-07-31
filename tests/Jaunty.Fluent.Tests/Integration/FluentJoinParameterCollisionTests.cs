using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// R27 batch 8/9 (high). Joined-query positional parameters ("@jp0", "@jp1", ...) are minted by
/// two independent counters: each Where/And/Or visitor restarts at 0 and the builder renumbers
/// its output against a query-wide sequence, while On(predicate) registered its visitor's raw
/// names without advancing that sequence. Two collision shapes followed: (a) renumbering one
/// name at a time let a new name capture a not-yet-renamed old occurrence in the same condition,
/// silently binding one value to both operands; (b) the first Where after a value-binding
/// On(predicate) renumbered its parameter onto the ON parameter's name and threw at build time.
/// </summary>
public class FluentJoinParameterCollisionTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinParameterCollisionTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void InnerJoin_Where_ThenAnd_WithTwoParameters_BindsBothValues()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .And((p, c) => p.UnitPrice > 10m && p.UnitPrice < 20m)
            .Select();

        Assert.Equal(2, products.Count);
        Assert.Contains(products, p => p.ProductName == "Chai");
        Assert.Contains(products, p => p.ProductName == "Chang");
    }

    [Fact]
    public void InnerJoin_OnPredicateWithValue_ThenWhereWithValue_DoesNotCollide()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId && c.CategoryId == 1)
            .Where((p, c) => p.UnitPrice > 100m)
            .Select();

        var product = Assert.Single(products);
        Assert.Equal("Luxury Item", product.ProductName);
    }

    [Fact]
    public void InnerJoin_OnPredicateWithValue_ThenAndOr_AllValuesBound()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId && c.CategoryId == 1)
            .Where((p, c) => p.UnitPrice > 10m && p.UnitPrice < 20m)
            .Or((p, c) => p.UnitPrice > 100m)
            .Select();

        Assert.Equal(3, products.Count);
        Assert.Contains(products, p => p.ProductName == "Chai");
        Assert.Contains(products, p => p.ProductName == "Chang");
        Assert.Contains(products, p => p.ProductName == "Luxury Item");
    }

    [Fact]
    public void InnerJoin_TwelveWherePredicates_MultiDigitTokensRenumberCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .Where((p, c) => (p.UnitPrice > 10m && p.UnitPrice < 20m) || (p.ProductId > 500 && p.ProductId < 600))
            .And((p, c) => c.CategoryId >= 1 && c.CategoryId <= 9999 && p.ProductId > 0 && p.ProductId < 100000)
            .Or((p, c) => p.UnitPrice > 100000m && p.UnitPrice < 100001m && p.ProductId > 900000 && p.ProductId < 900001)
            .Select();

        Assert.Equal(2, products.Count);
        Assert.Contains(products, p => p.ProductName == "Chai");
        Assert.Contains(products, p => p.ProductName == "Chang");
    }

    [Fact]
    public void On_NamedParameter_ReservedPositionalName_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id = categories.category_id AND categories.category_id = @jp0", "jp0", 1));

        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void On_NamedParameter_WhitespaceName_Throws()
    {
        Assert.Throws<ArgumentException>(() => _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id = categories.category_id", "  ", 1));
    }

    [Fact]
    public void On_NamedParameter_PrefixOnlyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id = categories.category_id", "@", 1));
    }

    [Fact]
    public void On_NamedParameter_DuplicateName_ThrowsNamingRemedy()
    {
        var ex = Assert.Throws<ArgumentException>(() => _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id = @catId", "catId", 1)
            .InnerJoin<Supplier>()
            .On("products.supplier_id = @catId", "catId", 1));

        Assert.Contains("On(condition, parameterName, value)", ex.Message);
    }
}
