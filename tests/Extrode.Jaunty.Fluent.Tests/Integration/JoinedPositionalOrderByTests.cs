using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R38-040: the type-keyed OrderByJoined overloads were CS0121 when two joined entities shared a
/// type, leaving no way to order by the later one.
/// </summary>
public class JoinedPositionalOrderByTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public JoinedPositionalOrderByTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IJoinedQuery3<Product, Category, Category> TwoCategories() => _fixture.Connection.From<Product>("p")
        .InnerJoin<Category>("c1").On("p.category_id", "c1.category_id")
        .InnerJoin<Category>("c2").On("p.category_id", "c2.category_id");

    private IJoinedQuery4<Product, Category, Supplier, Supplier> TwoSuppliers() => _fixture.Connection.From<Product>("p")
        .InnerJoin<Category>("c").On("p.category_id", "c.category_id")
        .InnerJoin<Supplier>("s1").On("p.supplier_id", "s1.supplier_id")
        .InnerJoin<Product, Category, Supplier, Supplier>("s2").On("p.supplier_id", "s2.supplier_id");

    [Fact]
    public void ThreeWay_OrderBy_PicksTheEntityByPosition()
    {
        string sql = TwoCategories().OrderBy((p, c1, c2) => c2.CategoryName).ToSql();

        Assert.Matches(@"ORDER BY c2\.\W?category_name\W?$", sql);
    }

    [Fact]
    public void ThreeWay_AllFourVerbs_ResolveEachPosition()
    {
        string sql = TwoCategories()
            .OrderByDescending((p, c1, c2) => p.ProductName)
            .ThenBy((p, c1, c2) => c1.CategoryName)
            .ThenByDescending((p, c1, c2) => c2.CategoryId)
            .ToSql();

        Assert.Matches(@"ORDER BY p\.\W?product_name\W? DESC, c1\.\W?category_name\W?, c2\.\W?category_id\W? DESC", sql);
    }

    [Fact]
    public void ThreeWay_OrderBy_ConvertedValueMember_Resolves()
    {
        string sql = TwoCategories().OrderBy<object>((p, c1, c2) => c2.CategoryId).ToSql();

        Assert.Matches(@"ORDER BY c2\.\W?category_id\W?$", sql);
    }

    [Fact]
    public void ThreeWay_OrderBy_Runs()
    {
        List<Product> results = TwoCategories().OrderBy((p, c1, c2) => c2.CategoryName).ThenBy((p, c1, c2) => p.ProductId).Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void FourWay_AllFourVerbs_ResolveEachPosition()
    {
        string sql = TwoSuppliers()
            .OrderBy((p, c, s1, s2) => s2.CompanyName)
            .ThenByDescending((p, c, s1, s2) => s1.City)
            .ThenBy((p, c, s1, s2) => c.CategoryName)
            .ThenByDescending((p, c, s1, s2) => p.ProductId)
            .ToSql();

        Assert.Matches(@"ORDER BY s2\.\W?company_name\W?, s1\.\W?city\W? DESC, c\.\W?category_name\W?, p\.\W?product_id\W? DESC", sql);
    }

    [Fact]
    public void FourWay_OrderByDescending_Runs()
    {
        IJoinedQuery4<Product, Category, Supplier, Supplier> query = TwoSuppliers().OrderByDescending((p, c, s1, s2) => s2.CompanyName);

        Assert.Matches(@"ORDER BY s2\.\W?company_name\W? DESC$", query.ToSql());
        Assert.NotEmpty(query.Select());
    }

    [Fact]
    public void OrderBy_MemberOfAColumn_IsRejected()
    {
        Assert.Throws<NotSupportedException>(() => TwoCategories().OrderBy((p, c1, c2) => c2.CategoryName.Length));
    }

    [Fact]
    public void OrderBy_AParameterNotInTheSelector_IsRejected()
    {
        ParameterExpression p = Expression.Parameter(typeof(Product), "p");
        ParameterExpression c1 = Expression.Parameter(typeof(Category), "c1");
        ParameterExpression c2 = Expression.Parameter(typeof(Category), "c2");
        ParameterExpression stranger = Expression.Parameter(typeof(Category), "x");
        var selector = Expression.Lambda<Func<Product, Category, Category, string>>(
            Expression.Property(stranger, nameof(Category.CategoryName)), p, c1, c2);

        var ex = Assert.Throws<ArgumentException>(() => TwoCategories().OrderBy(selector));
        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void OrderBy_NotAMember_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => TwoCategories().OrderBy((p, c1, c2) => c2.CategoryName + "x"));
    }
}
