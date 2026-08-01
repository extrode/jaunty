using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests that the async streaming overloads (QueryStreamAsync, QueryPartialStreamAsync) correctly
/// reject non-DbConnection wrappers with InvalidOperationException. Audit item: round-32 finding —
/// these guards existed but were never exercised by a test.
/// </summary>
public class StreamAsyncFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    private const string Sql =
        "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3";

    private const string ParameterizedSql =
        "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

    public StreamAsyncFallbackTests()
    {
        _realConnection = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    #region QueryStreamAsync

    [Fact]
    public async Task QueryStreamAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryStreamAsync<CategorySummary>(Sql))
            {
            }
        });
    }

    [Fact]
    public async Task QueryStreamAsync_WithParameters_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryStreamAsync<CategorySummary>(ParameterizedSql, new { Id = 1 }))
            {
            }
        });
    }

    [Fact]
    public async Task QueryStreamAsync_WithOptions_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryStreamAsync(Sql, default(CommandOptions<CategorySummary>)))
            {
            }
        });
    }

    [Fact]
    public async Task QueryStreamAsync_WithParametersAndOptions_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryStreamAsync(ParameterizedSql, new { Id = 1 }, default(CommandOptions<CategorySummary>)))
            {
            }
        });
    }

    #endregion

    #region QueryPartialStreamAsync

    [Fact]
    public async Task QueryPartialStreamAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryPartialStreamAsync<CategorySummary>(Sql))
            {
            }
        });
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithParameters_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryPartialStreamAsync<CategorySummary>(ParameterizedSql, new { Id = 1 }))
            {
            }
        });
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithOptions_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryPartialStreamAsync(Sql, default(CommandOptions<CategorySummary>)))
            {
            }
        });
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithParametersAndOptions_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _wrapper.QueryPartialStreamAsync(ParameterizedSql, new { Id = 1 }, default(CommandOptions<CategorySummary>)))
            {
            }
        });
    }

    #endregion
}
