using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R35-196 on <c>SelectExpressionVisitor.VisitMemberInit</c>.
/// </summary>
public class SelectProjectionBindingRejectionTests
{
    private readonly TestDialect _dialect = new();

    [Fact]
    public void NestedMemberBinding_IsRejected()
    {
        Expression<Func<Product, ProductDto>> expr = p => new ProductDto { Nested = { Name = p.ProductName } };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));

        Assert.Contains("MemberBinding", ex.Message);
        Assert.Contains("SELECT projections", ex.Message);
    }

    [Fact]
    public void ListBinding_IsRejected()
    {
        Expression<Func<Product, ProductDto>> expr = p => new ProductDto { Names = { p.ProductName } };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));

        Assert.Contains("ListBinding", ex.Message);
    }

    [Fact]
    public void ANestedBindingAlongsideAssignments_IsRejectedRatherThanPartiallyProjected()
    {
        Expression<Func<Product, ProductDto>> expr = p => new ProductDto
        {
            Name = p.ProductName,
            Nested = { Name = p.QuantityPerUnit }
        };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);

        Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
    }

    [Fact]
    public void AssignmentBindings_AreStillTranslated()
    {
        Expression<Func<Product, ProductDto>> expr = p => new ProductDto { Name = p.ProductName, Quantity = p.QuantityPerUnit };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);

        var columns = visitor.Translate(expr);

        Assert.Equal(2, columns.Count);
        Assert.Equal("[product_name]", columns[0].Sql);
        Assert.Equal("Name", columns[0].Alias);
        Assert.Equal("[quantity_per_unit]", columns[1].Sql);
    }

    private sealed class ProductDto
    {
        public string? Name { get; set; }
        public string? Quantity { get; set; }
        public NestedDto Nested { get; } = new();
        public List<string?> Names { get; } = new();
    }

    private sealed class NestedDto
    {
        public string? Name { get; set; }
    }
}
