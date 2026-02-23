using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryEmptyResultsTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryEmptyResultsTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string CategoriesTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Categories" : "categories";

    private static string CategoryIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CategoryId" : "category_id";

    private static string CategoryNameColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CategoryName" : "category_name";

    private static string DescriptionColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Description" : "description";

    private static string CustomerCustomerDemoTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CustomerCustomerDemo" : "customer_customer_demo";

    private static string CustomerIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CustomerId" : "customer_id";

    private static string CustomerTypeIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CustomerTypeId" : "customer_type_id";

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_NoMatchingRows_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<Category>(
            $"SELECT {CategoryIdColumn(dialect)} AS CategoryId, {CategoryNameColumn(dialect)} AS CategoryName, {DescriptionColumn(dialect)} AS Description FROM {CategoriesTable(dialect)} WHERE {CategoryIdColumn(dialect)} = @Id",
            new { Id = -99999 });

        Assert.Empty(results);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_EmptyTable_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // customer_customer_demo is typically empty in Northwind
        var results = connection.Query<CustomerCustomerDemo>(
            $"SELECT {CustomerIdColumn(dialect)} AS CustomerId, {CustomerTypeIdColumn(dialect)} AS CustomerTypeId FROM {CustomerCustomerDemoTable(dialect)}");

        Assert.Empty(results);
    }

    private class CustomerCustomerDemo
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerTypeId { get; set; } = string.Empty;
    }
}


