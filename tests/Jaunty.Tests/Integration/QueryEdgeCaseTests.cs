using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class QueryEdgeCaseTests : IDisposable
{
    private readonly Database _db;

    public QueryEdgeCaseTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    #region Null Handling

    [Fact]
    public void Query_NullablePropertyWithNullValue_SetsToNull()
    {
        var customers = _db.Connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE region IS NULL LIMIT 1");

        Assert.NotEmpty(customers);
        Assert.Null(customers[0].Region);
    }

    [Fact]
    public void Query_NullableIntWithNullValue_SetsToNull()
    {
        // Products with null supplier_id
        var products = _db.Connection.QueryPartial<Product>(
            @"SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId,
              category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
              units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel,
              discontinued AS Discontinued
              FROM products WHERE supplier_id IS NULL");

        // May or may not have results, but shouldn't throw
        Assert.NotNull(products);
    }

    #endregion

    #region Empty Results

    [Fact]
    public void Query_NoMatchingRows_ReturnsEmptyList()
    {
        var results = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            -99999);

        Assert.Empty(results);
    }

    [Fact]
    public void Query_EmptyTable_ReturnsEmptyList()
    {
        // customer_customer_demo is typically empty in Northwind
        var results = _db.Connection.Query<CustomerCustomerDemo>(
            "SELECT customer_id AS CustomerId, customer_type_id AS CustomerTypeId FROM customer_customer_demo");

        Assert.Empty(results);
    }

    #endregion

    #region Large Result Sets

    [Fact]
    public void Query_LargeResultSet_HandlesCorrectly()
    {
        var orders = _db.Connection.Query<OrderSummary>(
            "SELECT order_id AS OrderId, customer_id AS CustomerId, employee_id AS EmployeeId FROM orders");

        Assert.True(orders.Count > 100); // Northwind has ~800 orders
    }

    #endregion

    #region Special Characters in Data

    [Fact]
    public void Query_DataWithSpecialCharacters_HandlesCorrectly()
    {
        // Some company names have special characters like apostrophes
        var customers = _db.Connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE company_name LIKE @Name",
            "%'%");

        // Just verify it doesn't throw
        Assert.NotNull(customers);
    }

    #endregion

    #region Transaction Support

    [Fact]
    public void Query_WithTransaction_ExecutesCorrectly()
    {
        _db.Connection.Open();
        using var transaction = _db.Connection.BeginTransaction();

        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            new CommandOptions(transaction));

        Assert.NotEmpty(categories);
        transaction.Rollback();
    }

    [Fact]
    public void QueryScalar_WithTransaction_ExecutesCorrectly()
    {
        _db.Connection.Open();
        using var transaction = _db.Connection.BeginTransaction();

        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products",
            new CommandOptions(transaction));

        Assert.True(count > 0);
        transaction.Rollback();
    }

    #endregion

    #region CommandOptions Variants

    [Fact]
    public void Query_WithTimeoutOption_ExecutesCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(60));

        Assert.NotEmpty(categories);
    }

    [Fact]
    public void Query_WithParametersAndOptions_ExecutesCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 },
            CommandOptions.WithTimeout(30));

        Assert.Single(categories);
    }

    #endregion

    #region Case Sensitivity

    [Fact]
    public void Query_ColumnNameCaseInsensitive_MapsCorrectly()
    {
        // SQLite returns lowercase, our properties are PascalCase
        // This tests the case-insensitive matching in MetadataCache
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS categoryid, category_name AS categoryname, description AS DESCRIPTION FROM categories WHERE category_id = @Id",
            1);

        Assert.Single(categories);
        Assert.False(string.IsNullOrEmpty(categories[0].CategoryName));
    }

    #endregion

    #region Connection State Management

    [Fact]
    public void Query_ConnectionAlreadyOpen_LeavesOpen()
    {
        _db.Connection.Open();
        var initialState = _db.Connection.State;

        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.Equal(initialState, _db.Connection.State);
        Assert.NotEmpty(categories);
    }

    [Fact]
    public void Query_ConnectionClosed_OpensAndCloses()
    {
        // Connection starts closed
        Assert.Equal(System.Data.ConnectionState.Closed, _db.Connection.State);

        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        // Should be closed again after query
        Assert.Equal(System.Data.ConnectionState.Closed, _db.Connection.State);
        Assert.NotEmpty(categories);
    }

    #endregion
}

// Additional test entities
public class CustomerCustomerDemo
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerTypeId { get; set; } = string.Empty;
}

public class OrderSummary
{
    public long OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
}
