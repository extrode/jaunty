using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// Unit tests for SelectExpressionVisitor.
/// Tests SQL projection generation from lambda expressions.
/// </summary>
public class SelectExpressionVisitorTests
{
    private readonly TestDialect _dialect = new();

    #region Simple Property Selection

    [Fact]
    public void Visit_SingleProperty_GeneratesColumnName()
    {
        Expression<Func<Product, object>> expr = p => p.ProductName;
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Equal("[product_name]", column.Sql);
        Assert.Equal("ProductName", column.Alias);
    }

    [Fact]
    public void Visit_MultipleProperties_GeneratesAllColumns()
    {
        Expression<Func<Product, object>> expr = p => new { p.ProductName, p.UnitPrice };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        Assert.Equal(2, columns.Count);
        Assert.Equal("[product_name]", columns[0].Sql);
        Assert.Equal("[unit_price]", columns[1].Sql);
    }

    [Fact]
    public void Visit_WithColumnAttribute_GeneratesEscapedColumnName()
    {
        Expression<Func<Product, object>> expr = p => p.ProductId;
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Equal("[product_id]", column.Sql);
    }

    #endregion

    #region Anonymous Type Projections

    [Fact]
    public void Visit_AnonymousType_WithNamedProperties_GeneratesAliases()
    {
        Expression<Func<Product, object>> expr = p => new { Name = p.ProductName, Price = p.UnitPrice };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        Assert.Equal(2, columns.Count);
        Assert.Equal("Name", columns[0].Alias);
        Assert.Equal("Price", columns[1].Alias);
    }

    [Fact]
    public void Visit_AnonymousType_WithSqlFunctions_GeneratesFunctionCalls()
    {
        Expression<Func<Product, object>> expr = p => new { p.ProductName, Length = Sql.Length(p.ProductName) };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        Assert.Equal(2, columns.Count);
        Assert.Contains("LEN", columns[1].Sql);
        Assert.Equal("Length", columns[1].Alias);
    }

    #endregion

    #region DTO Projections (MemberInit)

    [Fact]
    public void Visit_MemberInit_WithDtoProperties_GeneratesColumns()
    {
        Expression<Func<Product, ProductDto>> expr = p => new ProductDto
        {
            Name = p.ProductName,
            Price = p.UnitPrice
        };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        Assert.Equal(2, columns.Count);
        Assert.Equal("Name", columns[0].Alias);
        Assert.Equal("Price", columns[1].Alias);
    }

    #endregion

    #region SQL Functions in Projections

    [Fact]
    public void Visit_SqlLength_GeneratesLengthFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Length(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("LEN([product_name])", column.Sql);
    }

    [Fact]
    public void Visit_SqlUpper_GeneratesUpperFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Upper(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("UPPER([product_name])", column.Sql);
    }

    [Fact]
    public void Visit_SqlLower_GeneratesLowerFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Lower(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("LOWER([product_name])", column.Sql);
    }

    [Fact]
    public void Visit_SqlTrim_GeneratesTrimFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Trim(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("TRIM([product_name])", column.Sql);
    }

    [Fact]
    public void Visit_SqlSubstring_GeneratesSubstringFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Substring(p.ProductName, 1, 5);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("SUBSTRING([product_name], 1, 5)", column.Sql);
    }

    [Fact]
    public void Visit_SqlCoalesce_GeneratesCoalesceFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Coalesce(p.SupplierId, 0);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("COALESCE", column.Sql);
    }

    [Fact]
    public void Visit_SqlIsNull_GeneratesIsNullFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.IsNull(p.SupplierId, 0);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("CASE WHEN [supplier_id] IS NULL", column.Sql);
    }

    [Fact]
    public void Visit_SqlNullIf_GeneratesNullIfFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.NullIf(p.Discontinued, false);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("NULLIF", column.Sql);
    }

    #endregion

    #region Window Functions

    [Fact]
    public void Visit_SqlRowNumber_GeneratesRowNumberFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("ROW_NUMBER()", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_SqlRank_GeneratesRankFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Rank<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("RANK()", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_SqlDenseRank_GeneratesDenseRankFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.DenseRank<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("DENSE_RANK()", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_WindowBuilder_WithPartitionBy_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>().PartitionBy(p => p.ProductId);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("ROW_NUMBER()", column.Sql);
        Assert.Contains("OVER", column.Sql);
        Assert.Contains("PARTITION BY", column.Sql);
        Assert.Contains("product_id", column.Sql);
    }

    [Fact]
    public void Visit_WindowBuilder_WithOrderBy_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>().OrderBy(p => p.UnitPrice ?? 0m);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("ROW_NUMBER()", column.Sql);
        Assert.Contains("OVER", column.Sql);
        Assert.Contains("ORDER BY", column.Sql);
        Assert.Contains("unit_price", column.Sql);
    }

    [Fact]
    public void Visit_WindowBuilder_WithPartitionAndOrderBy_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>()
            .PartitionBy(p => p.ProductId)
            .OrderBy(p => p.UnitPrice ?? 0m);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("ROW_NUMBER()", column.Sql);
        Assert.Contains("OVER", column.Sql);
        Assert.Contains("PARTITION BY", column.Sql);
        Assert.Contains("ORDER BY", column.Sql);
    }

    #endregion

    #region Aggregate Window Functions

    [Fact]
    public void Visit_WindowAggregate_Sum_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Sum<Product, decimal?>(p.UnitPrice);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("SUM", column.Sql);
        Assert.Contains("unit_price", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_WindowAggregate_Avg_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Avg<Product, decimal?>(p.UnitPrice);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("AVG", column.Sql);
        Assert.Contains("unit_price", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_WindowAggregate_Count_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Count<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("COUNT", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_WindowAggregate_Min_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Min<Product, decimal?>(p.UnitPrice);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("MIN", column.Sql);
        Assert.Contains("unit_price", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    [Fact]
    public void Visit_WindowAggregate_Max_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Max<Product, decimal?>(p.UnitPrice);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("MAX", column.Sql);
        Assert.Contains("unit_price", column.Sql);
        Assert.Contains("OVER", column.Sql);
    }

    #endregion

    #region Constant Values

    [Fact]
    public void Visit_ConstantString_GeneratesQuotedValue()
    {
        Expression<Func<Product, object>> expr = p => "Constant";
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Contains("Constant", column.Sql);
    }

    [Fact]
    public void Visit_ConstantInt_GeneratesValue()
    {
        Expression<Func<Product, object>> expr = p => 42;
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        var column = Assert.Single(columns);
        Assert.Equal("42", column.Sql);
    }

    #endregion
}
