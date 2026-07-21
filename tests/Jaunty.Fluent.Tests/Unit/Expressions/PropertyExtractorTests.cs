using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for PropertyExtractor, in particular the nullable-value-type ".Value" unwrapping
/// in GetMemberInfo (e.g. p => p.CategoryId!.Value should resolve to "CategoryId", not "Value").
/// </summary>
public class PropertyExtractorTests
{
    [Fact]
    public void ExtractPropertyName_DirectProperty_ReturnsPropertyName()
    {
        Expression<Func<Product, int>> selector = p => p.ProductId;

        var name = PropertyExtractor.ExtractPropertyName(selector);

        Assert.Equal("ProductId", name);
    }

    [Fact]
    public void ExtractPropertyName_NullableValueUnwrap_ReturnsUnderlyingPropertyName()
    {
        Expression<Func<Product, short>> selector = p => p.CategoryId!.Value;

        var name = PropertyExtractor.ExtractPropertyName(selector);

        Assert.Equal("CategoryId", name);
    }

    [Fact]
    public void ExtractPropertyName_NonMemberExpression_ThrowsArgumentException()
    {
        Expression<Func<Product, int>> selector = p => 5;

        Assert.Throws<ArgumentException>(() => PropertyExtractor.ExtractPropertyName(selector));
    }

    [Fact]
    public void ExtractPropertyName_NullableValueOnClosureField_FallsBackToValuePropertyName()
    {
        // The nullable-unwrap branch in GetMemberInfo only rewrites ".Value" to the underlying
        // property name when the target of ".Value" is itself a member access on a PropertyInfo
        // (e.g. p.CategoryId). When ".Value" is called on something else - here, a
        // closure-captured local, which compiles to a FieldInfo member access rather than a
        // PropertyInfo one - that rewrite doesn't apply, and the raw "Value" property name is
        // returned instead of throwing or resolving to the outer entity's property.
        short? local = 7;
        Expression<Func<Product, short>> selector = p => local!.Value;

        var name = PropertyExtractor.ExtractPropertyName(selector);

        Assert.Equal("Value", name);
    }

    [Fact]
    public void ExtractPropertyNames_EmptyArray_ReturnsEmptyArray()
    {
        var names = PropertyExtractor.ExtractPropertyNames<Product>();

        Assert.Empty(names);
    }

    [Fact]
    public void ExtractPropertyNames_MultipleSelectors_ReturnsNamesInOrder()
    {
        var names = PropertyExtractor.ExtractPropertyNames<Product>(
            p => p.ProductId,
            p => p.ProductName,
            p => p.CategoryId);

        Assert.Equal(["ProductId", "ProductName", "CategoryId"], names);
    }

    [Fact]
    public void ExtractPropertyNames_NullableValueUnwrap_ReturnsUnderlyingPropertyName()
    {
        var names = PropertyExtractor.ExtractPropertyNames<Product>(p => p.CategoryId!.Value);

        Assert.Equal(["CategoryId"], names);
    }

    [Fact]
    public void ExtractOrderByProperty_DirectProperty_ReturnsPropertyName()
    {
        Expression<Func<Product, object?>> selector = p => p.ProductName;

        var name = PropertyExtractor.ExtractOrderByProperty(selector);

        Assert.Equal("ProductName", name);
    }

    [Fact]
    public void ExtractOrderByProperty_NullableValueUnwrap_ReturnsUnderlyingPropertyName()
    {
        Expression<Func<Product, object?>> selector = p => p.CategoryId!.Value;

        var name = PropertyExtractor.ExtractOrderByProperty(selector);

        Assert.Equal("CategoryId", name);
    }

    [Fact]
    public void ExtractOrderByProperty_NonMemberExpression_ThrowsArgumentException()
    {
        Expression<Func<Product, object?>> selector = p => "literal";

        Assert.Throws<ArgumentException>(() => PropertyExtractor.ExtractOrderByProperty(selector));
    }
}
