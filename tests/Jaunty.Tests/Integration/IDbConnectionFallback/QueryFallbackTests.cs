using System.Data.SQLite;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests the IDbConnection/IDataReader fallback code paths.
/// By wrapping the real SQLite connection with IDbConnectionWrapper, we force:
/// - ExecuteReader(IDbConnection, ...) instead of ExecuteReader(DbConnection, ...)
/// - DrDispatcher.Resolve(IDataReader, ...) instead of DrDispatcher.Resolve(DbDataReader, ...)
/// - Sync Read() and Convert.ChangeType() instead of async ReadAsync() and GetFieldValue&lt;T&gt;()
/// </summary>
public class QueryFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public QueryFallbackTests()
    {
        _realConnection = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    #region Query (List)

    [Fact]
    public void Query_ViaWrapper_ReturnsResults()
    {
        var categories = _wrapper.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public void Query_ViaWrapper_WithParameters_FiltersCorrectly()
    {
        var categories = _wrapper.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Fact]
    public void QueryPartial_ViaWrapper_ReturnsResults()
    {
        var summaries = _wrapper.QueryPartial<OrderSummary>(
            "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders LIMIT 5");

        Assert.Equal(5, summaries.Count);
        Assert.All(summaries, s => Assert.True(s.OrderId > 0));
    }

    #endregion

    #region QueryFirst / QueryFirstOrDefault

    [Fact]
    public void QueryFirst_ViaWrapper_ReturnsFirst()
    {
        var category = _wrapper.QueryPartialFirst<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public void QueryFirst_ViaWrapper_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _wrapper.QueryPartialFirst<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
                new { Id = -999 }));
    }

    [Fact]
    public void QueryFirstOrDefault_ViaWrapper_ReturnsFirst()
    {
        var category = _wrapper.QueryPartialFirstOrDefault<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        Assert.NotNull(category);
    }

    [Fact]
    public void QueryFirstOrDefault_ViaWrapper_NoResults_ReturnsNull()
    {
        var category = _wrapper.QueryPartialFirstOrDefault<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        Assert.Null(category);
    }

    #endregion

    #region QuerySingle / QuerySingleOrDefault

    [Fact]
    public void QuerySingle_ViaWrapper_ReturnsSingle()
    {
        var category = _wrapper.QueryPartialSingle<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public void QuerySingle_ViaWrapper_MultipleResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _wrapper.QueryPartialSingle<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2"));
    }

    [Fact]
    public void QuerySingle_ViaWrapper_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _wrapper.QueryPartialSingle<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
                new { Id = -999 }));
    }

    [Fact]
    public void QuerySingleOrDefault_ViaWrapper_ReturnsSingle()
    {
        var category = _wrapper.QueryPartialSingleOrDefault<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public void QuerySingleOrDefault_ViaWrapper_NoResults_ReturnsNull()
    {
        var category = _wrapper.QueryPartialSingleOrDefault<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        Assert.Null(category);
    }

    [Fact]
    public void QuerySingleOrDefault_ViaWrapper_MultipleResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _wrapper.QueryPartialSingleOrDefault<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2"));
    }

    #endregion

    #region QueryScalar (IDataReader fallback: Convert.ChangeType instead of GetFieldValue<T>)

    [Fact]
    public void ExecuteScalar_ViaWrapper_ReturnsValue()
    {
        var count = _wrapper.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM categories");

        Assert.True(count > 0);
    }

    [Fact]
    public void ExecuteScalar_ViaWrapper_WithParameters_ReturnsValue()
    {
        var count = _wrapper.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.Equal(1, count);
    }

    [Fact]
    public void QueryScalar_ViaWrapper_ReturnsValue()
    {
        var count = _wrapper.QueryScalar<long>(
            "SELECT COUNT(*) FROM categories");

        Assert.True(count > 0);
    }

    [Fact]
    public void QueryScalar_ViaWrapper_NoResults_ReturnsDefault()
    {
        var result = _wrapper.QueryScalar<long>(
            "SELECT category_id FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        Assert.Equal(0, result);
    }

    #endregion

    #region QueryStream (IDbConnection fallback: inline sync iteration)

    [Fact]
    public void QueryStream_ViaWrapper_YieldsResults()
    {
        var count = 0;
        foreach (var category in _wrapper.QueryPartialStream<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3"))
        {
            Assert.True(category.CategoryId > 0);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void QueryStream_ViaWrapper_EmptyResult_YieldsNothing()
    {
        var count = 0;
        foreach (var _ in _wrapper.QueryPartialStream<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 }))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    #endregion

    #region Dictionary / KeyValuePair / ValueTuple (special type mappers via IDataReader)

    [Fact]
    public void Query_Dictionary_ViaWrapper_ReturnsResults()
    {
        var results = _wrapper.Query<Dictionary<string, object>>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2");

        Assert.Equal(2, results.Count);
        Assert.NotNull(results[0]["CategoryName"]);
    }

    [Fact]
    public void Query_KeyValuePair_ViaWrapper_ReturnsResults()
    {
        var results = _wrapper.Query<KeyValuePair<long, string>>(
            "SELECT category_id, category_name FROM categories ORDER BY category_id LIMIT 2");

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Key > 0);
    }

    [Fact]
    public void Query_ValueTuple_ViaWrapper_ReturnsResults()
    {
        var results = _wrapper.Query<(long, string)>(
            "SELECT category_id, category_name FROM categories ORDER BY category_id LIMIT 2");

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Item1 > 0);
        Assert.NotNull(results[0].Item2);
    }

    #endregion

    #region Connection State Management

    [Fact]
    public void Query_ViaWrapper_ClosedConnection_OpensAndCloses()
    {
        // Connection should start closed
        Assert.Equal(ConnectionState.Closed, _wrapper.State);

        var categories = _wrapper.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 1");

        Assert.Single(categories);
        // Connection should be closed again after query
        Assert.Equal(ConnectionState.Closed, _wrapper.State);
    }

    [Fact]
    public void Query_ViaWrapper_OpenConnection_LeavesOpen()
    {
        _wrapper.Open();
        Assert.Equal(ConnectionState.Open, _wrapper.State);

        var categories = _wrapper.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 1");

        Assert.Single(categories);
        // Connection should remain open
        Assert.Equal(ConnectionState.Open, _wrapper.State);

        _wrapper.Close();
    }

    #endregion
}