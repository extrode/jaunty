using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for ExistsExpressionVisitor.
/// </summary>
public class ExistsExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();

    [Fact]
    public void Translate_EqualityCorrelation_GeneratesSql()
    {
        var visitor = new ExistsExpressionVisitor<Product, Category>(
            _dialect,
            FluentMetadataCache.GetMetadata<Product>(),
            FluentMetadataCache.GetMetadata<Category>(),
            "p",
            "c");

        Expression<Func<Product, Category, bool>> predicate = (p, c) => p.CategoryId == c.CategoryId;
        var (sql, _) = visitor.Translate(predicate);

        Assert.Contains("p.[category_id]", sql);
        Assert.Contains("c.[category_id]", sql);
    }

    [Fact]
    public void Translate_MethodCall_ThrowsNotSupportedInsteadOfEmittingGarbageSql()
    {
        // Regression test: VisitMethodCall used to be unoverridden, so ExpressionVisitor's
        // base implementation silently produced malformed, operator-less SQL for method
        // calls in a correlation predicate instead of failing loudly.
        var visitor = new ExistsExpressionVisitor<Product, Category>(
            _dialect,
            FluentMetadataCache.GetMetadata<Product>(),
            FluentMetadataCache.GetMetadata<Category>(),
            "p",
            "c");

        Expression<Func<Product, Category, bool>> predicate = (p, c) => c.CategoryName.Contains("x");

        Assert.Throws<NotSupportedException>(() => visitor.Translate(predicate));
    }
}
