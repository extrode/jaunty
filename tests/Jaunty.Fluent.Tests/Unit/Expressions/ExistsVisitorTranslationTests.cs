using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// The 2026-08-27 mutation baseline put 47 uncovered mutants in
/// <c>ExistsExpressionVisitor</c>, and they cluster: <see cref="ExistsVisitorHardeningTests"/>
/// covers every shape the visitor <em>rejects</em>, while the paths that emit a value - null
/// comparisons, parameter binding, and the OR arm of a logical operator - had no test at all.
/// These cover the emission side.
/// </summary>
public class ExistsVisitorTranslationTests
{
    private readonly TestDialect _dialect = new();

    private static ExistsExpressionVisitor<Category, Product> Visitor(TestDialect dialect)
        => new(
            dialect,
            FluentMetadataCache.GetMetadata<Category>(),
            FluentMetadataCache.GetMetadata<Product>(),
            "c",
            "p");

    private (string Sql, List<(string Name, object? Value)> Parameters) Translate(
        Expression<Func<Category, Product, bool>> predicate)
        => Visitor(_dialect).Translate(predicate);

    [Fact]
    public void OrElse_EmitsOrNotAnd()
    {
        var (sql, _) = Translate((c, p) => c.CategoryId == p.CategoryId || p.UnitPrice > 10m);

        Assert.Contains(" OR ", sql);
        Assert.DoesNotContain(" AND ", sql);
    }

    [Fact]
    public void AndAlso_EmitsAndNotOr()
    {
        var (sql, _) = Translate((c, p) => c.CategoryId == p.CategoryId && p.UnitPrice > 10m);

        Assert.Contains(" AND ", sql);
        Assert.DoesNotContain(" OR ", sql);
    }

    [Fact]
    public void ColumnEqualsNull_EmitsIsNullAndBindsNothing()
    {
        var (sql, parameters) = Translate((c, p) => p.QuantityPerUnit == null);

        Assert.Contains(" IS NULL", sql);
        Assert.DoesNotContain("IS NOT NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ColumnNotEqualsNull_EmitsIsNotNullAndBindsNothing()
    {
        var (sql, parameters) = Translate((c, p) => p.QuantityPerUnit != null);

        Assert.Contains(" IS NOT NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void NullEqualsColumn_EmitsIsNullWithTheColumnOnTheLeft()
    {
        var (sql, parameters) = Translate((c, p) => null == p.QuantityPerUnit);

        Assert.Contains(" IS NULL", sql);
        Assert.DoesNotContain("IS NOT NULL", sql);
        Assert.Empty(parameters);
        Assert.StartsWith("(p.", sql);
    }

    [Fact]
    public void NullNotEqualsColumn_EmitsIsNotNullWithTheColumnOnTheLeft()
    {
        var (sql, parameters) = Translate((c, p) => null != p.QuantityPerUnit);

        Assert.Contains(" IS NOT NULL", sql);
        Assert.Empty(parameters);
        Assert.StartsWith("(p.", sql);
    }

    [Fact]
    public void ConstantOnTheLeft_IsBoundAsAParameter()
    {
        var (sql, parameters) = Translate((c, p) => 7 == c.CategoryId);

        Assert.Contains("@p_exists", sql);
        var parameter = Assert.Single(parameters);
        Assert.Equal("@p_exists", parameter.Name);
        Assert.Equal(7, parameter.Value);
    }

    [Fact]
    public void ConstantOnTheRight_IsBoundAsAParameter()
    {
        var (sql, parameters) = Translate((c, p) => p.UnitPrice > 10m);

        Assert.Contains("@p_exists", sql);
        var parameter = Assert.Single(parameters);
        Assert.Equal(10m, parameter.Value);
    }

    [Fact]
    public void TwoConstants_GetDistinctNames_AndTheNumberedSuffixSkipsOne()
    {
        var (sql, parameters) = Translate((c, p) => p.UnitPrice > 10m && p.CategoryId < (short)5);

        Assert.Equal(2, parameters.Count);
        Assert.Equal("@p_exists", parameters[0].Name);
        Assert.Equal("@p_exists2", parameters[1].Name);
        Assert.Contains("@p_exists2", sql);
        Assert.DoesNotContain("@p_exists1", sql);
        Assert.Distinct(parameters.Select(p => p.Name));
    }

    [Fact]
    public void BothSidesColumns_BindNothing()
    {
        var (sql, parameters) = Translate((c, p) => c.CategoryId == p.CategoryId);

        Assert.Empty(parameters);
        Assert.Contains("c.", sql);
        Assert.Contains("p.", sql);
        Assert.DoesNotContain("@p_exists", sql);
    }
}
