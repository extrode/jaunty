using FluentAssertions;

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
        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("order_date");
        sql.Should().Contain("CAST");
    }

    [Fact]
    public void Year_GreaterThan_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) > 1996)
            .ToSql();

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain(">");
    }

    [Fact]
    public void Year_NotEqual_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) != 2000)
            .ToSql();

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("<>");
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
        sql.Should().Contain("strftime('%m'");
        sql.Should().Contain("order_date");
        sql.Should().Contain("CAST");
    }

    [Fact]
    public void Month_LessThan_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.OrderDate) < 6)
            .ToSql();

        sql.Should().Contain("strftime('%m'");
        sql.Should().Contain("<");
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
        sql.Should().Contain("strftime('%d'");
        sql.Should().Contain("order_date");
        sql.Should().Contain("CAST");
    }

    [Fact]
    public void Day_GreaterOrEqual_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Day(o.OrderDate) >= 20)
            .ToSql();

        sql.Should().Contain("strftime('%d'");
        sql.Should().Contain(">=");
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

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("strftime('%m'");
        sql.Should().Contain("AND");
    }

    [Fact]
    public void Year_Month_Day_CombinedFilter_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .And(o => Sql.Month(o.OrderDate) == 7)
            .And(o => Sql.Day(o.OrderDate) == 4)
            .ToSql();

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("strftime('%m'");
        sql.Should().Contain("strftime('%d'");
        sql.Should().Contain("AND");
    }

    [Fact]
    public void DateFunctions_Or_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .Or(o => Sql.Year(o.OrderDate) == 1998)
            .ToSql();

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("OR");
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

        sql.Should().Contain("required_date");
        sql.Should().Contain("strftime('%Y'");
    }

    [Fact]
    public void Month_OnShippedDate_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.ShippedDate) == 1)
            .ToSql();

        sql.Should().Contain("shipped_date");
        sql.Should().Contain("strftime('%m'");
    }

    [Fact]
    public void Day_OnOrderDate_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Day(o.OrderDate) == 1)
            .ToSql();

        sql.Should().Contain("order_date");
        sql.Should().Contain("strftime('%d'");
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

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("ORDER BY");
    }

    [Fact]
    public void DateFunctions_WithTake_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Month(o.OrderDate) >= 10)
            .Take(5)
            .ToSql();

        sql.Should().Contain("strftime('%m'");
        sql.Should().Contain("LIMIT 5");
    }

    [Fact]
    public void DateFunctions_WithStringFunctions_ToSql()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .And(o => Sql.Upper(o.ShipCity) == "LONDON")
            .ToSql();

        sql.Should().Contain("strftime('%Y'");
        sql.Should().Contain("UPPER");
        sql.Should().Contain("AND");
    }
}
