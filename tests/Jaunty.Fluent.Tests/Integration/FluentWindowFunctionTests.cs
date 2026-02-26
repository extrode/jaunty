using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Window Functions (ROW_NUMBER, RANK, DENSE_RANK, NTILE, etc.)
/// </summary>
public class FluentWindowFunctionTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentWindowFunctionTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // ROW_NUMBER() Tests
    // ==========================================

    [Fact]
    public void RowNumber_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                RowNum = Sql.RowNumber<Product>()
            });

        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("AS RowNum", sql);
    }

    [Fact]
    public void RowNumber_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().OrderBy(x => x.UnitPrice)
            });

        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("AS RowNum", sql);
    }

    [Fact]
    public void RowNumber_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                RowNum = Sql.RowNumber<Product>().PartitionBy(x => x.CategoryId)
            });

        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("AS RowNum", sql);
    }

    [Fact]
    public void RowNumber_WithPartitionByAndOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>()
                    .PartitionBy(x => x.CategoryId)
                    .OrderBy(x => x.UnitPrice)
            });

        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("AS RowNum", sql);
    }

    [Fact]
    public void RowNumber_WithOrderByDescending_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().OrderByDescending(x => x.UnitPrice)
            });

        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("AS RowNum", sql);
    }

    // ==========================================
    // RANK() Tests
    // ==========================================

    [Fact]
    public void Rank_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                ProductRank = Sql.Rank<Product>()
            });

        Assert.Contains("RANK()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("AS ProductRank", sql);
    }

    [Fact]
    public void Rank_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                PriceRank = Sql.Rank<Product>().OrderBy(x => x.UnitPrice)
            });

        Assert.Contains("RANK()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("AS PriceRank", sql);
    }

    [Fact]
    public void Rank_WithPartitionByAndOrderByDesc_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                PriceRank = Sql.Rank<Product>()
                    .PartitionBy(x => x.CategoryId)
                    .OrderByDescending(x => x.UnitPrice)
            });

        Assert.Contains("RANK()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("AS PriceRank", sql);
    }

    // ==========================================
    // DENSE_RANK() Tests
    // ==========================================

    [Fact]
    public void DenseRank_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                DenseRank = Sql.DenseRank<Product>()
            });

        Assert.Contains("DENSE_RANK()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("AS DenseRank", sql);
    }

    [Fact]
    public void DenseRank_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                DenseRank = Sql.DenseRank<Product>().OrderBy(x => x.UnitPrice)
            });

        Assert.Contains("DENSE_RANK()", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("AS DenseRank", sql);
    }

    // ==========================================
    // NTILE() Tests
    // ==========================================

    [Fact]
    public void NTile_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                Quartile = Sql.NTile<Product>(4)
            });

        Assert.Contains("NTILE(4)", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("AS Quartile", sql);
    }

    [Fact]
    public void NTile_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                PriceQuartile = Sql.NTile<Product>(4).OrderBy(x => x.UnitPrice)
            });

        Assert.Contains("NTILE(4)", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("AS PriceQuartile", sql);
    }

    // ==========================================
    // Window Aggregate Tests (SUM, AVG, COUNT, MIN, MAX with OVER)
    // ==========================================

    [Fact]
    public void Sum_Over_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RunningTotal = Sql.Sum<Product, decimal?>(p.UnitPrice).Over()
            });

        Assert.Contains("SUM(", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("AS RunningTotal", sql);
    }

    [Fact]
    public void Sum_Over_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RunningTotal = Sql.Sum<Product, decimal?>(p.UnitPrice).Over().OrderBy(x => x.ProductName)
            });

        Assert.Contains("SUM(", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("AS RunningTotal", sql);
    }

    [Fact]
    public void Avg_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                CategoryAvgPrice = Sql.Avg<Product, decimal?>(p.UnitPrice).Over().PartitionBy(x => x.CategoryId)
            });

        Assert.Contains("AVG(", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("AS CategoryAvgPrice", sql);
    }

    [Fact]
    public void Count_Over_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                TotalCount = Sql.Count<Product>().Over()
            });

        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("AS TotalCount", sql);
    }

    [Fact]
    public void Count_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                CategoryProductCount = Sql.Count<Product>().Over().PartitionBy(x => x.CategoryId)
            });

        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("AS CategoryProductCount", sql);
    }

    [Fact]
    public void Min_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                CategoryMinPrice = Sql.Min<Product, decimal?>(p.UnitPrice).Over().PartitionBy(x => x.CategoryId)
            });

        Assert.Contains("MIN(", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("AS CategoryMinPrice", sql);
    }

    [Fact]
    public void Max_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                CategoryMaxPrice = Sql.Max<Product, decimal?>(p.UnitPrice).Over().PartitionBy(x => x.CategoryId)
            });

        Assert.Contains("MAX(", sql);
        Assert.Contains("OVER", sql);
        Assert.Contains("PARTITION BY", sql);
        Assert.Contains("AS CategoryMaxPrice", sql);
    }

    // ==========================================
    // Combined Tests
    // ==========================================

    [Fact]
    public void MultipleWindowFunctions_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().PartitionBy(x => x.CategoryId).OrderBy(x => x.UnitPrice),
                PriceRank = Sql.Rank<Product>().PartitionBy(x => x.CategoryId).OrderByDescending(x => x.UnitPrice)
            });

        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("RANK()", sql);
        Assert.Contains("AS RowNum", sql);
        Assert.Contains("AS PriceRank", sql);
        // Count OVER occurrences
        var overCount = sql.Split(new[] { "OVER" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(2, overCount);
    }

    [Fact]
    public void WindowFunction_WithWhere_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().OrderBy(x => x.UnitPrice)
            });

        Assert.Contains("SELECT", sql);
        Assert.Contains("ROW_NUMBER()", sql);
        Assert.Contains("FROM", sql);
        Assert.Contains("WHERE", sql);
    }

    // ==========================================
    // Entity Property Only Selection Tests
    // ==========================================

    [Fact]
    public void ProjectionWithOnlyEntityProperties_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice
            });

        Assert.Contains("SELECT", sql);
        Assert.Contains("FROM", sql);
        // Column name (product_name) differed from property name (ProductName), so alias is added
        Assert.Contains("AS ProductName", sql);
        Assert.Contains("AS UnitPrice", sql);
    }
}
