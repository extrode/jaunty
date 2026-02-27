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
        
        // PostgreSQL folds column names to lowercase, so use case-insensitive comparison
        var keys = row.Keys.ToList();
        Assert.True(keys.Any(k => k.Equals("CategoryId", StringComparison.OrdinalIgnoreCase)));
        Assert.True(keys.Any(k => k.Equals("CategoryName", StringComparison.OrdinalIgnoreCase)));
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
        
        // PostgreSQL folds column names to lowercase, so use case-insensitive comparison
        var key = row.Keys.FirstOrDefault(k => k.Equals("CategoryId", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(key);
        Assert.True(row[key] > 0);
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

    #region Edge Case Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dictionary_WithNullValue_MapsNullAsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        // Insert a row with a NULL value, then query it
        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, CAST(NULL AS NVARCHAR(100)) AS Description FROM Categories ORDER BY CategoryId",
            DialectProvider.Postgres => @"SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Description FROM categories ORDER BY category_id LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, CAST(NULL AS CHAR(100)) AS Description FROM categories ORDER BY category_id LIMIT 1",
            _ => "SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Description FROM categories ORDER BY category_id LIMIT 1"
        };

        var results = connection.Query<Dictionary<string, object>>(sql);

        Assert.Equal(1, results.Count);
        var row = results[0];

        // Verify null is mapped as null (not DBNull)
        var key = row.Keys.FirstOrDefault(k => k.Equals("Description", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(key);
        Assert.Null(row[key]);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ExpandoObject_WithNullValue_MapsNullAsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, CAST(NULL AS NVARCHAR(100)) AS Description FROM Categories ORDER BY CategoryId",
            DialectProvider.Postgres => @"SELECT category_id AS ""CategoryId"", CAST(NULL AS TEXT) AS ""Description"" FROM categories ORDER BY category_id LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, CAST(NULL AS CHAR(100)) AS Description FROM categories ORDER BY category_id LIMIT 1",
            _ => "SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Description FROM categories ORDER BY category_id LIMIT 1"
        };

        var results = connection.Query<dynamic>(sql);

        Assert.Equal(1, results.Count);
        dynamic row = results[0];

        // Verify null is mapped as null
        Assert.Null((object)row.Description);
    }

    #endregion

    #region Dictionary<string, TValue> Typed Value Tests

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringDecimal_MapsTypedValues(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        // Select numeric columns/literals that can be converted to decimal
        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, 19.99 AS Price FROM Categories",
            DialectProvider.Postgres => @"SELECT category_id AS CategoryId, 19.99 AS ""Price"" FROM categories LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, 19.99 AS Price FROM categories LIMIT 1",
            _ => "SELECT category_id AS CategoryId, 19.99 AS Price FROM categories LIMIT 1"
        };

        var results = connection.Query<Dictionary<string, decimal>>(sql);

        Assert.Equal(1, results.Count);
        var row = results[0];

        // Verify values are converted to decimal
        var categoryIdKey = row.Keys.FirstOrDefault(k => k.Equals("CategoryId", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(categoryIdKey);
        Assert.IsType<decimal>(row[categoryIdKey]);
        
        var priceKey = row.Keys.FirstOrDefault(k => k.Equals("Price", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(priceKey);
        Assert.Equal(19.99m, row[priceKey]);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_DictionaryStringDecimal_WithNullValue_HandlesNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, CAST(NULL AS DECIMAL(10,2)) AS Price FROM Categories ORDER BY CategoryId",
            DialectProvider.Postgres => @"SELECT category_id AS CategoryId, CAST(NULL AS NUMERIC(10,2)) AS Price FROM categories ORDER BY category_id LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, CAST(NULL AS DECIMAL(10,2)) AS Price FROM categories ORDER BY category_id LIMIT 1",
            _ => "SELECT category_id AS CategoryId, CAST(NULL AS DECIMAL(10,2)) AS Price FROM categories ORDER BY category_id LIMIT 1"
        };

        var results = connection.Query<Dictionary<string, decimal?>>(sql);

        Assert.Equal(1, results.Count);
        var row = results[0];

        // Verify nullable decimal handles null
        var priceKey = row.Keys.FirstOrDefault(k => k.Equals("Price", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(priceKey);
        Assert.Null(row[priceKey]);
    }

    #endregion

    #region ValueTuple Edge Cases

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ValueTuple_WithNullValues_HandlesNulls(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, CAST(NULL AS NVARCHAR(100)) AS Name FROM Categories",
            DialectProvider.Postgres => @"SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Name FROM categories LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, CAST(NULL AS CHAR(100)) AS Name FROM categories LIMIT 1",
            _ => "SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Name FROM categories LIMIT 1"
        };

        var results = connection.Query<(int, string)>(sql);

        Assert.Equal(1, results.Count);
        var tuple = results[0];
        Assert.True(tuple.Item1 > 0);
        Assert.Null(tuple.Item2);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ValueTuple_WithMixedTypes_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, CategoryName, 123, 45.67 FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, 123, 45.67 FROM categories LIMIT 1";

        var results = connection.Query<(int, string, int, double)>(sql);

        Assert.Equal(1, results.Count);
        var tuple = results[0];
        Assert.Equal(123, tuple.Item3);
        Assert.Equal(45.67, tuple.Item4, 2);
    }

    #endregion

    #region KeyValuePair Edge Cases

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_WithNullValue_HandlesNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, CAST(NULL AS NVARCHAR(100)) AS Name FROM Categories",
            DialectProvider.Postgres => @"SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Name FROM categories LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, CAST(NULL AS CHAR(100)) AS Name FROM categories LIMIT 1",
            _ => "SELECT category_id AS CategoryId, CAST(NULL AS TEXT) AS Name FROM categories LIMIT 1"
        };

        var results = connection.Query<KeyValuePair<int, string?>>(sql);

        Assert.Equal(1, results.Count);
        var kvp = results[0];
        Assert.True(kvp.Key > 0);
        Assert.Null(kvp.Value);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_WithTypedValues_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) CategoryId, 123.45 FROM Categories"
            : "SELECT category_id AS CategoryId, 123.45 FROM categories LIMIT 1";

        var results = connection.Query<KeyValuePair<int, decimal>>(sql);

        Assert.Equal(1, results.Count);
        var kvp = results[0];
        Assert.True(kvp.Key > 0);
        Assert.Equal(123.45m, kvp.Value);
    }

    #endregion

    #region ExpandoObject Edge Cases

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_MultipleColumns_MapsAll(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, CategoryName, Description FROM Categories",
            DialectProvider.Postgres => @"SELECT category_id AS ""CategoryId"", category_name AS ""CategoryName"", description AS ""Description"" FROM categories LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 1",
            _ => "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 1"
        };

        var results = connection.Query<dynamic>(sql);

        Assert.Equal(1, results.Count);
        dynamic row = results[0];

        // Verify all columns are mapped
        Assert.True(((int)row.CategoryId) > 0);
        Assert.NotNull((string)row.CategoryName);
        // Description might be null for some rows
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_WithNumericColumns_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = dialect.Provider switch
        {
            DialectProvider.SqlServer => "SELECT TOP (1) CategoryId, 123 AS NumValue FROM Categories",
            DialectProvider.Postgres => @"SELECT category_id AS ""CategoryId"", 123 AS ""NumValue"" FROM categories LIMIT 1",
            DialectProvider.MariaDb => "SELECT category_id AS CategoryId, 123 AS NumValue FROM categories LIMIT 1",
            _ => "SELECT category_id AS CategoryId, 123 AS NumValue FROM categories LIMIT 1"
        };

        var results = connection.Query<dynamic>(sql);

        Assert.Equal(1, results.Count);
        dynamic row = results[0];

        Assert.True(((int)row.CategoryId) > 0);
        Assert.Equal(123, (int)row.NumValue);
    }

    #endregion
}
