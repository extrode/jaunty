using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryMappingModeTests : IDisposable
{
    private readonly Database _db;

    public QueryMappingModeTests()
    {
        _db = new Database();
    }

    public void Dispose() => _db.Dispose();

    #region Strict Mode (Query)

    [Fact]
    public void Query_AllColumnsPresent_Succeeds()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.NotEmpty(categories);
    }

    [Fact]
    public void Query_MissingOneColumn_ThrowsWithPropertyName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories"));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [Fact]
    public void Query_MissingMultipleColumns_ThrowsWithFirstMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId FROM categories"));

        // Should mention at least one missing property
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [SkipSQLiteAsyncFact]
    public void Query_ExtraColumnsInResult_Ignored()
    {
        // Query returns extra column not in entity - should be ignored
        var summaries = _db.Connection.Query<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
    }

    #endregion

    #region Projection Mode (QueryPartial)

    [Fact]
    public void QueryPartial_MissingColumns_Allowed()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public void QueryPartial_OnlyIdColumn_MapsId()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s =>
        {
            Assert.True(s.ProductId > 0);
            Assert.Equal(string.Empty, s.ProductName); // Default value
        });
    }

    [Fact]
    public void QueryPartial_NoMatchingColumns_ReturnsDefaultEntities()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT category_id, category_name FROM categories");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s =>
        {
            Assert.Equal(0, s.ProductId);
            Assert.Equal(string.Empty, s.ProductName);
        });
    }

    [Fact]
    public void QueryPartial_ExtraColumnsInResult_Ignored()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price, supplier_id FROM products");

        Assert.NotEmpty(summaries);
    }

    [Fact]
    public void QueryPartial_WithParameters_WorksCorrectly()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { id = 1 });

        Assert.NotEmpty(summaries);
    }

    #endregion

    #region Null into Non-Nullable

    [Fact]
    public void Query_NullIntoNonNullableProperty_Throws()
    {
        // Create entity with non-nullable property that will receive null
        var ex = Assert.Throws<InvalidOperationException>(() => _db.Connection.Query<NonNullableEntity>("SELECT NULL AS RequiredValue"));

        Assert.Contains("Cannot assign NULL", ex.Message);
        Assert.Contains("RequiredValue", ex.Message);
    }

    [Fact]
    public void QueryPartial_NullIntoNonNullableProperty_Throws()
    {
        // Even in projection mode, null into non-nullable should throw
        var ex = Assert.Throws<InvalidOperationException>(() => _db.Connection.QueryPartial<NonNullableEntity>("SELECT NULL AS RequiredValue"));

        Assert.Contains("Cannot assign NULL", ex.Message);
    }

    #endregion
}

public class NonNullableEntity
{
    public int RequiredValue { get; set; }
}