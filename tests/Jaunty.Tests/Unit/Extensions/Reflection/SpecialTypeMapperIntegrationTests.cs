using System.Collections.Generic;
using System.Dynamic;

using FluentAssertions;

using Jaunty.Extensions.Reflection;
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
    public void Query_DictionaryStringObject_MapsColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<Dictionary<string, object>>(sql);

        results.Should().HaveCount(1);
        var row = results[0];
        row.Should().ContainKey("CategoryId");
        row.Should().ContainKey("CategoryName");
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_DictionaryStringInt_MapsColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId FROM Categories"
            : "SELECT category_id AS CategoryId FROM categories LIMIT 1";

        var results = connection.Query<Dictionary<string, int>>(sql);

        results.Should().HaveCount(1);
        var row = results[0];
        row.Should().ContainKey("CategoryId");
        row["CategoryId"].Should().BeGreaterThan(0);
    }

    #endregion

    #region KeyValuePair Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_MapsTwoColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<KeyValuePair<int, string>>(sql);

        results.Should().HaveCount(1);
        var kvp = results[0];
        kvp.Key.Should().BeGreaterThan(0);
        kvp.Value.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ValueTuple Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_ValueTuple_TwoElements_MapsPositionally(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<(int Id, string Name)>(sql);

        results.Should().HaveCount(1);
        var tuple = results[0];
        tuple.Id.Should().BeGreaterThan(0);
        tuple.Name.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_ValueTuple_SevenElements_MapsAllItems(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName, Description, 1, 2.0, 3, '4' FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description, 1, 2.0, 3, '4' FROM categories LIMIT 1";

        var results = connection.Query<(int, string, string, int, double, int, string)>(sql);

        results.Should().HaveCount(1);
        var tuple = results[0];
        tuple.Item1.Should().BeGreaterThan(0);
        tuple.Item2.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ExpandoObject/Dynamic Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_MapsAllColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1";

        var results = connection.Query<dynamic>(sql);

        results.Should().HaveCount(1);
        dynamic row = results[0];
        ((int)row.CategoryId).Should().BeGreaterThan(0);
        ((string)row.CategoryName).Should().NotBeNullOrEmpty();
    }

    #endregion
}
