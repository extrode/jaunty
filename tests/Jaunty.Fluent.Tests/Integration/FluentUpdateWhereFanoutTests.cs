using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// CF-9 / AUD-R31-007: IUpdateWhereClause&lt;T&gt; gained the BETWEEN, EXISTS and IN-SUBQUERY
/// families that IWhereClause&lt;T&gt; already exposed. The seeded rows are Chai (Beverages,
/// 18.00), Chang (Beverages, 19.00) and Aniseed Syrup (Condiments, 10.00).
/// </summary>
public class FluentUpdateWhereFanoutTests : IDisposable
{
    private readonly InMemoryDatabase _db;

    public FluentUpdateWhereFanoutTests() => _db = new InMemoryDatabase();

    public void Dispose() => _db.Dispose();

    private List<Product> Products() => _db.Connection.From<Product>().Select();

    private static IEnumerable<string> NamesWithReorderLevel(IEnumerable<Product> products, short level)
        => products.Where(p => p.ReorderLevel == level).Select(p => p.ProductName!).OrderBy(n => n);

    private IUpdateWhereClause<Product> SetReorderLevel(short level)
        => _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, level)
            .Where(p => p.Discontinued == false);

    #region BETWEEN

    [Fact]
    public void AndBetween_UpdatesOnlyRowsInRange()
    {
        var rows = SetReorderLevel(91)
            .AndBetween(p => p.UnitPrice!.Value, 15.00m, 19.50m)
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Chai", "Chang"], NamesWithReorderLevel(Products(), 91));
    }

    [Fact]
    public void AndNotBetween_UpdatesOnlyRowsOutsideRange()
    {
        var rows = SetReorderLevel(92)
            .AndNotBetween(p => p.UnitPrice!.Value, 15.00m, 19.50m)
            .Update();

        Assert.Equal(1, rows);
        Assert.Equal(["Aniseed Syrup"], NamesWithReorderLevel(Products(), 92));
    }

    [Fact]
    public void OrBetween_WidensTheMatchRatherThanNarrowingIt()
    {
        var rows = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)93)
            .Where(p => p.ProductName == "Aniseed Syrup")
            .OrBetween(p => p.UnitPrice!.Value, 15.00m, 19.50m)
            .Update();

        Assert.Equal(3, rows);
        Assert.Equal(["Aniseed Syrup", "Chai", "Chang"], NamesWithReorderLevel(Products(), 93));
    }

    [Fact]
    public void OrNotBetween_WidensTheMatchRatherThanNarrowingIt()
    {
        var rows = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)94)
            .Where(p => p.ProductName == "Chai")
            .OrNotBetween(p => p.UnitPrice!.Value, 15.00m, 19.50m)
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Aniseed Syrup", "Chai"], NamesWithReorderLevel(Products(), 94));
    }

    #endregion

    #region EXISTS

    [Fact]
    public void AndExists_UpdatesRowsWithAMatchingCorrelatedRow()
    {
        var rows = SetReorderLevel(95)
            .AndExists<Category>((p, c) => c.CategoryId == p.CategoryId && c.CategoryName == "Beverages")
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Chai", "Chang"], NamesWithReorderLevel(Products(), 95));
    }

    [Fact]
    public void AndNotExists_UpdatesRowsWithoutAMatchingCorrelatedRow()
    {
        var rows = SetReorderLevel(96)
            .AndNotExists<Category>((p, c) => c.CategoryId == p.CategoryId && c.CategoryName == "Beverages")
            .Update();

        Assert.Equal(1, rows);
        Assert.Equal(["Aniseed Syrup"], NamesWithReorderLevel(Products(), 96));
    }

    [Fact]
    public void OrExists_WidensTheMatch()
    {
        var rows = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)97)
            .Where(p => p.ProductName == "Aniseed Syrup")
            .OrExists<Category>((p, c) => c.CategoryId == p.CategoryId && c.CategoryName == "Beverages")
            .Update();

        Assert.Equal(3, rows);
    }

    [Fact]
    public void OrNotExists_WidensTheMatch()
    {
        var rows = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)98)
            .Where(p => p.ProductName == "Chai")
            .OrNotExists<Category>((p, c) => c.CategoryId == p.CategoryId && c.CategoryName == "Beverages")
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Aniseed Syrup", "Chai"], NamesWithReorderLevel(Products(), 98));
    }

    #endregion

    #region IN SUBQUERY

    [Fact]
    public void AndInSubquery_UpdatesRowsWhoseKeyIsInTheSubqueryResult()
    {
        var beverages = _db.Connection.From<Category>().Where(c => c.CategoryName == "Beverages");

        var rows = SetReorderLevel(81)
            .AndInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId, beverages)
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Chai", "Chang"], NamesWithReorderLevel(Products(), 81));
    }

    [Fact]
    public void AndNotInSubquery_UpdatesRowsWhoseKeyIsAbsentFromTheSubqueryResult()
    {
        var beverages = _db.Connection.From<Category>().Where(c => c.CategoryName == "Beverages");

        var rows = SetReorderLevel(82)
            .AndNotInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId, beverages)
            .Update();

        Assert.Equal(1, rows);
        Assert.Equal(["Aniseed Syrup"], NamesWithReorderLevel(Products(), 82));
    }

    [Fact]
    public void OrInSubquery_WidensTheMatch()
    {
        var beverages = _db.Connection.From<Category>().Where(c => c.CategoryName == "Beverages");

        var rows = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)83)
            .Where(p => p.ProductName == "Aniseed Syrup")
            .OrInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId, beverages)
            .Update();

        Assert.Equal(3, rows);
    }

    [Fact]
    public void OrNotInSubquery_WidensTheMatch()
    {
        var beverages = _db.Connection.From<Category>().Where(c => c.CategoryName == "Beverages");

        var rows = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)84)
            .Where(p => p.ProductName == "Chai")
            .OrNotInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId, beverages)
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Aniseed Syrup", "Chai"], NamesWithReorderLevel(Products(), 84));
    }

    #endregion

    [Fact]
    public void ChainedFanoutConditions_CombineWithoutParameterCollision()
    {
        var beverages = _db.Connection.From<Category>().Where(c => c.CategoryName == "Beverages");

        var rows = SetReorderLevel(70)
            .AndBetween(p => p.UnitPrice!.Value, 15.00m, 19.50m)
            .AndExists<Category>((p, c) => c.CategoryId == p.CategoryId)
            .AndInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId, beverages)
            .Update();

        Assert.Equal(2, rows);
        Assert.Equal(["Chai", "Chang"], NamesWithReorderLevel(Products(), 70));
    }

    [Fact]
    public void ToSql_AfterFanoutCondition_ContainsTheGeneratedPredicate()
    {
        var sql = SetReorderLevel(1)
            .AndBetween(p => p.UnitPrice!.Value, 1m, 2m)
            .ToSql();

        Assert.Contains("UPDATE", sql);
        Assert.Contains("BETWEEN", sql);
    }
}
