using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Sql.Year(), Sql.Month(), Sql.Day() functions.
/// Note: The Northwind SQLite database uses a non-standard date format (YYYY/M/D)
/// which doesn't work with SQLite's strftime. These tests focus on SQL generation
/// rather than actual query execution. The generated SQL would work correctly
/// with properly formatted dates (ISO 8601 format).
/// </summary>
public class FluentDateFunctionsTests : IDisposable
{
    private readonly Database _db;

    public FluentDateFunctionsTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // Sql.Year Tests - SQL Generation
    // ==========================================

    [Fact]
    public void Year_ToSql_GeneratesYearFunction()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .ToSql();

        // SQLite uses strftime with CAST for integer comparison
        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("order_date", sql);
        Assert.Contains("CAST", sql);
    }

    [Fact]
    public void Year_GreaterThan_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) > 1996)
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains(">", sql);
    }

    [Fact]
    public void Year_NotEqual_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) != 2000)
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("<>", sql);
    }

    // ==========================================
    // Sql.Month Tests - SQL Generation
    // ==========================================

    [Fact]
    public void Month_ToSql_GeneratesMonthFunction()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.OrderDate) == 12)
            .ToSql();

        // SQLite uses strftime
        Assert.Contains("strftime('%m'", sql);
        Assert.Contains("order_date", sql);
        Assert.Contains("CAST", sql);
    }

    [Fact]
    public void Month_LessThan_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.OrderDate) < 6)
            .ToSql();

        Assert.Contains("strftime('%m'", sql);
        Assert.Contains("<", sql);
    }

    // ==========================================
    // Sql.Day Tests - SQL Generation
    // ==========================================

    [Fact]
    public void Day_ToSql_GeneratesDayFunction()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Day(o.OrderDate) == 15)
            .ToSql();

        // SQLite uses strftime
        Assert.Contains("strftime('%d'", sql);
        Assert.Contains("order_date", sql);
        Assert.Contains("CAST", sql);
    }

    [Fact]
    public void Day_GreaterOrEqual_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Day(o.OrderDate) >= 20)
            .ToSql();

        Assert.Contains("strftime('%d'", sql);
        Assert.Contains(">=", sql);
    }

    // ==========================================
    // Combined Tests - SQL Generation
    // ==========================================

    [Fact]
    public void Year_And_Month_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .And(o => Sql.Month(o.OrderDate) == 12)
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("strftime('%m'", sql);
        Assert.Contains("AND", sql);
    }

    [Fact]
    public void Year_Month_Day_CombinedFilter_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .And(o => Sql.Month(o.OrderDate) == 7)
            .And(o => Sql.Day(o.OrderDate) == 4)
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("strftime('%m'", sql);
        Assert.Contains("strftime('%d'", sql);
        Assert.Contains("AND", sql);
    }

    [Fact]
    public void DateFunctions_Or_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .Or(o => Sql.Year(o.OrderDate) == 1998)
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("OR", sql);
    }

    // ==========================================
    // Different Date Columns Tests
    // ==========================================

    [Fact]
    public void Year_OnRequiredDate_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.RequiredDate) == 1997)
            .ToSql();

        Assert.Contains("required_date", sql);
        Assert.Contains("strftime('%Y'", sql);
    }

    [Fact]
    public void Month_OnShippedDate_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.ShippedDate) == 1)
            .ToSql();

        Assert.Contains("shipped_date", sql);
        Assert.Contains("strftime('%m'", sql);
    }

    [Fact]
    public void Day_OnOrderDate_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Day(o.OrderDate) == 1)
            .ToSql();

        Assert.Contains("order_date", sql);
        Assert.Contains("strftime('%d'", sql);
    }

    // ==========================================
    // Combined with Other Features
    // ==========================================

    [Fact]
    public void DateFunctions_WithOrderBy_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .OrderBy(o => o.OrderDate)
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void DateFunctions_WithTake_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.OrderDate) >= 10)
            .Take(5)
            .ToSql();

        Assert.Contains("strftime('%m'", sql);
        Assert.Contains("LIMIT 5", sql);
    }

    [Fact]
    public void DateFunctions_WithStringFunctions_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .And(o => Sql.Upper(o.ShipCity) == "LONDON")
            .ToSql();

        Assert.Contains("strftime('%Y'", sql);
        Assert.Contains("UPPER", sql);
        Assert.Contains("AND", sql);
    }
}
