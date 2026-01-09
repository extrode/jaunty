using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class PositionalParameterTests : IDisposable
{
    private readonly Database _db;

    public PositionalParameterTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    #region Single Positional Parameter

    [Fact]
    public void Query_SingleIntParameter_BindsCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1});

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Fact]
    public void Query_SingleStringParameter_BindsCorrectly()
    {
        var customers = _db.Connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        Assert.Single(customers);
        Assert.Equal("ALFKI", customers[0].CustomerId);
    }

    //[Fact]
    //public void Query_SingleBoolParameter_BindsCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE discontinued = @Discontinued",
    //        false);

    //    Assert.True(count > 0);
    //}

    //[Fact]
    //public void Query_SingleDecimalParameter_BindsCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE unit_price > @Price",
    //        20.0m);

    //    Assert.True(count > 0);
    //}

    [Fact]
    public void Query_SingleDateTimeParameter_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM orders WHERE order_date > @Date",
            new { Date = new DateTime(1997, 6, 1) });

        Assert.True(count > 0);
    }

    #endregion

    #region Multiple Positional Parameters (params)

    //[Fact]
    //public void Query_TwoPositionalParameters_BindsCorrectly()
    //{
    //    var categories = _db.Connection.Query<Category>(
    //        "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id >= @Min AND category_id <= @Max",
    //        1, 3);

    //    Assert.Equal(3, categories.Count);
    //}

    //[Fact]
    //public void Query_ThreePositionalParameters_BindsCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE category_id = @Cat AND supplier_id = @Sup AND discontinued = @Disc",
    //        1, 1, false);

    //    Assert.True(count >= 0);
    //}

    //[Fact]
    //public void Query_FourPositionalParameters_BindsCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE category_id >= @A AND category_id <= @B AND supplier_id >= @C AND supplier_id <= @D",
    //        1, 3, 1, 5);

    //    Assert.True(count >= 0);
    //}

    //[Fact]
    //public void Query_MixedTypePositionalParameters_BindsCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE category_id = @Cat AND product_name LIKE @Name AND unit_price > @Price",
    //        1, "%a%", 5.0);

    //    Assert.True(count >= 0);
    //}

    #endregion

    #region Array Parameters

    [Fact]
    public void Query_ObjectArray_BindsCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id >= @Min AND category_id <= @Max",
            new { Min = 1, Max = 3 });

        Assert.Equal(3, categories.Count);
    }

    [Fact]
    public void QueryScalar_ObjectArray_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @Cat AND supplier_id = @Sup",
            new { Cat = 1, Sup = 1 });

        Assert.True(count >= 0);
    }

    #endregion

    #region Duplicate Parameters in SQL

    //[Fact]
    //public void Query_DuplicateParameterName_UsedTwice_WorksCorrectly()
    //{
    //    // Same parameter used twice in SQL should only require one value
    //    var categories = _db.Connection.Query<Category>(
    //        "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id OR category_id = @Id",
    //        1);

    //    Assert.Single(categories);
    //}

    //[Fact]
    //public void Query_DuplicateParameterInDifferentClauses_WorksCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE category_id >= @Val AND supplier_id <= @Val",
    //        3);

    //    Assert.True(count >= 0);
    //}

    #endregion

    #region Parameter Count Validation

    [Fact]
    public void Query_TooFewParameters_ThrowsWithMessage()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @A AND supplier_id = @B AND discontinued = @C",
                new { A = 1, B = 2 }));

        // Error should indicate missing parameter C
        Assert.Contains("@C", ex.Message);
    }

    //[Fact]
    //public void Query_TooManyParameters_ThrowsWithMessage()
    //{
    //    var ex = Assert.Throws<ArgumentException>(() =>
    //        _db.Connection.QueryScalar<long>(
    //            "SELECT COUNT(*) FROM products WHERE category_id = @A",
    //            new { category_id = 1, category_id = 2, category_id = 3 }));

    //    Assert.Contains("1", ex.Message); // Expected 1 parameter
    //    Assert.Contains("3", ex.Message); // Got 3 values
    //}

    #endregion

    #region NULL Positional Parameters

    [Fact]
    public void Query_NullPositionalParameter_BindsAsDbNull()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM customers WHERE region = @Region OR @Region IS NULL",
            new { region = (long?)null });

        Assert.True(count >= 0);
    }

    #endregion
}
