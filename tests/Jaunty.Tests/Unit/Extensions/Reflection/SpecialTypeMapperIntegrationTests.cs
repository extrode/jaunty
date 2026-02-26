using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// Integration tests for SpecialTypeMappers.
/// Tests Dictionary, KeyValuePair, ValueTuple, and ExpandoObject mapping.
/// </summary>
public class SpecialTypeMapperIntegrationTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public SpecialTypeMapperIntegrationTests(DialectFixture fixture)
    {
        _fixture = fixture;
        
        // Register special type mappers
        SpecialTypeMappers.Register();
    }

    #region Dictionary Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringObject_MapsColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<Dictionary<string, object>>(sql);

        Assert.Equal(1, results.Count);
        var row = results[0];
        Assert.Contains("CategoryId", row.Keys);
        Assert.Contains("CategoryName", row.Keys);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringInt_MapsColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId FROM Categories"
            : "SELECT category_id AS CategoryId FROM categories LIMIT 1";

        var results = connection.Query<Dictionary<string, int>>(sql);

        Assert.Equal(1, results.Count);
        var row = results[0];
        Assert.Contains("CategoryId", row.Keys);
        Assert.True(row["CategoryId"] > 0);
    }

    #endregion

    #region KeyValuePair Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_MapsTwoColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<KeyValuePair<int, string>>(sql);

        Assert.Equal(1, results.Count);
        var kvp = results[0];
        Assert.True(kvp.Key > 0);
        Assert.NotNull(kvp.Value);
        Assert.NotEmpty(kvp.Value);
    }

    #endregion

    #region ValueTuple Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ValueTuple_TwoElements_MapsPositionally(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<(int Id, string Name)>(sql);

        Assert.Equal(1, results.Count);
        var tuple = results[0];
        Assert.True(tuple.Id > 0);
        Assert.NotNull(tuple.Name);
        Assert.NotEmpty(tuple.Name);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ValueTuple_SevenElements_MapsAllItems(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName, Description, 1, 2.0, 3, '4' FROM Categories"
            : dialect.Provider == DialectProvider.Postgres
            ? @"SELECT category_id AS ""CategoryId"", category_name AS ""CategoryName"", description AS ""Description"", 1, 2.0, 3, '4' FROM categories LIMIT 1"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description, 1, 2.0, 3, '4' FROM categories LIMIT 1";

        var results = connection.Query<(int, string, string, int, double, int, string)>(sql);

        Assert.Equal(1, results.Count);
        var tuple = results[0];
        Assert.True(tuple.Item1 > 0);
        Assert.NotNull(tuple.Item2);
        Assert.NotEmpty(tuple.Item2);
    }

    #endregion

    #region ExpandoObject/Dynamic Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_MapsAllColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : dialect.Provider == DialectProvider.Postgres
            ? @"SELECT category_id AS ""CategoryId"", category_name AS ""CategoryName"" FROM categories LIMIT 1"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<dynamic>(sql);

        Assert.Equal(1, results.Count);
        dynamic row = results[0];
        Assert.True(((int)row.CategoryId) > 0);
        Assert.NotNull((string)row.CategoryName);
        Assert.NotEmpty((string)row.CategoryName);
    }

    #endregion
}
