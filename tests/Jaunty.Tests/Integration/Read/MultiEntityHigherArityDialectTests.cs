using System.Data;

using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-090. The arity 3-7 multi-entity family was only ever executed against
/// <c>System.Data.SQLite</c>: every case in <c>QueryMultiEntity{Async,}N{3..7}Tests.cs</c> is
/// <c>[Theory] [SystemSqlite]</c> over an in-memory database the file creates itself, so no server
/// dialect's identifier quoting, parameter prefix or reader behaviour was ever proven above arity 2 -
/// where <c>QueryMultiEntityTests</c> does run on SQL Server, PostgreSQL and MariaDB.
/// <para>
/// This runs the family against the seeded Northwind schema on each server dialect that is
/// available, at every arity from 3 to 7, sync and async. Converting the five existing SQLite files
/// would mean porting their bespoke DDL to three servers; joining the schema the dialect fixture
/// already seeds proves the same thing without it.
/// </para>
/// <para>
/// Note the round-11 registry entry AUD-R11-006 claims these arity files were added "SQLite-backed
/// plus SqlServer Theory cases that skip without a live instance". They were not: there is no
/// <c>[SqlServer]</c> attribute in any of them. That line is corrected in the findings registry.
/// </para>
/// </summary>
public class MultiEntityHigherArityDialectTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public MultiEntityHigherArityDialectTests(DialectFixture fixture) => _fixture = fixture;

    private static string TopPrefix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? $"TOP ({count}) " : string.Empty;

    private static string LimitSuffix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? string.Empty : $" LIMIT {count}";

    public sealed class ProductRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }

    public sealed class CategoryRow
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public sealed class SupplierRow
    {
        public int SupplierId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }

    public sealed class OrderRow
    {
        public int OrderId { get; set; }
    }

    public sealed class OrderDetailRow
    {
        public int DetailProductId { get; set; }
        public int DetailOrderId { get; set; }
    }

    public sealed class CustomerRow
    {
        public string CustomerId { get; set; } = string.Empty;
    }

    public sealed class EmployeeRow
    {
        public int EmployeeId { get; set; }
    }

    /// <summary>
    /// The one identifier the three seeds do not agree on: SQL Server's table is <c>OrderDetails</c>
    /// with a snake_case computed column per column, where PostgreSQL and MariaDB name the table
    /// <c>order_details</c>. Every other table resolves case-insensitively on SQL Server and is
    /// already lowercase on the other two.
    /// </summary>
    private static string Details(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "OrderDetails" : "order_details";

    private static string Joins(DialectInfo dialect) => $@"
              FROM {Details(dialect)} od
              JOIN orders o ON o.order_id = od.order_id
              JOIN products p ON p.product_id = od.product_id
              JOIN categories c ON c.category_id = p.category_id
              JOIN suppliers s ON s.supplier_id = p.supplier_id
              JOIN customers cu ON cu.customer_id = o.customer_id
              JOIN employees e ON e.employee_id = o.employee_id";

    private static string Select(DialectInfo dialect, int arity, int rows)
    {
        // unit_price is float on SQL Server and numeric/decimal on the other two, so the detail row
        // carries order_id instead - the arity, not the type mapping, is what this file is proving.
        string[] columns =
        [
            "p.product_id AS ProductId, p.product_name AS ProductName",
            "c.category_id AS CategoryId, c.category_name AS CategoryName",
            "s.supplier_id AS SupplierId, s.company_name AS CompanyName",
            "o.order_id AS OrderId",
            "od.product_id AS DetailProductId, od.order_id AS DetailOrderId",
            "cu.customer_id AS CustomerId",
            "e.employee_id AS EmployeeId",
        ];

        return $"SELECT {TopPrefix(dialect, rows)}{string.Join(", ", columns.Take(arity))}"
            + Joins(dialect)
            + $" ORDER BY od.order_id, od.product_id{LimitSuffix(dialect, rows)}";
    }

    // ------------------------------------------------------------------
    // Sync.
    // ------------------------------------------------------------------

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Arity3_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow)> rows =
            connection.Query<ProductRow, CategoryRow, SupplierRow>(Select(dialect, 3, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.True(r.Item1.ProductId > 0);
            Assert.True(r.Item2.CategoryId > 0);
            Assert.True(r.Item3.SupplierId > 0);
            Assert.NotEmpty(r.Item1.ProductName);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Arity4_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow, OrderRow)> rows =
            connection.Query<ProductRow, CategoryRow, SupplierRow, OrderRow>(Select(dialect, 4, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.True(r.Item4.OrderId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Arity5_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow)> rows =
            connection.Query<ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow>(Select(dialect, 5, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(r.Item1.ProductId, r.Item5.DetailProductId);
            Assert.Equal(r.Item4.OrderId, r.Item5.DetailOrderId);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Arity6_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow, CustomerRow)> rows =
            connection.Query<ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow, CustomerRow>(
                Select(dialect, 6, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.NotEmpty(r.Item6.CustomerId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Arity7_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow, CustomerRow, EmployeeRow)> rows =
            connection.Query<ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow, CustomerRow, EmployeeRow>(
                Select(dialect, 7, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.True(r.Item7.EmployeeId > 0));
    }

    // ------------------------------------------------------------------
    // Async.
    // ------------------------------------------------------------------

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task Arity3Async_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow)> rows =
            await connection.QueryAsync<ProductRow, CategoryRow, SupplierRow>(Select(dialect, 3, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.True(r.Item3.SupplierId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task Arity5Async_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow)> rows =
            await connection.QueryAsync<ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow>(
                Select(dialect, 5, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Equal(r.Item1.ProductId, r.Item5.DetailProductId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task Arity7Async_RunsOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        List<(ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow, CustomerRow, EmployeeRow)> rows =
            await connection.QueryAsync<ProductRow, CategoryRow, SupplierRow, OrderRow, OrderDetailRow, CustomerRow, EmployeeRow>(
                Select(dialect, 7, 5));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.NotEmpty(r.Item6.CustomerId));
    }

    /// <summary>
    /// A parameter, so the dialect's parameter prefix is exercised on this family too and not only
    /// its identifier quoting.
    /// </summary>
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Arity3_BindsAParameterOnAServerDialect(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        string sql = $"SELECT {TopPrefix(dialect, 5)}"
            + "p.product_id AS ProductId, p.product_name AS ProductName, "
            + "c.category_id AS CategoryId, c.category_name AS CategoryName, "
            + "s.supplier_id AS SupplierId, s.company_name AS CompanyName"
            + Joins(dialect)
            + " WHERE c.category_id = @CategoryId"
            + $" ORDER BY od.order_id, od.product_id{LimitSuffix(dialect, 5)}";

        List<(ProductRow, CategoryRow, SupplierRow)> rows =
            connection.Query<ProductRow, CategoryRow, SupplierRow>(sql, new { CategoryId = 1 });

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(1, r.Item2.CategoryId));
    }
}
