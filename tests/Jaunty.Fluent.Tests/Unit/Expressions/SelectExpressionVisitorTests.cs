using System.Linq.Expressions;

using FluentAssertions;

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

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Be("[product_name]");
        columns[0].Alias.Should().Be("ProductName");
    }

    [Fact]
    public void Visit_MultipleProperties_GeneratesAllColumns()
    {
        Expression<Func<Product, object>> expr = p => new { p.ProductName, p.UnitPrice };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().HaveCount(2);
        columns[0].Sql.Should().Be("[product_name]");
        columns[1].Sql.Should().Be("[unit_price]");
    }

    [Fact]
    public void Visit_WithColumnAttribute_GeneratesEscapedColumnName()
    {
        Expression<Func<Product, object>> expr = p => p.ProductId;
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Be("[product_id]");
    }

    #endregion

    #region Anonymous Type Projections

    [Fact]
    public void Visit_AnonymousType_WithNamedProperties_GeneratesAliases()
    {
        Expression<Func<Product, object>> expr = p => new { Name = p.ProductName, Price = p.UnitPrice };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().HaveCount(2);
        columns[0].Alias.Should().Be("Name");
        columns[1].Alias.Should().Be("Price");
    }

    [Fact]
    public void Visit_AnonymousType_WithSqlFunctions_GeneratesFunctionCalls()
    {
        Expression<Func<Product, object>> expr = p => new { p.ProductName, Length = Sql.Length(p.ProductName) };
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().HaveCount(2);
        columns[1].Sql.Should().Contain("LEN");
        columns[1].Alias.Should().Be("Length");
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

        columns.Should().HaveCount(2);
        columns[0].Alias.Should().Be("Name");
        columns[1].Alias.Should().Be("Price");
    }

    #endregion

    #region SQL Functions in Projections

    [Fact]
    public void Visit_SqlLength_GeneratesLengthFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Length(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("LEN([product_name])");
    }

    [Fact]
    public void Visit_SqlUpper_GeneratesUpperFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Upper(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("UPPER([product_name])");
    }

    [Fact]
    public void Visit_SqlLower_GeneratesLowerFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Lower(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("LOWER([product_name])");
    }

    [Fact]
    public void Visit_SqlTrim_GeneratesTrimFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Trim(p.ProductName);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("TRIM([product_name])");
    }

    [Fact]
    public void Visit_SqlSubstring_GeneratesSubstringFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Substring(p.ProductName, 1, 5);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("SUBSTRING([product_name], 1, 5)");
    }

    [Fact]
    public void Visit_SqlCoalesce_GeneratesCoalesceFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Coalesce(p.SupplierId, 0);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("COALESCE");
    }

    [Fact]
    public void Visit_SqlIsNull_GeneratesIsNullFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.IsNull(p.SupplierId, 0);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("CASE WHEN [supplier_id] IS NULL");
    }

    [Fact]
    public void Visit_SqlNullIf_GeneratesNullIfFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.NullIf(p.Discontinued, false);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("NULLIF");
    }

    #endregion

    #region Window Functions

    [Fact]
    public void Visit_SqlRowNumber_GeneratesRowNumberFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("ROW_NUMBER() OVER ()");
    }

    [Fact]
    public void Visit_SqlRank_GeneratesRankFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Rank<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("RANK() OVER ()");
    }

    [Fact]
    public void Visit_SqlDenseRank_GeneratesDenseRankFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.DenseRank<Product>();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("DENSE_RANK() OVER ()");
    }

    [Fact]
    public void Visit_WindowBuilder_WithPartitionBy_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>().PartitionBy(p => p.ProductId);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("ROW_NUMBER() OVER (PARTITION BY [product_id])");
    }

    [Fact]
    public void Visit_WindowBuilder_WithOrderBy_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>().OrderBy(p => p.UnitPrice ?? 0m);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("ROW_NUMBER() OVER (ORDER BY CASE WHEN [unit_price] IS NULL THEN 0 ELSE [unit_price] END ASC)");
    }

    [Fact]
    public void Visit_WindowBuilder_WithPartitionAndOrderBy_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.RowNumber<Product>()
            .PartitionBy(p => p.ProductId)
            .OrderBy(p => p.UnitPrice ?? 0m);
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("ROW_NUMBER() OVER (PARTITION BY [product_id] ORDER BY CASE WHEN [unit_price] IS NULL THEN 0 ELSE [unit_price] END ASC)");
    }

    #endregion

    #region Aggregate Window Functions

    [Fact]
    public void Visit_WindowAggregate_Sum_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Sum<Product, decimal?>(p.UnitPrice).Over();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("SUM([unit_price]) OVER ()");
    }

    [Fact]
    public void Visit_WindowAggregate_Avg_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Avg<Product, decimal?>(p.UnitPrice).Over();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("AVG([unit_price]) OVER ()");
    }

    [Fact]
    public void Visit_WindowAggregate_Count_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Count<Product>().Over();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("COUNT(*) OVER ()");
    }

    [Fact]
    public void Visit_WindowAggregate_Min_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Min<Product, decimal?>(p.UnitPrice).Over();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("MIN([unit_price]) OVER ()");
    }

    [Fact]
    public void Visit_WindowAggregate_Max_GeneratesWindowFunction()
    {
        Expression<Func<Product, object>> expr = p => Sql.Max<Product, decimal?>(p.UnitPrice).Over();
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("MAX([unit_price]) OVER ()");
    }

    #endregion

    #region Constant Values

    [Fact]
    public void Visit_ConstantString_GeneratesQuotedValue()
    {
        Expression<Func<Product, object>> expr = p => "Constant";
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Contain("Constant");
    }

    [Fact]
    public void Visit_ConstantInt_GeneratesValue()
    {
        Expression<Func<Product, object>> expr = p => 42;
        var visitor = new SelectExpressionVisitor<Product>(_dialect);
        var columns = visitor.Translate(expr);

        columns.Should().ContainSingle();
        columns[0].Sql.Should().Be("42");
    }

    #endregion
}
