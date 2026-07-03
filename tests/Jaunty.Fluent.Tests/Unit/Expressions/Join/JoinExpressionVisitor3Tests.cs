using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for JoinExpressionVisitor3 (3-table joins).
/// Tests SQL WHERE clause generation from lambda expressions.
/// </summary>
public class JoinExpressionVisitor3Tests
{
    private readonly TestDialect _dialect = new();

    #region Simple Where Conditions

    [Fact]
    public void Visit_SingleCondition_GeneratesWhereClause()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.Discontinued == false;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("[discontinued]", sql);
        Assert.Contains("=", sql);
    }

    [Fact]
    public void Visit_EqualityComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => c.CategoryId == 1;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("c.[category_id]", sql);
        Assert.Contains(parameters, p => Equals(p.Value, 1));
    }

    [Fact]
    public void Visit_ThirdTableProperty_GeneratesCorrectAlias()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => s.Country == "UK";
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("s.[country]", sql);
        Assert.Contains(parameters, p => Equals(p.Value, "UK"));
    }

    #endregion

    #region AndAlso (And) Conditions

    [Fact]
    public void Visit_AndAlso_CombinesWithAnd()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => c.CategoryId == 1 && s.Country == "UK";
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("AND", sql);
        Assert.Contains("c.[category_id]", sql);
        Assert.Contains("s.[country]", sql);
        Assert.Contains(parameters, p => Equals(p.Value, 1));
        Assert.Contains(parameters, p => Equals(p.Value, "UK"));
    }

    [Fact]
    public void Visit_MultipleAnd_CombinesAllConditions()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => c.CategoryId == 1 && s.Country == "UK" && p.Discontinued == false;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        var andCount = sql.Split("AND", StringSplitOptions.None).Length - 1;
        Assert.Equal(2, andCount);
    }

    #endregion

    #region OrElse (Or) Conditions

    [Fact]
    public void Visit_OrElse_CombinesWithOr()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => c.CategoryId == 1 || c.CategoryId == 2;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("OR", sql);
    }

    [Fact]
    public void Visit_OrElse_WithDifferentTables_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => c.CategoryId == 1 || s.SupplierId == 1;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("OR", sql);
        Assert.Contains("c.[category_id]", sql);
        Assert.Contains("s.[supplier_id]", sql);
    }

    #endregion

    #region Comparison Operators

    [Fact]
    public void Visit_GreaterThan_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.UnitPrice > 10;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains(">", sql);
        Assert.Contains("[unit_price]", sql);
    }

    [Fact]
    public void Visit_LessThan_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.UnitPrice < 50;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("<", sql);
    }

    [Fact]
    public void Visit_GreaterThanOrEqual_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.UnitPrice >= 10;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains(">=", sql);
    }

    [Fact]
    public void Visit_LessThanOrEqual_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.UnitPrice <= 100;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("<=", sql);
    }

    #endregion

    #region String Comparisons

    [Fact]
    public void Visit_StringEquals_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => s.Country == "USA";
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains(parameters, p => Equals(p.Value, "USA"));
    }

    #endregion

    #region Boolean Comparisons

    [Fact]
    public void Visit_BooleanEqualsTrue_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.Discontinued == true;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("[discontinued]", sql);
        Assert.Contains(parameters, p => Equals(p.Value, true));
    }

    [Fact]
    public void Visit_BooleanEqualsFalse_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => p.Discontinued == false;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("[discontinued]", sql);
        Assert.Contains(parameters, p => Equals(p.Value, false));
    }

    #endregion

    #region Complex Expressions

    [Fact]
    public void Visit_ComplexExpression_CombinesCorrectly()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) =>
            (c.CategoryId == 1 || c.CategoryId == 2) && p.Discontinued == false;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("AND", sql);
        Assert.Contains("OR", sql);
    }

    #endregion
}

/// <summary>
/// Unit tests for JoinExpressionVisitor4 (4-table joins).
/// Tests SQL WHERE clause generation from lambda expressions.
/// </summary>
public class JoinExpressionVisitor4Tests
{
    private readonly TestDialect _dialect = new();

    #region Simple Where Conditions

    [Fact]
    public void Visit_SingleCondition_GeneratesWhereClause()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) => p.Discontinued == false;
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("[discontinued]", sql);
    }

    [Fact]
    public void Visit_FourthTableProperty_GeneratesCorrectAlias()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) => o.OrderId == 1;
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("o.[order_id]", sql);
        Assert.Contains(parameters, p => Equals(p.Value, 1));
    }

    #endregion

    #region Multi-Table Conditions

    [Fact]
    public void Visit_AllFourTables_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) =>
            c.CategoryId == 1 && s.Country == "UK" && p.Discontinued == false && o.OrderId > 100;
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("p.[discontinued]", sql);
        Assert.Contains("c.[category_id]", sql);
        Assert.Contains("s.[country]", sql);
        Assert.Contains("o.[order_id]", sql);
    }

    [Fact]
    public void Visit_OrAcrossTables_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) =>
            c.CategoryId == 1 || s.SupplierId == 1;
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("OR", sql);
        Assert.Contains("c.[category_id]", sql);
        Assert.Contains("s.[supplier_id]", sql);
    }

    #endregion

    #region Complex Expressions

    [Fact]
    public void Visit_ComplexExpression_CombinesCorrectly()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) =>
            (c.CategoryId == 1 && s.Country == "UK") || (c.CategoryId == 2 && s.Country == "USA");
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("AND", sql);
        Assert.Contains("OR", sql);
    }

    #endregion
}
