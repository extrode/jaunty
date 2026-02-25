using System.Linq.Expressions;

using FluentAssertions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for WhereExpressionVisitor.
/// Tests SQL generation from lambda expressions without database dependencies.
/// </summary>
public class WhereExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();

    #region Equality and Comparison Operators

    [Fact]
    public void Visit_EqualityComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductId == 1;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([product_id] = @product_id)");
        parameters.Should().ContainSingle(p => p.Name == "@product_id" && p.Value.Equals(1));
    }

    [Fact]
    public void Visit_InequalityComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductId != 1;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([product_id] <> @product_id)");
        parameters.Should().ContainSingle(p => p.Name == "@product_id" && p.Value.Equals(1));
    }

    [Fact]
    public void Visit_LessThanComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice < 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([unit_price] < @unit_price)");
        parameters.Should().ContainSingle(p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    [Fact]
    public void Visit_LessThanOrEqualComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice <= 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([unit_price] <= @unit_price)");
        parameters.Should().ContainSingle(p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    [Fact]
    public void Visit_GreaterThanComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice > 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([unit_price] > @unit_price)");
        parameters.Should().ContainSingle(p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    [Fact]
    public void Visit_GreaterThanOrEqualComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice >= 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([unit_price] >= @unit_price)");
        parameters.Should().ContainSingle(p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    #endregion

    #region Null Comparisons

    [Fact]
    public void Visit_NullEquality_GeneratesIsNull()
    {
        Expression<Func<Product, bool>> expr = p => p.SupplierId == null;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([supplier_id] IS NULL)");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void Visit_NullInequality_GeneratesIsNotNull()
    {
        Expression<Func<Product, bool>> expr = p => p.SupplierId != null;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([supplier_id] IS NOT NULL)");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void Visit_NullOnRightSide_GeneratesIsNull()
    {
        Expression<Func<Product, bool>> expr = p => null == p.SupplierId;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([supplier_id] IS NULL)");
        parameters.Should().BeEmpty();
    }

    #endregion

    #region Logical Operators (AND, OR)

    [Fact]
    public void Visit_AndAlso_CombinesWithAnd()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == 1 && p.Discontinued == false;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("(([category_id] = @category_id) AND ([discontinued] = @discontinued))");
        parameters.Should().HaveCount(2);
    }

    [Fact]
    public void Visit_OrElse_CombinesWithOr()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == 1 || p.CategoryId == 2;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("(([category_id] = @category_id) OR ([category_id] = @category_id2))");
        parameters.Should().HaveCount(2);
    }

    [Fact]
    public void Visit_ComplexBooleanExpression_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => (p.CategoryId == 1 && p.Discontinued == false) || p.UnitPrice > 100;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("AND");
        sql.Should().Contain("OR");
        parameters.Should().HaveCount(3);
    }

    [Fact]
    public void Visit_Not_NegatesCondition()
    {
        Expression<Func<Product, bool>> expr = p => !p.Discontinued;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("NOT ([discontinued] = 1)");
        parameters.Should().BeEmpty();
    }

    #endregion

    #region Boolean Properties

    [Fact]
    public void Visit_BooleanProperty_GeneratesEqualsOne()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("[discontinued] = 1");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void Visit_BooleanPropertyEqualsTrue_GeneratesEqualsOne()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued == true;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([discontinued] = @discontinued)");
        parameters.Should().ContainSingle(p => p.Name == "@discontinued" && p.Value.Equals(true));
    }

    [Fact]
    public void Visit_BooleanPropertyEqualsFalse_GeneratesEqualsFalse()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued == false;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("([discontinued] = @discontinued)");
        parameters.Should().ContainSingle(p => p.Name == "@discontinued" && p.Value.Equals(false));
    }

    #endregion

    #region String Methods

    [Fact]
    public void Visit_StringContains_GeneratesLike()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Contains("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LIKE");
        parameters.Should().ContainSingle(p => p.Name == "@product_name" && p.Value.ToString()!.Contains("Chef"));
    }

    [Fact]
    public void Visit_StringStartsWith_GeneratesLike()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.StartsWith("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LIKE");
        parameters.Should().ContainSingle(p => p.Name == "@product_name" && p.Value.ToString()!.StartsWith("Chef"));
    }

    [Fact]
    public void Visit_StringEndsWith_GeneratesLike()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.EndsWith("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LIKE");
        parameters.Should().ContainSingle(p => p.Name == "@product_name" && p.Value.ToString()!.EndsWith("Chef"));
    }

    [Fact]
    public void Visit_StringEquals_GeneratesEquality()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Equals("Test");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("[product_name] = @product_name");
        parameters.Should().ContainSingle(p => p.Name == "@product_name" && p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_StringEqualsIgnoreCase_GeneratesCaseInsensitiveComparison()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Equals("Test", StringComparison.OrdinalIgnoreCase);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LOWER");
        parameters.Should().ContainSingle(p => p.Name == "@product_name" && p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_ToUpper_GeneratesUpperFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.ToUpper() == "TEST";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("UPPER([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals("TEST"));
    }

    [Fact]
    public void Visit_ToLower_GeneratesLowerFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.ToLower() == "test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LOWER([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals("test"));
    }

    [Fact]
    public void Visit_Trim_GeneratesTrimFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Trim() == "Test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("TRIM([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_Substring_WithLength_GeneratesSubstringFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(0, 3) == "Tes";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("SUBSTRING([product_name], 1, 3)");
        parameters.Should().ContainSingle(p => p.Value.Equals("Tes"));
    }

    [Fact]
    public void Visit_Substring_WithoutLength_GeneratesSubstringFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(3) == "t";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("SUBSTRING([product_name], 4, 8000)");
        parameters.Should().ContainSingle(p => p.Value.Equals("t"));
    }

    [Fact]
    public void Visit_StringLength_GeneratesLengthFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Length > 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LEN([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals(10));
    }

    #endregion

    #region Sql Functions - Coalesce, IsNull, NullIf

    [Fact]
    public void Visit_SqlCoalesce_GeneratesCoalesceFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Coalesce(p.SupplierId, 0) > 0;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("COALESCE");
        sql.Should().Contain("[supplier_id]");
    }

    [Fact]
    public void Visit_SqlIsNull_GeneratesIsNullFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.IsNull(p.SupplierId, 0) > 0;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("CASE WHEN [supplier_id] IS NULL");
    }

    [Fact]
    public void Visit_SqlNullIf_GeneratesNullIfFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.NullIf(p.Discontinued, false) == true;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("NULLIF");
    }

    #endregion

    #region Sql Functions - String Functions

    [Fact]
    public void Visit_SqlLength_GeneratesLengthFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Length(p.ProductName) > 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LEN([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals(10));
    }

    [Fact]
    public void Visit_SqlUpper_GeneratesUpperFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Upper(p.ProductName) == "TEST";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("UPPER([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals("TEST"));
    }

    [Fact]
    public void Visit_SqlLower_GeneratesLowerFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Lower(p.ProductName) == "test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("LOWER([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals("test"));
    }

    [Fact]
    public void Visit_SqlTrim_GeneratesTrimFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Trim(p.ProductName) == "Test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("TRIM([product_name])");
        parameters.Should().ContainSingle(p => p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_SqlSubstring_WithLength_GeneratesSubstringFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Substring(p.ProductName, 1, 3) == "Tes";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("SUBSTRING([product_name], @SqlFn, @SqlFn2)");
        parameters.Should().Contain(p => p.Value.Equals("Tes"));
    }

    #endregion

    #region Sql Functions - Date Functions

    [Fact]
    public void Visit_SqlYear_GeneratesYearFunction()
    {
        var date = DateTime.Now;
        Expression<Func<Product, bool>> expr = p => Sql.Year(date) > 2020;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("YEAR(");
        parameters.Should().Contain(p => p.Value.Equals(2020));
    }

    [Fact]
    public void Visit_SqlMonth_GeneratesMonthFunction()
    {
        var date = DateTime.Now;
        Expression<Func<Product, bool>> expr = p => Sql.Month(date) > 6;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("MONTH(");
        parameters.Should().Contain(p => p.Value.Equals(6));
    }

    [Fact]
    public void Visit_SqlDay_GeneratesDayFunction()
    {
        var date = DateTime.Now;
        Expression<Func<Product, bool>> expr = p => Sql.Day(date) > 15;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("DAY(");
        parameters.Should().Contain(p => p.Value.Equals(15));
    }

    #endregion

    #region CASE Expressions

    [Fact]
    public void Visit_CaseExpression_WithWhenElse_GeneratesCaseStatement()
    {
        // Test that Sql.Case followed by When/Else/End generates CASE statement
        Expression<Func<Product, bool>> expr = p => Sql.Case<Product, int>()
            .When(x => x.UnitPrice < 10, 1)
            .When(x => x.UnitPrice < 100, 2)
            .Else(3) > 1;
        
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("CASE");
        sql.Should().Contain("WHEN");
        sql.Should().Contain("THEN");
        sql.Should().Contain("ELSE");
        sql.Should().Contain("END");
    }

    [Fact]
    public void Visit_CaseExpression_WithoutElse_GeneratesCaseStatement()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Case<Product, int>()
            .When(x => x.UnitPrice < 10, 1)
            .When(x => x.UnitPrice < 100, 2)
            .End() > 0;
        
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("CASE");
        sql.Should().Contain("WHEN");
        sql.Should().Contain("THEN");
        sql.Should().Contain("END");
    }

    #endregion

    #region Enumerable.Contains (IN clause)

    [Fact]
    public void Visit_EnumerableContains_GeneratesInClause()
    {
        var ids = new[] { 1, 2, 3 };
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("IN");
        parameters.Should().HaveCount(3);
    }

    [Fact]
    public void Visit_EnumerableContains_EmptyCollection_GeneratesFalse()
    {
        var ids = Array.Empty<int>();
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Be("1 = 0");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void Visit_EnumerableContains_SingleItem_GeneratesInClause()
    {
        var ids = new[] { 42 };
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("IN");
        parameters.Should().ContainSingle();
    }

    #endregion

    #region Duplicate Parameter Handling

    [Fact]
    public void Visit_SameColumnMultipleTimes_GeneratesUniqueParameterNames()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        sql.Should().Contain("@category_id");
        sql.Should().Contain("@category_id2");
        sql.Should().Contain("@category_id3");
        parameters.Should().HaveCount(3);
        parameters.Select(p => p.Name).Should().OnlyHaveUniqueItems();
    }

    #endregion
}
