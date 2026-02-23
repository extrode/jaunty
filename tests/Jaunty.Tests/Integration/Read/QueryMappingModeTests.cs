using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryMappingModeTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryMappingModeTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Strict Mode (Query)

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_AllColumnsPresent_Succeeds(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_MissingOneColumn_ThrowsWithPropertyName(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories"));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_MissingMultipleColumns_ThrowsWithFirstMissing(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Query<Category>(
                "SELECT category_id AS CategoryId FROM categories"));

        // Should mention at least one missing property
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ExactColumnMatch_Succeeds(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Strict mode requires exact match: all entity properties present, no unmapped columns
        var summaries = connection.Query<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    #endregion

    #region Projection Mode (QueryPartial)

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_MissingColumns_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_OnlyIdColumn_MapsId(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s =>
        {
            Assert.True(s.ProductId > 0);
            Assert.Equal(string.Empty, s.ProductName); // Default value
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_NoMatchingColumns_ReturnsDefaultEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT category_id, category_name FROM categories");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s =>
        {
            Assert.Equal(0, s.ProductId);
            Assert.Equal(string.Empty, s.ProductName);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_ExtraColumnsInResult_Ignored(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price, supplier_id FROM products");

        Assert.NotEmpty(summaries);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_WithParameters_WorksCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { id = 1 });

        Assert.NotEmpty(summaries);
    }

    #endregion

    #region Null into Non-Nullable

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_NullIntoNonNullableProperty_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Create entity with non-nullable property that will receive null
        var ex = Assert.Throws<InvalidOperationException>(() => connection.Query<NonNullableEntity>("SELECT NULL AS RequiredValue"));

        Assert.Contains("Cannot assign NULL", ex.Message);
        Assert.Contains("RequiredValue", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartial_NullIntoNonNullableProperty_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Even in projection mode, null into non-nullable should throw
        var ex = Assert.Throws<InvalidOperationException>(() => connection.QueryPartial<NonNullableEntity>("SELECT NULL AS RequiredValue"));

        Assert.Contains("Cannot assign NULL", ex.Message);
    }

    #endregion
}

public class NonNullableEntity
{
    public int RequiredValue { get; set; }
}


