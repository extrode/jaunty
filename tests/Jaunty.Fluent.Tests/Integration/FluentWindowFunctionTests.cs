using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Window Functions (ROW_NUMBER, RANK, DENSE_RANK, NTILE, etc.)
/// </summary>
public class FluentWindowFunctionTests : IDisposable
{
    private readonly Database _db;

    public FluentWindowFunctionTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // ROW_NUMBER() Tests
    // ==========================================

    [Fact]
    public void RowNumber_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                RowNum = Sql.RowNumber<Product>()
            });

        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("AS RowNum");
    }

    [Fact]
    public void RowNumber_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().OrderBy(x => x.UnitPrice)
            });

        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("AS RowNum");
    }

    [Fact]
    public void RowNumber_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                RowNum = Sql.RowNumber<Product>().PartitionBy(x => x.CategoryId)
            });

        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("AS RowNum");
    }

    [Fact]
    public void RowNumber_WithPartitionByAndOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>()
                    .PartitionBy(x => x.CategoryId)
                    .OrderBy(x => x.UnitPrice)
            });

        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("AS RowNum");
    }

    [Fact]
    public void RowNumber_WithOrderByDescending_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().OrderByDescending(x => x.UnitPrice)
            });

        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("DESC");
        sql.Should().Contain("AS RowNum");
    }

    // ==========================================
    // RANK() Tests
    // ==========================================

    [Fact]
    public void Rank_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                ProductRank = Sql.Rank<Product>()
            });

        sql.Should().Contain("RANK()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("AS ProductRank");
    }

    [Fact]
    public void Rank_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                PriceRank = Sql.Rank<Product>().OrderBy(x => x.UnitPrice)
            });

        sql.Should().Contain("RANK()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("AS PriceRank");
    }

    [Fact]
    public void Rank_WithPartitionByAndOrderByDesc_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                PriceRank = Sql.Rank<Product>()
                    .PartitionBy(x => x.CategoryId)
                    .OrderByDescending(x => x.UnitPrice)
            });

        sql.Should().Contain("RANK()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("DESC");
        sql.Should().Contain("AS PriceRank");
    }

    // ==========================================
    // DENSE_RANK() Tests
    // ==========================================

    [Fact]
    public void DenseRank_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                DenseRank = Sql.DenseRank<Product>()
            });

        sql.Should().Contain("DENSE_RANK()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("AS DenseRank");
    }

    [Fact]
    public void DenseRank_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                DenseRank = Sql.DenseRank<Product>().OrderBy(x => x.UnitPrice)
            });

        sql.Should().Contain("DENSE_RANK()");
        sql.Should().Contain("OVER");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("AS DenseRank");
    }

    // ==========================================
    // NTILE() Tests
    // ==========================================

    [Fact]
    public void NTile_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                Quartile = Sql.NTile<Product>(4)
            });

        sql.Should().Contain("NTILE(4)");
        sql.Should().Contain("OVER");
        sql.Should().Contain("AS Quartile");
    }

    [Fact]
    public void NTile_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                PriceQuartile = Sql.NTile<Product>(4).OrderBy(x => x.UnitPrice)
            });

        sql.Should().Contain("NTILE(4)");
        sql.Should().Contain("OVER");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("AS PriceQuartile");
    }

    // ==========================================
    // Window Aggregate Tests (SUM, AVG, COUNT, MIN, MAX with OVER)
    // ==========================================

    [Fact]
    public void Sum_Over_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RunningTotal = Sql.Sum<Product, decimal?>(p.UnitPrice).Over()
            });

        sql.Should().Contain("SUM(");
        sql.Should().Contain("OVER");
        sql.Should().Contain("AS RunningTotal");
    }

    [Fact]
    public void Sum_Over_WithOrderBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RunningTotal = Sql.Sum<Product, decimal?>(p.UnitPrice).Over().OrderBy(x => x.ProductName)
            });

        sql.Should().Contain("SUM(");
        sql.Should().Contain("OVER");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("AS RunningTotal");
    }

    [Fact]
    public void Avg_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                CategoryAvgPrice = Sql.Avg<Product, decimal?>(p.UnitPrice).Over().PartitionBy(x => x.CategoryId)
            });

        sql.Should().Contain("AVG(");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("AS CategoryAvgPrice");
    }

    [Fact]
    public void Count_Over_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                TotalCount = Sql.Count<Product>().Over()
            });

        sql.Should().Contain("COUNT(*)");
        sql.Should().Contain("OVER");
        sql.Should().Contain("AS TotalCount");
    }

    [Fact]
    public void Count_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                CategoryProductCount = Sql.Count<Product>().Over().PartitionBy(x => x.CategoryId)
            });

        sql.Should().Contain("COUNT(*)");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("AS CategoryProductCount");
    }

    [Fact]
    public void Min_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                CategoryMinPrice = Sql.Min<Product, decimal?>(p.UnitPrice).Over().PartitionBy(x => x.CategoryId)
            });

        sql.Should().Contain("MIN(");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("AS CategoryMinPrice");
    }

    [Fact]
    public void Max_Over_WithPartitionBy_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                CategoryMaxPrice = Sql.Max<Product, decimal?>(p.UnitPrice).Over().PartitionBy(x => x.CategoryId)
            });

        sql.Should().Contain("MAX(");
        sql.Should().Contain("OVER");
        sql.Should().Contain("PARTITION BY");
        sql.Should().Contain("AS CategoryMaxPrice");
    }

    // ==========================================
    // Combined Tests
    // ==========================================

    [Fact]
    public void MultipleWindowFunctions_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.CategoryId,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().PartitionBy(x => x.CategoryId).OrderBy(x => x.UnitPrice),
                PriceRank = Sql.Rank<Product>().PartitionBy(x => x.CategoryId).OrderByDescending(x => x.UnitPrice)
            });

        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("RANK()");
        sql.Should().Contain("AS RowNum");
        sql.Should().Contain("AS PriceRank");
        // Count OVER occurrences
        var overCount = sql.Split(new[] { "OVER" }, StringSplitOptions.None).Length - 1;
        overCount.Should().Be(2);
    }

    [Fact]
    public void WindowFunction_WithWhere_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice,
                RowNum = Sql.RowNumber<Product>().OrderBy(x => x.UnitPrice)
            });

        sql.Should().Contain("SELECT");
        sql.Should().Contain("ROW_NUMBER()");
        sql.Should().Contain("FROM");
        sql.Should().Contain("WHERE");
    }

    // ==========================================
    // Entity Property Only Selection Tests
    // ==========================================

    [Fact]
    public void ProjectionWithOnlyEntityProperties_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .ToSql(p => new
            {
                p.ProductName,
                p.UnitPrice
            });

        sql.Should().Contain("SELECT");
        sql.Should().Contain("FROM");
        // Column name (product_name) differs from property name (ProductName), so alias is added
        sql.Should().Contain("AS ProductName");
        sql.Should().Contain("AS UnitPrice");
    }
}
