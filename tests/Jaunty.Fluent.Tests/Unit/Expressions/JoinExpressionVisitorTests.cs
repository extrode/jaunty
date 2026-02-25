using System.Linq.Expressions;

using FluentAssertions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for JoinExpressionVisitor.
/// Tests SQL ON clause generation from lambda expressions.
/// </summary>
public class JoinExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();

    #region Simple Join Conditions

    [Fact]
    public void Visit_EqualityJoin_GeneratesOnClause()
    {
        Expression<Func<Product, Category, bool>> expr = (p, c) => p.CategoryId == c.CategoryId;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");
        var sql = visitor.Translate(expr);

        sql.Should().Contain("[category_id]");
        sql.Should().Contain("=");
    }

    [Fact]
    public void Visit_AndAlso_CombinesWithAnd()
    {
        Expression<Func<Product, Category, bool>> expr = (p, c) => p.CategoryId == c.CategoryId && c.CategoryName == "Beverages";
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");
        var sql = visitor.Translate(expr);

        sql.Should().Contain("AND");
        sql.Should().Contain("p.[category_id] = c.[category_id]");
        sql.Should().Contain("c.[category_name] = 'Beverages'");
    }

    [Fact]
    public void Visit_OrElse_CombinesWithOr()
    {
        Expression<Func<Product, Category, bool>> expr = (p, c) => p.CategoryId == c.CategoryId || p.ProductId == c.CategoryId;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");
        var sql = visitor.Translate(expr);

        sql.Should().Contain("OR");
    }

    #endregion

    #region Column Name Escaping

    [Fact]
    public void Visit_WithColumnAttributes_GeneratesEscapedNames()
    {
        Expression<Func<Product, Category, bool>> expr = (p, c) => p.ProductId == c.CategoryId;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "prod", "cat");
        var sql = visitor.Translate(expr);

        sql.Should().Contain("[product_id]");
        sql.Should().Contain("[category_id]");
    }

    #endregion

    #region Complex Join Conditions

    [Fact]
    public void Visit_ComplexJoinCondition_GeneratesCorrectSql()
    {
        Expression<Func<Product, Category, bool>> expr = (p, c) => 
            p.CategoryId == c.CategoryId && 
            p.Discontinued == false;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");
        var sql = visitor.Translate(expr);

        sql.Should().Contain("AND");
        sql.Should().Contain("[category_id]");
    }

    #endregion

    #region Inequality Joins

    [Fact]
    public void Visit_InequalityJoin_GeneratesNotEqual()
    {
        Expression<Func<Product, Category, bool>> expr = (p, c) => p.ProductId != c.CategoryId;
        var visitor = new JoinExpressionVisitor<Product, Category>(_dialect, "p", "c");
        var sql = visitor.Translate(expr);

        sql.Should().Contain("<>");
    }

    #endregion
}
