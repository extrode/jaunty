using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R38-038/039 and the batch-14 lows: GROUP BY keys, aggregate operands and HAVING aggregates
/// took any member as a column; <c>g.Key.Member</c> resolved through the entity instead of the key;
/// <c>ColumnReference.RequireDirect</c> let static members through; Where's parameter finder
/// matched a nested lambda's parameter and had no <c>VisitParameter</c>.
/// </summary>
public class GroupByKeyAndColumnResolutionTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public GroupByKeyAndColumnResolutionTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void RenamedCompositeKeyMembers_SelectTheirOwnColumns()
    {
        string sql = _fixture.Connection.From<Product>()
            .GroupBy(p => new { Cat = p.CategoryId, Sup = p.SupplierId })
            .ToSql(g => new { g.Key.Cat, g.Key.Sup, N = g.Count() });

        Assert.Contains("category_id AS Cat", sql);
        Assert.Contains("supplier_id AS Sup", sql);
    }

    [Fact]
    public void AKeyMemberNamedAfterAnotherProperty_SelectsTheKeysColumn()
    {
        string sql = _fixture.Connection.From<Product>()
            .GroupBy(p => new { CategoryId = p.SupplierId, p.Discontinued })
            .ToSql(g => new { g.Key.CategoryId, N = g.Count() });

        Assert.Contains("supplier_id AS CategoryId", sql);
        Assert.DoesNotContain("category_id", sql);
    }

    [Fact]
    public void RenamedCompositeKey_ExecutesLikeTheUnrenamedOne()
    {
        var renamed = _fixture.Connection.From<Product>()
            .GroupBy(p => new { Cat = p.CategoryId, Sup = p.SupplierId })
            .Select(g => new { g.Key.Cat, g.Key.Sup, N = g.Count() })
            .Select(r => (r.Cat, r.Sup, r.N)).OrderBy(r => r.Cat).ThenBy(r => r.Sup).ToList();

        var plain = _fixture.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .Select(g => new { g.Key.CategoryId, g.Key.SupplierId, N = g.Count() })
            .Select(r => (r.CategoryId, r.SupplierId, r.N)).OrderBy(r => r.CategoryId).ThenBy(r => r.SupplierId).ToList();

        Assert.NotEmpty(plain);
        Assert.Equal(plain, renamed);
    }

    [Fact]
    public void AMemberOfAScalarKey_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.ProductName)
            .ToSql(g => new { g.Key.Length, N = g.Count() }));

        Assert.Equal("Unknown GROUP BY key property 'Length'.", ex.Message);
    }

    [Fact]
    public void ANestedGroupByKey_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.ProductName.Length));

        Assert.Contains("is a member of a column, not a column", ex.Message);
    }

    [Fact]
    public void ANestedCompositeKeyMember_Throws()
    {
        Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.ProductName.Length }));
    }

    [Fact]
    public void ACapturedGroupByKey_Throws()
    {
        var box = new { Value = 1 };

        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => box.Value));

        Assert.EndsWith("is not a property of 'p'. Only a property of the lambda parameter can be translated to a column here.", ex.Message);
    }

    [Fact]
    public void ANestedAggregateOperand_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { g.Key, Total = g.Sum(p => p.ProductName.Length) }));

        Assert.Contains("is a member of a column, not a column", ex.Message);
    }

    [Fact]
    public void ACapturedAggregateOperand_Throws()
    {
        int factor = 2;

        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { g.Key, Total = g.Sum(p => factor) }));

        Assert.EndsWith("is not a property of 'p'. Only a property of the lambda parameter can be translated to a column here.", ex.Message);
    }

    [Fact]
    public void ANestedHavingAggregateOperand_Throws()
    {
        Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Sum(p => p.ProductName.Length) > 3));
    }

    [Fact]
    public void ACapturedHavingAggregateOperand_Throws()
    {
        int factor = 2;

        Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Max(p => factor) > 3));
    }

    [Fact]
    public void ADirectHavingAggregateOperand_StillTranslates()
    {
        string sql = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Sum(p => p.UnitPrice) > 3)
            .ToSql(g => new { g.Key, N = g.Count() });

        Assert.Contains("HAVING SUM(unit_price) > @sum_unit_price", sql);
    }

    [Fact]
    public void AStaticMemberAsAnOrderByColumn_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            PropertyExtractor.ExtractOrderByProperty<Order>(o => DateTime.Now));

        Assert.Equal("'DateTime.Now' is a static member, not a column of the entity. Compute the value before the query, or name a property of the lambda parameter.", ex.Message);
    }

    [Fact]
    public void AStaticMemberAsAJoinKey_Throws()
    {
        Expression<Func<Order, string>> selector = o => Environment.NewLine;

        Assert.Throws<NotSupportedException>(() => PropertyExtractor.ExtractPropertyName(selector));
    }

    [Fact]
    public void Where_AClosedCallTakingALambda_IsEvaluatedNotRejected()
    {
        var allowed = new List<string> { "x" };

        var (sql, _) = new WhereExpressionVisitor<Product>(new TestDialect())
            .Translate(p => p.CategoryId == 3 && allowed.Any(a => a == "x"));

        Assert.EndsWith("AND 1 = 1)", sql);
    }

    [Fact]
    public void Where_TheWholeEntityComparedToNull_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            new WhereExpressionVisitor<Product>(new TestDialect()).Translate(p => (object)p == null));

        Assert.Equal("'p' is the whole entity, not a condition. Compare its properties instead.", ex.Message);
    }
}
