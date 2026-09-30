using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

public class PropertyExtractorExactTests
{
    public sealed class Inner
    {
        public int? N { get; set; }
    }

    public sealed class Wrapper
    {
        public Inner Holder { get; set; } = new();
    }

    [Fact]
    public void ExtractPropertyName_OfANonProperty_NamesTheExpressionAndTheParameter()
    {
        Expression<Func<Product, int>> selector = p => 5;

        var ex = Assert.Throws<ArgumentException>(() => PropertyExtractor.ExtractPropertyName(selector));

        Assert.StartsWith("Expression 'p => 5' does not refer to a property.", ex.Message, StringComparison.Ordinal);
        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void ExtractOrderByProperty_OfANonProperty_NamesTheExpressionAndTheParameter()
    {
        Expression<Func<Product, object?>> selector = p => 5;

        var ex = Assert.Throws<ArgumentException>(() => PropertyExtractor.ExtractOrderByProperty(selector));

        Assert.StartsWith($"Expression '{selector}' does not refer to a property.", ex.Message, StringComparison.Ordinal);
        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void ExtractPropertyNames_OfANonProperty_NamesTheExpressionAndTheParameter()
    {
        Expression<Func<Product, object?>> bad = p => 5;

        var ex = Assert.Throws<ArgumentException>(() => PropertyExtractor.ExtractPropertyNames<Product>(p => p.ProductId, bad));

        Assert.StartsWith($"Expression '{bad}' does not refer to a property.", ex.Message, StringComparison.Ordinal);
        Assert.Equal("selectors", ex.ParamName);
    }

    [Fact]
    public void ExtractPropertyNames_WithANullArray_ReturnsEmpty()
        => Assert.Empty(PropertyExtractor.ExtractPropertyNames<Product>(null!));

    [Fact]
    public void ANullableMemberOtherThanValue_IsRejectedAsAMemberOfAColumn()
    {
        Expression<Func<Product, object?>> selector = p => p.CategoryId.HasValue;

        var ex = Assert.Throws<NotSupportedException>(() => PropertyExtractor.ExtractPropertyNames(selector));

        Assert.Contains("is a member of a column, not a column", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullableValueOverANestedColumn_IsRejectedAsAMemberOfAColumn()
    {
        Expression<Func<Wrapper, int>> selector = w => w.Holder.N!.Value;

        var ex = Assert.Throws<NotSupportedException>(() => PropertyExtractor.ExtractPropertyName(selector));

        Assert.Contains("is a member of a column, not a column", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMemberOfAColumn_IsRejectedWithTheFullGuidance()
    {
        Expression<Func<Order, int>> selector = o => o.OrderDate!.Value.Year;

        var ex = Assert.Throws<NotSupportedException>(() => PropertyExtractor.ExtractPropertyName(selector));

        Assert.Equal(
            $"'{selector.Body}' is a member of a column, not a column. Extrode.Jaunty does not translate " +
            "'Year' into SQL - use the Sql.* helpers for the supported spellings " +
            "(Sql.Year, Sql.Month, Sql.Day, Sql.Length, ...), or compute the value in memory.",
            ex.Message);
    }
}
