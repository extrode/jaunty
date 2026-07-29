using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

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

        Assert.Equal("([product_id] = @product_id)", sql);
        var param = Assert.Single(parameters, p => p.Name == "@product_id" && p.Value.Equals(1));
    }

    [Fact]
    public void Visit_InequalityComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductId != 1;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([product_id] <> @product_id)", sql);
        var param = Assert.Single(parameters, p => p.Name == "@product_id" && p.Value.Equals(1));
    }

    [Fact]
    public void Visit_LessThanComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice < 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([unit_price] < @unit_price)", sql);
        var param = Assert.Single(parameters, p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    [Fact]
    public void Visit_LessThanOrEqualComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice <= 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([unit_price] <= @unit_price)", sql);
        var param = Assert.Single(parameters, p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    [Fact]
    public void Visit_GreaterThanComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice > 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([unit_price] > @unit_price)", sql);
        var param = Assert.Single(parameters, p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    [Fact]
    public void Visit_GreaterThanOrEqualComparison_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitPrice >= 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([unit_price] >= @unit_price)", sql);
        var param = Assert.Single(parameters, p => p.Name == "@unit_price" && p.Value.Equals(10m));
    }

    #endregion

    #region Same-Entity Column-to-Column Comparisons

    // AUD-R12: a same-entity column-to-column comparison used to have an unbound
    // ParameterExpression on whichever side ExtractColumnAndValue treated as "the value" -
    // EvaluateExpression's Expression.Lambda(...).Compile() then threw InvalidOperationException
    // at runtime instead of producing valid SQL.

    [Fact]
    public void Visit_ColumnToColumnEquality_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == p.SupplierId;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([category_id] = [supplier_id])", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_ColumnToColumnInequality_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId != p.SupplierId;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([category_id] <> [supplier_id])", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_ColumnToColumnGreaterThan_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId > p.SupplierId;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([category_id] > [supplier_id])", sql);
        Assert.Empty(parameters);
    }

    // R16: a string.Length comparison against another column on the same entity had the same
    // unbound-ParameterExpression crash as AUD-R12's plain column-to-column case, because
    // TryGetColumnName deliberately excludes .Length (it's rendered as LENGTH(), not a column), so
    // the old both-sides-TryGetColumnName check never caught it and fell through to
    // ExtractColumnAndValue's EvaluateExpression.
    [Fact]
    public void Visit_LengthToColumnGreaterThan_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Length > p.UnitsInStock;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("(LEN([product_name]) > [units_in_stock])", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_ColumnToLengthLessThan_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.UnitsInStock < p.ProductName.Length;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([units_in_stock] < LEN([product_name]))", sql);
        Assert.Empty(parameters);
    }

    // R16: unlike the mixed Length-vs-column cases above, Length-vs-Length never actually crashed
    // - TryGetColumnName rejects .Length on both sides, so ExtractColumnAndValue's own both-sides
    // check fails first and it returns (null, null, false) without ever calling EvaluateExpression,
    // letting the pre-fix code fall through to the generic Visit()-based rendering that already
    // produced correct SQL. This is a format-consistency check for the new explicit VisitBinary
    // branch, not a regression test for a crash.
    [Fact]
    public void Visit_LengthToLengthGreaterThan_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Length > p.QuantityPerUnit!.Length;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("(LEN([product_name]) > LEN([quantity_per_unit]))", sql);
        Assert.Empty(parameters);
    }

    #endregion

    #region Null Comparisons

    [Fact]
    public void Visit_NullEquality_GeneratesIsNull()
    {
        Expression<Func<Product, bool>> expr = p => p.SupplierId == null;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([supplier_id] IS NULL)", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_NullInequality_GeneratesIsNotNull()
    {
        Expression<Func<Product, bool>> expr = p => p.SupplierId != null;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([supplier_id] IS NOT NULL)", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_NullOnRightSide_GeneratesIsNull()
    {
        Expression<Func<Product, bool>> expr = p => null == p.SupplierId;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([supplier_id] IS NULL)", sql);
        Assert.Empty(parameters);
    }

    #endregion

    #region Logical Operators (AND, OR)

    [Fact]
    public void Visit_AndAlso_CombinesWithAnd()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == 1 && p.Discontinued == false;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("(([category_id] = @category_id) AND ([discontinued] = @discontinued))", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Visit_OrElse_CombinesWithOr()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == 1 || p.CategoryId == 2;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("(([category_id] = @category_id) OR ([category_id] = @category_id2))", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Visit_ComplexBooleanExpression_GeneratesCorrectSql()
    {
        Expression<Func<Product, bool>> expr = p => (p.CategoryId == 1 && p.Discontinued == false) || p.UnitPrice > 100;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("AND", sql);
        Assert.Contains("OR", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void Visit_Not_NegatesCondition()
    {
        Expression<Func<Product, bool>> expr = p => !p.Discontinued;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("NOT ([discontinued] = 1)", sql);
        Assert.Empty(parameters);
    }

    #endregion

    #region Boolean Properties

    [Fact]
    public void Visit_BooleanProperty_GeneratesEqualsOne()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("[discontinued] = 1", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_BooleanPropertyEqualsTrue_GeneratesEqualsOne()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued == true;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([discontinued] = @discontinued)", sql);
        Assert.Single(parameters, p => p.Name == "@discontinued" && p.Value.Equals(true));
    }

    [Fact]
    public void Visit_BooleanPropertyEqualsFalse_GeneratesEqualsFalse()
    {
        Expression<Func<Product, bool>> expr = p => p.Discontinued == false;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("([discontinued] = @discontinued)", sql);
        Assert.Single(parameters, p => p.Name == "@discontinued" && p.Value.Equals(false));
    }

    #endregion

    #region String Methods

    [Fact]
    public void Visit_StringContains_GeneratesLike()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Contains("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters, p => p.Name == "@product_name" && p.Value.ToString()!.Contains("Chef"));
    }

    [Fact]
    public void Visit_StringStartsWith_GeneratesLike()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.StartsWith("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters, p => p.Name == "@product_name" && p.Value.ToString()!.StartsWith("Chef"));
    }

    [Fact]
    public void Visit_StringEndsWith_GeneratesLike()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.EndsWith("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters, p => p.Name == "@product_name" && p.Value.ToString()!.EndsWith("Chef"));
    }

    [Fact]
    public void Visit_StringEquals_GeneratesEquality()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Equals("Test");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("[product_name] = @product_name", sql);
        Assert.Single(parameters, p => p.Name == "@product_name" && p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_StringEqualsNull_GeneratesIsNull()
    {
        // AUD-R18: HandleStringEquals used to coerce a null comparison value into an empty
        // string ("" bound as the parameter), so p.Name.Equals(null) matched empty-string rows
        // instead of actual NULL rows - a legitimate, non-throwing .NET comparison that must
        // translate to IS NULL per SQL three-valued logic.
        Expression<Func<Product, bool>> expr = p => p.ProductName.Equals(null);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("[product_name] IS NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_StringEqualsIgnoreCase_GeneratesCaseInsensitiveComparison()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Equals("Test", StringComparison.OrdinalIgnoreCase);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LOWER", sql);
        Assert.Single(parameters, p => p.Name == "@product_name" && p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_ToUpper_GeneratesUpperFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.ToUpper() == "TEST";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("UPPER([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals("TEST"));
    }

    [Fact]
    public void Visit_ToLower_GeneratesLowerFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.ToLower() == "test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LOWER([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals("test"));
    }

    [Fact]
    public void Visit_Trim_GeneratesTrimFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Trim() == "Test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("TRIM([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_Substring_WithLength_GeneratesSubstringFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(0, 3) == "Tes";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("SUBSTRING([product_name], 1, 3)", sql);
        Assert.Single(parameters, p => p.Value.Equals("Tes"));
    }

    [Fact]
    public void Visit_Substring_WithoutLength_GeneratesSubstringFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(3) == "t";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        // AUD-R26: was 8000 - a SQL Server convention emitted from a dialect-neutral visitor, which
        // silently truncated anything longer. TestDialect predates ISubstringToEndDialect and does
        // not implement it, so this is the fallback path; SubstringToEndTests covers the native
        // per-dialect forms.
        Assert.Contains("SUBSTRING([product_name], 4, 2147483647)", sql);
        Assert.Single(parameters, p => p.Value.Equals("t"));
    }

    [Fact]
    public void Visit_StringLength_GeneratesLengthFunction()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Length > 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LEN([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals(10));
    }

    #endregion

    #region Sql Functions - Coalesce, IsNull, NullIf

    [Fact]
    public void Visit_SqlCoalesce_GeneratesCoalesceFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Coalesce(p.SupplierId, 0) > 0;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("COALESCE", sql);
        Assert.Contains("[supplier_id]", sql);
    }

    [Fact]
    public void Visit_SqlIsNull_GeneratesIsNullFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.IsNull(p.SupplierId, 0) > 0;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("CASE WHEN [supplier_id] IS NULL", sql);
    }

    [Fact]
    public void Visit_SqlNullIf_GeneratesNullIfFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.NullIf(p.Discontinued, false) == true;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("NULLIF", sql);
    }

    #endregion

    #region Sql Functions - String Functions

    [Fact]
    public void Visit_SqlLength_GeneratesLengthFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Length(p.ProductName) > 10;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LEN([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals(10));
    }

    [Fact]
    public void Visit_SqlUpper_GeneratesUpperFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Upper(p.ProductName) == "TEST";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("UPPER([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals("TEST"));
    }

    [Fact]
    public void Visit_SqlLower_GeneratesLowerFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Lower(p.ProductName) == "test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LOWER([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals("test"));
    }

    [Fact]
    public void Visit_SqlTrim_GeneratesTrimFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Trim(p.ProductName) == "Test";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("TRIM([product_name])", sql);
        Assert.Single(parameters, p => p.Value.Equals("Test"));
    }

    [Fact]
    public void Visit_SqlSubstring_WithLength_GeneratesSubstringFunction()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Substring(p.ProductName, 1, 3) == "Tes";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("SUBSTRING([product_name], @SqlFn, @SqlFn2)", sql);
        Assert.Contains(parameters, p => p.Value.Equals("Tes"));
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

        Assert.Contains("YEAR(", sql);
        Assert.Contains(parameters, p => p.Value.Equals(2020));
    }

    [Fact]
    public void Visit_SqlMonth_GeneratesMonthFunction()
    {
        var date = DateTime.Now;
        Expression<Func<Product, bool>> expr = p => Sql.Month(date) > 6;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("MONTH(", sql);
        Assert.Contains(parameters, p => p.Value.Equals(6));
    }

    [Fact]
    public void Visit_SqlDay_GeneratesDayFunction()
    {
        var date = DateTime.Now;
        Expression<Func<Product, bool>> expr = p => Sql.Day(date) > 15;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("DAY(", sql);
        Assert.Contains(parameters, p => p.Value.Equals(15));
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

        Assert.Contains("CASE", sql);
        Assert.Contains("WHEN", sql);
        Assert.Contains("THEN", sql);
        Assert.Contains("ELSE", sql);
        Assert.Contains("END", sql);
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

        Assert.Contains("CASE", sql);
        Assert.Contains("WHEN", sql);
        Assert.Contains("THEN", sql);
        Assert.Contains("END", sql);
    }

    // R16: a WHEN condition comparing string.Length to another column on the same entity hit the
    // same unbound-parameter crash as the plain WHERE case (see
    // Visit_LengthToColumnGreaterThan_GeneratesCorrectSql). A .When() condition argument is always
    // Quote-wrapped (it's an Expression<Func<T,bool>>-typed argument nested inside the outer
    // expression tree), so TranslateCaseCondition's own BinaryExpression branch never fires here -
    // ExpressionVisitor's base Quote/Lambda handling routes the body through VisitBinary instead,
    // so this is a coverage check confirming the VisitBinary fix also covers CASE WHEN conditions.
    [Fact]
    public void Visit_CaseExpression_WithLengthToColumnWhenCondition_GeneratesCaseStatement()
    {
        Expression<Func<Product, bool>> expr = p => Sql.Case<Product, int>()
            .When(x => x.ProductName.Length > x.UnitsInStock, 1)
            .Else(0) > 0;

        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("WHEN (LEN([product_name]) > [units_in_stock]) THEN", sql);
    }

    [Fact]
    public void Visit_CaseExpression_WithNullWhenCondition_GeneratesIsNull()
    {
        // AUD-R18: TranslateCaseCondition used to parameterize a WHEN condition compared to a
        // literal null as "col = @param" bound to NULL, which per SQL three-valued logic never
        // matches - it must emit IS NULL instead, mirroring VisitBinary's top-level null handling.
        Expression<Func<Product, bool>> expr = p => Sql.Case<Product, string>()
            .When(x => x.SupplierId == null, "NoSupplier")
            .Else("HasSupplier") == "NoSupplier";

        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("WHEN ([supplier_id] IS NULL) THEN", sql);
        Assert.DoesNotContain("@supplier_id", sql);
    }

    [Fact]
    public void Visit_CaseExpression_WithCompoundWhenCondition_DoesNotThrow()
    {
        // Regression test: a compound (AndAlso/OrElse) .When() condition used to be
        // reachable through TranslateCaseCondition's BinaryExpression fallback, which called
        // GetOperator(AndAlso/OrElse) and threw NotSupportedException.
        Expression<Func<Product, bool>> expr = p => Sql.Case<Product, int>()
            .When(x => x.UnitPrice < 10 && x.UnitsInStock > 0, 1)
            .Else(0) > 0;

        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, _) = visitor.Translate(expr);

        Assert.Contains("CASE", sql);
        Assert.Contains("AND", sql);
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

        Assert.Contains("IN", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void Visit_EnumerableContains_EmptyCollection_GeneratesFalse()
    {
        var ids = Array.Empty<int>();
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("1 = 0", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_EnumerableContains_SingleItem_GeneratesInClause()
    {
        var ids = new[] { 42 };
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("IN", sql);
        Assert.Single(parameters);
    }

    // List<T>.Contains/HashSet<T>.Contains bind to the type's own instance Contains method
    // rather than the Enumerable.Contains extension method, so they need separate handling.

    [Fact]
    public void Visit_ListContains_GeneratesInClause()
    {
        var ids = new List<int> { 1, 2, 3 };
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("IN", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void Visit_ListContains_EmptyCollection_GeneratesFalse()
    {
        var ids = new List<int>();
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Equal("1 = 0", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_HashSetContains_GeneratesInClause()
    {
        var ids = new HashSet<int> { 1, 2, 3 };
        Expression<Func<Product, bool>> expr = p => ids.Contains(p.ProductId);
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("IN", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void Visit_StringContains_StillGeneratesLike_NotInClause()
    {
        // Guards against the instance-Contains detection over-matching string.Contains,
        // which must keep going through the LIKE translation path, not the IN-clause path.
        Expression<Func<Product, bool>> expr = p => p.ProductName.Contains("Chef");
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("LIKE", sql);
        Assert.DoesNotContain(" IN (", sql);
    }

    // C# 14's first-class span conversions rebind an array receiver from Enumerable.Contains to
    // MemoryExtensions.Contains(ReadOnlySpan<T>, T). The repo is pinned to LangVersion 13, so
    // these trees are built by hand in the exact shape the C# 14 compiler emits (verified against
    // the .NET 10 SDK). A consumer compiling with C# 14 hands this shape to the shipped visitor
    // whatever Jaunty itself was built with, so the pin does not stand in for these tests.

    private static Expression<Func<Product, bool>> SpanContains<TValue>(TValue[] values, string property)
        where TValue : IEquatable<TValue>
    {
        var parameter = Expression.Parameter(typeof(Product), "p");
        var member = Expression.Property(parameter, property);
        var toSpan = typeof(ReadOnlySpan<TValue>).GetMethod("op_Implicit", new[] { typeof(TValue[]) })!;
        var contains = typeof(MemoryExtensions).GetMethods()
            .Single(m => m.Name == "Contains"
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType.IsGenericType
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
            .MakeGenericMethod(typeof(TValue));

        var body = Expression.Call(contains, Expression.Call(toSpan, Expression.Constant(values)), member);
        return Expression.Lambda<Func<Product, bool>>(body, parameter);
    }

    [Fact]
    public void Visit_SpanContains_GeneratesInClause()
    {
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(SpanContains(new[] { 1, 2, 3 }, nameof(Product.ProductId)));

        Assert.Contains("IN", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void Visit_SpanContains_SingleItem_GeneratesInClause()
    {
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(SpanContains(new[] { 42 }, nameof(Product.ProductId)));

        Assert.Contains("IN", sql);
        Assert.Single(parameters);
    }

    [Fact]
    public void Visit_SpanContains_EmptyCollection_GeneratesFalse()
    {
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(SpanContains(Array.Empty<int>(), nameof(Product.ProductId)));

        Assert.Equal("1 = 0", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Visit_SpanContains_StringArray_GeneratesInClause()
    {
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(SpanContains(new[] { "Chai", "Chang" }, nameof(Product.ProductName)));

        Assert.Contains("IN", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Visit_SpanContains_ProducesTheSameSqlAsEnumerableContains()
    {
        var ids = new[] { 1, 2, 3 };
        Expression<Func<Product, bool>> enumerableForm = p => ids.Contains(p.ProductId);

        var (spanSql, spanParameters) = new WhereExpressionVisitor<Product>(_dialect)
            .Translate(SpanContains(ids, nameof(Product.ProductId)));
        var (enumerableSql, enumerableParameters) = new WhereExpressionVisitor<Product>(_dialect)
            .Translate(enumerableForm);

        Assert.Equal(enumerableSql, spanSql);
        Assert.Equal(enumerableParameters.Select(p => p.Value), spanParameters.Select(p => p.Value));
    }

    [Fact]
    public void Visit_UnsupportedMethodOnTheEntity_ThrowsNotSupportedNamingTheMethod()
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Normalize() == "Chai";
        var visitor = new WhereExpressionVisitor<Product>(_dialect);

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr));

        Assert.Contains("Normalize", ex.Message);
    }

    #endregion

    #region Duplicate Parameter Handling

    [Fact]
    public void Visit_SameColumnMultipleTimes_GeneratesUniqueParameterNames()
    {
        Expression<Func<Product, bool>> expr = p => p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3;
        var visitor = new WhereExpressionVisitor<Product>(_dialect);
        var (sql, parameters) = visitor.Translate(expr);

        Assert.Contains("@category_id", sql);
        Assert.Contains("@category_id2", sql);
        Assert.Contains("@category_id3", sql);
        Assert.Equal(3, parameters.Count);
        Assert.Equal(parameters.Select(p => p.Name).Distinct().Count(), parameters.Select(p => p.Name).Count());
    }

    #endregion
}