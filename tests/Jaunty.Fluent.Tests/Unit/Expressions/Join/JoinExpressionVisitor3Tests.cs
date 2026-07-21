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

    #region Self-Join (Same Entity Type for Multiple Parameters)

    [Fact]
    public void Visit_SelfJoin_TwoParametersOfSameType_ResolveToDistinctAliases()
    {
        // T1 and T2 are both Product - parameters must be matched by reference/position, not
        // by Type, or both p1.* and p2.* would resolve to the same (first) alias.
        Expression<Func<Product, Product, Category, bool>> expr =
            (p1, p2, c) => p1.SupplierId == p2.ProductId && p2.CategoryId == c.CategoryId;
        var visitor = new JoinExpressionVisitor3<Product, Product, Category>(_dialect, "p1", "p2", "c");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("p1.[supplier_id] = p2.[product_id]", sql);
        Assert.Contains("p2.[category_id] = c.[category_id]", sql);
    }

    #endregion

    #region Null Alias Fallback

    [Fact]
    public void Visit_NullAlias_UsesEscapedTableNameNotRawTableName()
    {
        // Regression test: same alias-less fallback bug as JoinExpressionVisitor, fixed the
        // same way - _dialect.EscapeTableName instead of the raw, unescaped table name.
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => c.CategoryId == 1;
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", null, "s");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("[categories].[category_id]", sql);
        Assert.DoesNotContain("categories.[category_id]", sql);
    }

    #endregion

    #region Unsupported Method Calls

    [Fact]
    public void Visit_MethodCall_ThrowsNotSupportedInsteadOfEmittingGarbageSql()
    {
        Expression<Func<Product, Category, Supplier, bool>> expr = (p, c, s) => s.Country.Contains("K");
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s");

        Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
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

    #region Self-Join (Same Entity Type for Multiple Parameters)

    [Fact]
    public void Visit_SelfJoin_TwoParametersOfSameType_ResolveToDistinctAliases()
    {
        // T1 and T3 are both Product - parameters must be matched by reference/position, not
        // by Type, or both p1.* and p3.* would resolve to the same (first) alias.
        Expression<Func<Product, Category, Product, Order, bool>> expr =
            (p1, c, p3, o) => p1.SupplierId == p3.ProductId && p3.CategoryId == c.CategoryId;
        var visitor = new JoinExpressionVisitor4<Product, Category, Product, Order>(_dialect, "p1", "c", "p3", "o");
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("p1.[supplier_id] = p3.[product_id]", sql);
        Assert.Contains("p3.[category_id] = c.[category_id]", sql);
    }

    #endregion

    #region Null Alias Fallback

    [Fact]
    public void Visit_NullAlias_UsesEscapedTableNameNotRawTableName()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) => o.OrderId == 1;
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", null);
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("[order_id]", sql);
        Assert.DoesNotContain("orders.[order_id]", sql);
    }

    #endregion

    #region Unsupported Method Calls

    [Fact]
    public void Visit_MethodCall_ThrowsNotSupportedInsteadOfEmittingGarbageSql()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> expr = (p, c, s, o) => s.Country.Contains("K");
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o");

        Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));
    }

    #endregion
}
